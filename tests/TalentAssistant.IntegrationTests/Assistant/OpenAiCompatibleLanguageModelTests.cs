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
                "upcoming_birthday"
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

    private const string CannedContentResponse =
        """{"choices":[{"message":{"content":"Synthetic answer.","tool_calls":null}}]}""";

    private static AssistantRequest SimpleRequest() => new(
        [new ChatMessage("user", "Hello")],
        ChatOrchestrator.ToolCatalog,
        MaxTokens: 700);

    private static OpenAiCompatibleLanguageModel CreateClient(
        StubHttpMessageHandler handler,
        string apiKey = SyntheticApiKey,
        TimeSpan? timeout = null)
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
            new AssistantProviderOptions(apiKey, "test-model"));
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
