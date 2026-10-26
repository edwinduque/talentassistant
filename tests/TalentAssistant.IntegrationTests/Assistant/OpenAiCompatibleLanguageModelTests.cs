using System.Net;
using System.Text;
using System.Text.Json;
using TalentAssistant.Application.Assistant;
using TalentAssistant.Infrastructure.Assistant;

namespace TalentAssistant.IntegrationTests.Assistant;

/// <summary>
/// Provider client tests over a stub HttpMessageHandler: no sockets, no
/// network, synthetic credentials only (SPEC-009 S2, S4; AC-031).
/// </summary>
public sealed class OpenAiCompatibleLanguageModelTests
{
    private const string SyntheticApiKey = "synthetic-test-key";

    [Fact]
    public async Task OutgoingRequestCarriesModelToolsMessagesAndAuthorization()
    {
        var handler = new StubHttpMessageHandler(_ => Json(CannedContentResponse));
        var client = CreateClient(handler);

        var request = new AssistantRequest(
            [
                new ChatMessage("system", "You are Talent Assistant."),
                new ChatMessage("user", "Where does Juan work?"),
                new ChatMessage("assistant", string.Empty, ToolCalls:
                [
                    new AssistantToolCall("call-1", "search_employees", """{"name":"Juan"}""")
                ]),
                new ChatMessage("tool", """[{"Id":"employee-demo-juan"}]""", ToolCallId: "call-1")
            ],
            ChatOrchestrator.ToolCatalog,
            MaxTokens: 512);

        var completion = await client.CompleteAsync(request);

        Assert.Equal("Synthetic answer.", completion.Content);

        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Request.Method);
        Assert.Equal(
            new Uri("https://llm.test/openai/v1/chat/completions"),
            sent.Request.RequestUri);
        Assert.Equal("Bearer", sent.Request.Headers.Authorization?.Scheme);
        Assert.Equal(SyntheticApiKey, sent.Request.Headers.Authorization?.Parameter);
        Assert.Equal("application/json", sent.Request.Content?.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(sent.Body);
        var root = body.RootElement;
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.Equal(512, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.1, root.GetProperty("temperature").GetDouble());

        var messages = root.GetProperty("messages").EnumerateArray()
            .ToArray();
        Assert.Equal(4, messages.Length);
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());

        // The assistant turn keeps its tool_calls and the tool answer its id.
        var assistantToolCall = messages[2].GetProperty("tool_calls")[0];
        Assert.Equal("function", assistantToolCall.GetProperty("type").GetString());
        Assert.Equal("search_employees", assistantToolCall.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal(
            """{"name":"Juan"}""",
            assistantToolCall.GetProperty("function").GetProperty("arguments").GetString());
        Assert.Equal(
            "call-1",
            messages[3].GetProperty("tool_call_id").GetString());

        var tools = root.GetProperty("tools").EnumerateArray()
            .ToArray();
        var names = tools
            .Select(tool => tool.GetProperty("function").GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(
            new[]
            {
                "search_employees",
                "presence",
                "timeoff",
                "tasks",
                "attendance_insights",
                "upcoming_birthday",
                "query_hr_data"
            },
            names!);
        Assert.All(tools, tool =>
        {
            Assert.Equal("function", tool.GetProperty("type").GetString());
            Assert.Equal(
                JsonValueKind.Object,
                tool.GetProperty("function").GetProperty("parameters").ValueKind);
        });
    }

    [Fact]
    public async Task ParsesAContentCompletion()
    {
        var handler = new StubHttpMessageHandler(_ => Json(CannedContentResponse));
        var client = CreateClient(handler);

        var completion = await client.CompleteAsync(SimpleRequest());

        Assert.Equal("Synthetic answer.", completion.Content);
        Assert.Empty(completion.ToolCalls);
    }

    [Fact]
    public async Task ParsesAToolCallCompletion()
    {
        const string response =
            """
            {
              "choices": [
                {
                  "message": {
                    "content": null,
                    "tool_calls": [
                      {
                        "id": "call-1",
                        "type": "function",
                        "extra_content": {
                          "google": {
                            "thought_signature": "synthetic-signature"
                          }
                        },
                        "function": {
                          "name": "search_employees",
                          "arguments": "{\"name\":\"Juan\"}"
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;
        var handler = new StubHttpMessageHandler(_ => Json(response));
        var client = CreateClient(handler);

        var completion = await client.CompleteAsync(SimpleRequest());

        Assert.Null(completion.Content);
        var toolCall = Assert.Single(completion.ToolCalls);
        Assert.Equal("call-1", toolCall.Id);
        Assert.Equal("search_employees", toolCall.Name);
        Assert.Equal("""{"name":"Juan"}""", toolCall.ArgumentsJson);
        Assert.NotNull(toolCall.ExtraContentJson);
        Assert.Contains("synthetic-signature", toolCall.ExtraContentJson);
    }

    [Fact]
    public async Task ReplaysToolCallExtraContent()
    {
        var handler = new StubHttpMessageHandler(_ => Json(CannedContentResponse));
        var client = CreateClient(handler);
        var request = new AssistantRequest(
            [
                new ChatMessage("system", "system"),
                new ChatMessage("user", "question"),
                new ChatMessage("assistant", string.Empty, ToolCalls:
                [
                    new AssistantToolCall(
                        "call-1",
                        "search_employees",
                        """{"name":"Juan"}""",
                        """{"google":{"thought_signature":"synthetic-signature"}}""")
                ]),
                new ChatMessage("tool", "[]", ToolCallId: "call-1")
            ],
            ChatOrchestrator.ToolCatalog,
            MaxTokens: 512);

        await client.CompleteAsync(request);

        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        var toolCall = body.RootElement
            .GetProperty("messages")[2]
            .GetProperty("tool_calls")[0];
        Assert.Equal(
            "synthetic-signature",
            toolCall.GetProperty("extra_content")
                .GetProperty("google")
                .GetProperty("thought_signature")
                .GetString());
    }

    [Fact]
    public async Task Http429MapsToRateLimited()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(
            HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":"rate limited"}""")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => client.CompleteAsync(SimpleRequest()));

