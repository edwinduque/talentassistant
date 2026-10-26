using Microsoft.Extensions.Logging.Abstractions;
using TalentAssistant.Application.Assistant;
using TalentAssistant.Infrastructure.Assistant;

namespace TalentAssistant.IntegrationTests.Assistant;

/// <summary>
/// SPEC-022 S2–S7: weighted sticky selection, failover over the same transcript
/// and the in-memory circuit breaker. Every provider is a fake; no network.
/// </summary>
public sealed class RoutingLanguageModelTests
{
    [Fact]
    public async Task NoConfiguredProviderThrowsNotConfigured()
    {
        var router = CreateRouter([]);

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => router.CompleteAsync(Request("conv-1")));

        Assert.Equal(AssistantProviderFailureReason.NotConfigured, exception.Reason);
    }

    [Fact]
    public async Task SingleProviderIsAlwaysUsed()
    {
        var gemini = new FakeProvider("gemini");
        var router = CreateRouter([new("gemini", 50, gemini)]);

        var completion = await router.CompleteAsync(Request("conv-1"));

        Assert.Equal("gemini answer", completion.Content);
        Assert.Equal(1, gemini.CallCount);
    }

    [Fact]
    public async Task SameConversationUsesTheSameProviderAcrossTurns()
    {
        var groq = new FakeProvider("groq");
        var gemini = new FakeProvider("gemini");

        for (var index = 0; index < 20; index++)
        {
            var conversation = $"conversation-{index}";
            var first = await CreateRouter(Slots(groq, gemini)).CompleteAsync(Request(conversation));
            var second = await CreateRouter(Slots(groq, gemini)).CompleteAsync(Request(conversation));

            Assert.Equal(first.Content, second.Content);
        }
    }

    [Fact]
    public async Task DifferentConversationsSplitByWeight()
    {
        var groq = new FakeProvider("groq");
        var gemini = new FakeProvider("gemini");
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < 20; index++)
        {
            var completion = await CreateRouter(Slots(groq, gemini))
                .CompleteAsync(Request($"conversation-{index}"));
            seen.Add(completion.Content!);
        }

        Assert.Contains("groq answer", seen);
        Assert.Contains("gemini answer", seen);
    }

    [Fact]
    public async Task ZeroWeightProviderIsNeverSelected()
    {
        var groq = new FakeProvider("groq");
        var gemini = new FakeProvider("gemini");

        for (var index = 0; index < 20; index++)
        {
            var completion = await CreateRouter(
                    [new("groq", 100, groq), new("gemini", 0, gemini)])
                .CompleteAsync(Request($"conversation-{index}"));
            Assert.Equal("groq answer", completion.Content);
        }

        Assert.Equal(0, gemini.CallCount);
    }

    [Fact]
    public async Task PreferredFailureFallsBackAndSticksForTheTurn()
    {
        var groq = new FakeProvider("groq")
        {
            Failure = new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.RateLimited,
                "limited")
        };
        var gemini = new FakeProvider("gemini");
        var router = CreateRouter([new("groq", 100, groq), new("gemini", 0, gemini)]);

        var first = await router.CompleteAsync(Request("conv-1"));
        var second = await router.CompleteAsync(Request("conv-1"));

        Assert.Equal("gemini answer", first.Content);
        Assert.Equal("gemini answer", second.Content);
        Assert.Equal(1, groq.CallCount);
        Assert.Equal(2, gemini.CallCount);
    }

    [Fact]
    public async Task BothRateLimitedFailuresReportRateLimited()
    {
        var groq = new FakeProvider("groq")
        {
            Failure = new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.RateLimited,
                "limited")
        };
        var gemini = new FakeProvider("gemini")
        {
            Failure = new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.RateLimited,
                "limited")
        };
        var router = CreateRouter(Slots(groq, gemini));

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => router.CompleteAsync(Request("conv-1")));

        Assert.Equal(AssistantProviderFailureReason.RateLimited, exception.Reason);
        Assert.Equal(1, groq.CallCount);
        Assert.Equal(1, gemini.CallCount);
    }

    [Fact]
    public async Task SecondFailureReasonIsReportedWhenNotRateLimited()
    {
        var groq = new FakeProvider("groq")
        {
            Failure = new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.RateLimited,
                "limited")
        };
        var gemini = new FakeProvider("gemini")
        {
            Failure = new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.Timeout,
                "timeout")
        };
        var router = CreateRouter([new("groq", 100, groq), new("gemini", 0, gemini)]);

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => router.CompleteAsync(Request("conv-1")));

        Assert.Equal(AssistantProviderFailureReason.Timeout, exception.Reason);
    }

    [Fact]
    public async Task BlockedPreferredRoutesToTheOtherProvider()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var health = new AssistantProviderHealth(clock, failureThreshold: 1, TimeSpan.FromMinutes(2));
        var groq = new FakeProvider("groq")
        {
            Failure = new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.RateLimited,
                "limited")
        };
        var gemini = new FakeProvider("gemini");
        var slots = new[] { new AssistantProviderSlot("groq", 100, groq), new("gemini", 0, gemini) };

        await CreateRouter(slots, health, clock).CompleteAsync(Request("conv-1"));
        var groqCallsAfterFailure = groq.CallCount;

        var completion = await CreateRouter(slots, health, clock).CompleteAsync(Request("conv-1"));

        Assert.Equal("gemini answer", completion.Content);
        Assert.Equal(groqCallsAfterFailure, groq.CallCount);
        Assert.False(health.IsAvailable("groq"));
    }

    [Fact]
    public async Task BothBlockedThrowsWithoutCallingAProvider()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var health = new AssistantProviderHealth(clock, failureThreshold: 1, TimeSpan.FromMinutes(2));
        health.RecordFailure("groq");
        health.RecordFailure("gemini");
        var groq = new FakeProvider("groq");
        var gemini = new FakeProvider("gemini");

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => CreateRouter(Slots(groq, gemini), health, clock).CompleteAsync(Request("conv-1")));

        Assert.Equal(AssistantProviderFailureReason.RateLimited, exception.Reason);
        Assert.Equal(0, groq.CallCount);
        Assert.Equal(0, gemini.CallCount);
    }

    [Fact]
    public async Task CooldownExpiryAllowsAProbe()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var health = new AssistantProviderHealth(clock, failureThreshold: 1, TimeSpan.FromMinutes(1));
        health.RecordFailure("groq");
        var groq = new FakeProvider("groq");
        var gemini = new FakeProvider("gemini");

        clock.Now = clock.Now.AddSeconds(61);
        var completion = await CreateRouter(
                [new("groq", 100, groq), new("gemini", 0, gemini)],
                health,
                clock)
            .CompleteAsync(Request("conv-1"));

        Assert.Equal("groq answer", completion.Content);
        Assert.Equal(1, groq.CallCount);
    }

    [Fact]
    public async Task FallbackRejectsForeignToolTranscriptAndRetriesWithCleanMessages()
    {
        var groq = new FakeProvider("groq")
        {
            Failure = new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.RateLimited,
                "limited")
        };
        var gemini = new FakeProvider("gemini");
        var requests = new List<AssistantRequest>();
        gemini.Handler = (request, call) =>
        {
            requests.Add(request);
            return call == 1
                ? Task.FromException<AssistantCompletion>(
                    new AssistantProviderUnavailableException(
                        AssistantProviderFailureReason.InvalidResponse,
                        "rejected foreign tool transcript"))
                : Task.FromResult(new AssistantCompletion("clean answer", []));
        };
        var router = CreateRouter([new("groq", 100, groq), new("gemini", 0, gemini)]);
        var request = new AssistantRequest(
            [
                new ChatMessage("system", "system"),
                new ChatMessage("user", "pregunta"),
                new ChatMessage(
                    "assistant",
                    string.Empty,
                    ToolCalls: [new AssistantToolCall("call-1", "search_employees", "{}")]),
                new ChatMessage("tool", "[]", ToolCallId: "call-1")
            ],
            [],
            MaxTokens: 700,
            ConversationId: "conv-1");

        var completion = await router.CompleteAsync(request);

        Assert.Equal("clean answer", completion.Content);
        Assert.Equal(2, gemini.CallCount);
        Assert.Equal(2, requests.Count);
        Assert.Contains(requests[0].Messages, message => message.ToolCalls is not null);
        Assert.DoesNotContain(
            requests[1].Messages,
            message => message.ToolCalls is not null || message.ToolCallId is not null);
        Assert.Contains(requests[1].Messages, message => message.Role == "system");
        Assert.Contains(requests[1].Messages, message => message.Role == "user");
    }

    [Fact]
    public async Task FallbackInvalidResponseWithoutToolMessagesIsNotRetried()
    {
        var groq = new FakeProvider("groq")
        {
            Failure = new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.RateLimited,
                "limited")
        };
        var gemini = new FakeProvider("gemini")
        {
            Failure = new AssistantProviderUnavailableException(
                AssistantProviderFailureReason.InvalidResponse,
                "invalid request")
        };
        var router = CreateRouter([new("groq", 100, groq), new("gemini", 0, gemini)]);

        var exception = await Assert.ThrowsAsync<AssistantProviderUnavailableException>(
            () => router.CompleteAsync(Request("conv-1")));

        Assert.Equal(AssistantProviderFailureReason.InvalidResponse, exception.Reason);
        Assert.Equal(1, gemini.CallCount);
    }

    private static AssistantProviderSlot[] Slots(FakeProvider groq, FakeProvider gemini) =>
        [new("groq", 50, groq), new("gemini", 50, gemini)];

    private static RoutingLanguageModel CreateRouter(IReadOnlyList<AssistantProviderSlot> slots)
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        return CreateRouter(
            slots,
            new AssistantProviderHealth(clock, failureThreshold: 2, TimeSpan.FromMinutes(2)),
            clock);
    }

    private static RoutingLanguageModel CreateRouter(
        IReadOnlyList<AssistantProviderSlot> slots,
        AssistantProviderHealth health,
        TimeProvider clock) =>
        new(slots, health, NullLogger<RoutingLanguageModel>.Instance);

    private static AssistantRequest Request(string conversationId) =>
        new([new ChatMessage("user", "hola")], [], MaxTokens: 700, conversationId);

    private sealed class FakeProvider(string name) : IAssistantLanguageModel
    {
        public int CallCount { get; private set; }

        public Exception? Failure { get; init; }

        public Func<AssistantRequest, int, Task<AssistantCompletion>>? Handler { get; set; }

        public Task<AssistantCompletion> CompleteAsync(
            AssistantRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Handler is not null)
            {
                return Handler(request, CallCount);
            }

            return Failure is null
                ? Task.FromResult(new AssistantCompletion($"{name} answer", []))
                : Task.FromException<AssistantCompletion>(Failure);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
