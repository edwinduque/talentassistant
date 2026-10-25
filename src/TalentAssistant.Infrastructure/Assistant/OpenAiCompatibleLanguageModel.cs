using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TalentAssistant.Application.Assistant;

namespace TalentAssistant.Infrastructure.Assistant;

/// <summary>
/// OpenAI-compatible language model client over raw HttpClient (SPEC-009 S2,
/// ADR-0011, SPEC-022): POST {BaseUrl}/chat/completions with Bearer auth and
/// function tools, no SDK and no new packages. Groq and Gemini share this
/// client; provider settings select the base URL, model, retry budget and the
/// optional schema normalization. Provider failures translate into
/// <see cref="AssistantProviderUnavailableException"/>; no exception escapes
/// untranslated and no secret is ever logged (NFR-004).
/// Un 400 de Groq con code tool_use_failed se reintenta una vez: es un fallo
/// transitorio de la generación de la llamada de herramienta, no del prompt.
/// </summary>
public sealed class OpenAiCompatibleLanguageModel : IAssistantLanguageModel
{
    private const int MaximumProviderErrorBodyBytes = 8 * 1024;
    private const int MaximumProviderErrorDetailLength = 300;

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AssistantProviderOptions _options;
    private readonly ILogger<OpenAiCompatibleLanguageModel> _logger;

    public OpenAiCompatibleLanguageModel(
        HttpClient httpClient,
        AssistantProviderOptions options,
        ILogger<OpenAiCompatibleLanguageModel> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<AssistantCompletion> CompleteAsync(
        AssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A missing key degrades before any HTTP call (SPEC-009 §8.6).
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.NotConfigured,
                "The assistant provider API key is not configured.");
        }