        Assert.Equal(AssistantProviderFailureReason.RateLimited, exception.Reason);
    }

    [Fact]
    public async Task Http400ToolUseFailedIsRetriedOnceAndSucceeds()
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            calls++;
            return calls == 1
                ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        """{"error":{"code":"tool_use_failed","message":"Failed to call a function."}}""")
                }
                : Json(CannedContentResponse);
        });
        var client = CreateClient(handler);

        var completion = await client.CompleteAsync(SimpleRequest());

        Assert.Equal("Synthetic answer.", completion.Content);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Http400InvalidRequestIsNotRetried()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(
            HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error":{"code":"invalid_request_error","message":"Invalid request."}}""")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => client.CompleteAsync(SimpleRequest()));

        Assert.Equal(AssistantProviderFailureReason.InvalidResponse, exception.Reason);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DelayedRequestMapsToTimeout()
    {
        var handler = new StubHttpMessageHandler(
            (_, cancellationToken) =>
                Delay(TimeSpan.FromSeconds(5), cancellationToken, Json(CannedContentResponse)));
        // A tiny HttpClient timeout cancels the handler token after 100 ms and
        // surfaces the provider-timeout path without waiting for the delay.
        var client = CreateClient(handler, timeout: TimeSpan.FromMilliseconds(100));

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => client.CompleteAsync(SimpleRequest()));

        Assert.Equal(AssistantProviderFailureReason.Timeout, exception.Reason);
    }

    [Fact]
    public async Task MissingApiKeyThrowsNotConfiguredWithoutInvokingTheHandler()
    {
        var handler = new StubHttpMessageHandler(_ => Json(CannedContentResponse));
        var client = CreateClient(handler, apiKey: string.Empty);

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => client.CompleteAsync(SimpleRequest()));

        Assert.Equal(AssistantProviderFailureReason.NotConfigured, exception.Reason);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task MalformedJsonThrowsInvalidResponse()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("this is not json", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => client.CompleteAsync(SimpleRequest()));

        Assert.Equal(AssistantProviderFailureReason.InvalidResponse, exception.Reason);
    }

    [Fact]
    public async Task MissingChoicesThrowsInvalidResponse()
    {
        var handler = new StubHttpMessageHandler(_ => Json("""{"choices":[]}"""));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => client.CompleteAsync(SimpleRequest()));

        Assert.Equal(AssistantProviderFailureReason.InvalidResponse, exception.Reason);
    }

    [Fact]
    public async Task SingleAttemptOptionDoesNotRetryServerErrors()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(
            HttpStatusCode.InternalServerError));
        var client = CreateClient(handler, maxAttempts: 1);

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => client.CompleteAsync(SimpleRequest()));

        Assert.Equal(AssistantProviderFailureReason.InvalidResponse, exception.Reason);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ReasoningEffortIsSentOnlyWhenConfigured()
    {
        var handler = new StubHttpMessageHandler(_ => Json(CannedContentResponse));
        await CreateClient(handler, reasoningEffort: "low")
            .CompleteAsync(SimpleRequest());

        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("low", body.RootElement.GetProperty("reasoning_effort").GetString());

        var plainHandler = new StubHttpMessageHandler(_ => Json(CannedContentResponse));
        await CreateClient(plainHandler).CompleteAsync(SimpleRequest());

        using var plainBody = JsonDocument.Parse(plainHandler.Requests[0].Body);
        Assert.False(plainBody.RootElement.TryGetProperty("reasoning_effort", out _));
    }

    [Fact]
    public async Task NormalizedToolSchemasHaveNoNullableUnions()
    {
        var handler = new StubHttpMessageHandler(_ => Json(CannedContentResponse));
        await CreateClient(handler, normalizeToolSchemas: true)
            .CompleteAsync(SimpleRequest());

        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        foreach (var tool in body.RootElement.GetProperty("tools").EnumerateArray())
        {
            AssertNoNullableTypeArray(
                tool.GetProperty("function").GetProperty("parameters"));
        }
    }

    [Fact]
    public async Task CanonicalSchemasRemainUntouchedWithoutNormalization()
    {
        var handler = new StubHttpMessageHandler(_ => Json(CannedContentResponse));
        await CreateClient(handler).CompleteAsync(SimpleRequest());

        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        var hasTypeUnion = body.RootElement.GetProperty("tools")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("function").GetProperty("parameters"))
            .Any(ContainsTypeUnion);
        Assert.True(hasTypeUnion, "The canonical schema should keep its nullable type unions.");
    }

    [Fact]
    public async Task GeminiStyleOptionsOmitDeprecatedTemperature()
    {
        var handler = new StubHttpMessageHandler(_ => Json(CannedContentResponse));
        await CreateClient(handler, temperature: null).CompleteAsync(SimpleRequest());

        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.False(body.RootElement.TryGetProperty("temperature", out _));
    }

    private const string CannedContentResponse =
        """{"choices":[{"message":{"content":"Synthetic answer.","tool_calls":null}}]}""";

    private static AssistantRequest SimpleRequest() => new(
        [new ChatMessage("user", "Hello")],
        ChatOrchestrator.ToolCatalog,
        MaxTokens: 700);

    private static OpenAiCompatibleLanguageModel CreateClient(
        StubHttpMessageHandler handler,
        string apiKey = SyntheticApiKey,
        TimeSpan? timeout = null,
        int maxAttempts = 2,
        string? reasoningEffort = null,
        bool normalizeToolSchemas = false,
        double? temperature = 0.1)
    {
        var httpClient = new HttpClient(handler)
        {
            // Trailing slash: relative "chat/completions" must append, not
            // replace the "v1" segment (same normalization as the DI helper).
            BaseAddress = new Uri("https://llm.test/openai/v1/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };
        return new OpenAiCompatibleLanguageModel(
            httpClient,
            new AssistantProviderOptions(
                apiKey,
                "test-model",
                "test-provider",
                reasoningEffort,
                maxAttempts,
                normalizeToolSchemas,
                temperature),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAiCompatibleLanguageModel>.Instance);
    }

    private static bool ContainsTypeUnion(JsonElement node) =>
        node.ValueKind switch
        {
            JsonValueKind.Object => node.EnumerateObject().Any(property =>
                (property.NameEquals("type") && property.Value.ValueKind == JsonValueKind.Array)
                || ContainsTypeUnion(property.Value)),
            JsonValueKind.Array => node.EnumerateArray().Any(ContainsTypeUnion),
            _ => false
        };

    private static void AssertNoNullableTypeArray(JsonElement node)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in node.EnumerateObject())
                {
                    if (property.NameEquals("type"))
                    {
                        // The adapter must leave a single string type, never a
                        // nullable or multi-type union.
                        Assert.Equal(JsonValueKind.String, property.Value.ValueKind);
                    }
                    else
                    {
                        AssertNoNullableTypeArray(property.Value);
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                {
                    AssertNoNullableTypeArray(item);
                }

                break;
        }
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static async Task<HttpResponseMessage> Delay(
        TimeSpan delay,
        CancellationToken cancellationToken,
        HttpResponseMessage response)
    {
        await Task.Delay(delay, cancellationToken);
        return response;
    }

    /// <summary>
    /// Handler stub that records the outgoing request and its body, then
    /// replies from a canned responder. No sockets are ever opened.
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
            : this((request, _) => responder(request))
        {
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            : this(request => Task.FromResult(responder(request)))
        {
        }

        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        public int CallCount => Requests.Count;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));

            return await _responder(request, cancellationToken);
        }
    }
}