        var requestBody = BuildRequestBody(request);
        var maximumAttempts = Math.Max(1, _options.MaxAttempts);
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "chat/completions")
            {
                Content = new StringContent(
                    requestBody,
                    Encoding.UTF8,
                    "application/json")
            };
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            HttpResponseMessage httpResponse;
            try
            {
                httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                throw new AssistantProviderUnavailableException(
                    AssistantProviderFailureReason.Unreachable,
                    "The assistant provider could not be reached.",
                    exception);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller cancelled (endpoint shutdown), not the provider timeout.
                throw;
            }
            catch (OperationCanceledException exception)
            {
                // HttpClient raises this on its own Timeout elapsing.
                throw new AssistantProviderUnavailableException(
                    AssistantProviderFailureReason.Timeout,
                    "The assistant provider request timed out.",
                    exception);
            }

            using (httpResponse)
            {
                if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new AssistantProviderUnavailableException(
                        AssistantProviderFailureReason.RateLimited,
                        "The assistant provider rate limit was exceeded.");
                }

                if (httpResponse.IsSuccessStatusCode)
                {
                    var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                    return ParseCompletion(body);
                }

                var statusCode = (int)httpResponse.StatusCode;
                var providerError = await ReadProviderErrorAsync(httpResponse, cancellationToken);
                _logger.LogWarning(
                    "The assistant provider {Provider} returned HTTP {StatusCode} on attempt {Attempt}: {Code} {Message}",
                    _options.Name,
                    statusCode,
                    attempt,
                    providerError.Code,
                    providerError.Message);

                // Groq's tool_use_failed is a transient generation failure: the
                // same request often succeeds on a second attempt. A 5xx is
                // also retried once; 4xx configuration errors are not.
                var retryable = attempt < maximumAttempts
                    && ((httpResponse.StatusCode == HttpStatusCode.BadRequest
                            && providerError.IsToolUseFailed)
                        || statusCode >= 500);
                if (!retryable)
                {
                    throw new AssistantProviderUnavailableException(
                        AssistantProviderFailureReason.InvalidResponse,
                        $"The assistant provider returned HTTP {statusCode}.");
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        throw new AssistantProviderUnavailableException(
            AssistantProviderFailureReason.InvalidResponse,
            "The assistant provider did not return a completion.");
    }

    private static async Task<ProviderError> ReadProviderErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (body.Length > MaximumProviderErrorBodyBytes)
        {
            body = body[..MaximumProviderErrorBodyBytes];
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object)
            {
                var code = error.TryGetProperty("code", out var codeValue)
                    && codeValue.ValueKind == JsonValueKind.String
                        ? codeValue.GetString()
                        : null;
                var message = error.TryGetProperty("message", out var messageValue)
                    && messageValue.ValueKind == JsonValueKind.String
                        ? messageValue.GetString()
                        : null;
                return new ProviderError(code, Truncate(message));
            }
        }
        catch (JsonException)
        {
            // Non-JSON provider errors are not logged verbatim: they can echo
            // the request. The status code is enough for the caller.
        }

        return new ProviderError(null, null);
    }

    private static string? Truncate(string? value) =>
        value is null || value.Length <= MaximumProviderErrorDetailLength
            ? value
            : value[..MaximumProviderErrorDetailLength];

    private sealed record ProviderError(string? Code, string? Message)
    {
        public bool IsToolUseFailed =>
            string.Equals(Code, "tool_use_failed", StringComparison.OrdinalIgnoreCase)
            || (Message?.Contains("tool_use_failed", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private string BuildRequestBody(AssistantRequest request)
    {
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            var node = new JsonObject
            {
                ["role"] = message.Role,
                ["content"] = message.Content
            };

            if (message.ToolCalls is { Count: > 0 })
            {
                var toolCalls = new JsonArray();
                foreach (var toolCall in message.ToolCalls)
                {
                    toolCalls.Add(new JsonObject
                    {
                        ["id"] = toolCall.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = toolCall.Name,
                            ["arguments"] = toolCall.ArgumentsJson
                        }
                    });
                }

                node["tool_calls"] = toolCalls;
            }

            if (message.ToolCallId is not null)
            {
                node["tool_call_id"] = message.ToolCallId;
            }

            messages.Add(node);
        }

        var tools = new JsonArray();
        foreach (var tool in request.Tools)
        {
            var parameters = JsonNode.Parse(tool.ParametersJson);
            if (_options.NormalizeToolSchemas)
            {
                parameters = ToolSchemaNormalizer.Normalize(parameters);
            }

            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    // The hand-written JSON Schema is embedded verbatim unless
                    // the provider needs the normalized subset.
                    ["parameters"] = parameters
                }
            });
        }

        var body = new JsonObject
        {
            ["model"] = _options.Model,
            ["messages"] = messages,
            ["tools"] = tools,
            ["max_tokens"] = request.MaxTokens,
            ["temperature"] = 0.1
        };

        if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
        {
            body["reasoning_effort"] = _options.ReasoningEffort;
        }

        return body.ToJsonString(SerializerOptions);
    }

    private static AssistantCompletion ParseCompletion(string body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            throw new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.InvalidResponse,
                "The assistant provider returned an unreadable response.",
                exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                throw new AssistantProviderUnavailableException(
                    AssistantProviderFailureReason.InvalidResponse,
                    "The assistant provider response contains no choices.");
            }

            var message = choices[0];
            if (!message.TryGetProperty("message", out var messageElement)
                || messageElement.ValueKind != JsonValueKind.Object)
            {
                throw new AssistantProviderUnavailableException(
                    AssistantProviderFailureReason.InvalidResponse,
                    "The assistant provider response contains no message.");
            }

            string? content = null;
            if (messageElement.TryGetProperty("content", out var contentElement)
                && contentElement.ValueKind == JsonValueKind.String)
            {
                content = contentElement.GetString();
            }

            var toolCalls = new List<AssistantToolCall>();
            if (messageElement.TryGetProperty("tool_calls", out var toolCallsElement)
                && toolCallsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var toolCallElement in toolCallsElement.EnumerateArray())
                {
                    if (!toolCallElement.TryGetProperty("function", out var functionElement)
                        || functionElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var name = functionElement.TryGetProperty("name", out var nameElement)
                        && nameElement.ValueKind == JsonValueKind.String
                            ? nameElement.GetString()
                            : null;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var arguments = functionElement.TryGetProperty("arguments", out var argumentsElement)
                        && argumentsElement.ValueKind == JsonValueKind.String
                            ? argumentsElement.GetString() ?? "{}"
                            : "{}";
                    var id = toolCallElement.TryGetProperty("id", out var idElement)
                        && idElement.ValueKind == JsonValueKind.String
                            ? idElement.GetString() ?? string.Empty
                            : string.Empty;

                    toolCalls.Add(new AssistantToolCall(id, name, arguments));
                }
            }

            return new AssistantCompletion(content, toolCalls);
        }
    }
}
