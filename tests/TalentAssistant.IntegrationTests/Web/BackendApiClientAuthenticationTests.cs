extern alias Web;

using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

using BackendApiClient = Web::TalentAssistant.Web.Services.BackendApiClient;

namespace TalentAssistant.IntegrationTests.Web;

public sealed class BackendApiClientAuthenticationTests
{
    private const string AccessTokenClaimType = "access_token";
    private const string AccessToken = "circuit-access-token";

    [Theory]
    [InlineData("presence")]
    [InlineData("employees")]
    [InlineData("chat")]
    public async Task ProtectedApiCallsAttachTheCircuitTokenWithoutHttpContext(string endpoint)
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(AccessTokenClaimType, AccessToken)],
            authenticationType: "test"));
        var authenticationStateProvider = new SequenceAuthenticationStateProvider(principal);
        var client = new BackendApiClient(
            httpClient,
            NullLogger<BackendApiClient>.Instance,
            authenticationStateProvider);

        switch (endpoint)
        {
            case "presence":
                var presence = await client.GetPresenceAsync(new DateOnly(2026, 10, 25));
                Assert.Equal(Web::TalentAssistant.Web.Services.WorkforceFailure.None, presence.Failure);
                break;
            case "employees":
                var employees = await client.GetEmployeesAsync(null);
                Assert.Equal(Web::TalentAssistant.Web.Services.WorkforceFailure.None, employees.Failure);
                break;
            case "chat":
                var answer = await client.AskAssistantAsync("hello", []);
                Assert.True(answer.IsSuccess);
                break;
            default:
                Assert.Fail($"Unexpected endpoint '{endpoint}'.");
                break;
        }

        Assert.Equal([$"Bearer {AccessToken}"], handler.AuthorizationHeaders);
    }

    [Fact]
    public async Task ChatRequestCarriesTheConversationId()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var client = new BackendApiClient(
            httpClient,
            NullLogger<BackendApiClient>.Instance,
            new SequenceAuthenticationStateProvider(AuthenticatedPrincipal(AccessToken)));

        await client.AskAssistantAsync("hello", [], "conversation-123");

        Assert.Contains(
            "\"conversationId\":\"conversation-123\"",
            Assert.Single(handler.RequestBodies));
    }

    [Fact]
    public async Task PublicHealthRequestDoesNotSendCircuitBearerToken()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(AccessTokenClaimType, AccessToken)],
            authenticationType: "test"));
        var client = new BackendApiClient(
            httpClient,
            NullLogger<BackendApiClient>.Instance,
            new SequenceAuthenticationStateProvider(principal));

        Assert.Equal(
            Web::TalentAssistant.Web.Services.BackendHealthStatus.Healthy,
            (await client.GetHealthAsync()).Status);
        Assert.Equal([null], handler.AuthorizationHeaders);
    }

    [Theory]
    [InlineData("presence")]
    [InlineData("employees")]
    [InlineData("chat")]
    public async Task UnauthenticatedCircuitDoesNotSendBearerToken(string endpoint)
    {
        using var handler = new RecordingHandler(HttpStatusCode.Unauthorized);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var client = new BackendApiClient(
            httpClient,
            NullLogger<BackendApiClient>.Instance,
            new SequenceAuthenticationStateProvider(anonymous));

        switch (endpoint)
        {
            case "presence":
                Assert.Equal(
                    Web::TalentAssistant.Web.Services.WorkforceFailure.ApiErrorResponse,
                    (await client.GetPresenceAsync(new DateOnly(2026, 10, 25))).Failure);
                break;
            case "employees":
                Assert.Equal(
                    Web::TalentAssistant.Web.Services.WorkforceFailure.ApiErrorResponse,
                    (await client.GetEmployeesAsync(null)).Failure);
                break;
            case "chat":
                var answer = await client.AskAssistantAsync("hello", []);
                Assert.Equal(Web::TalentAssistant.Web.Services.AssistantFailure.Other, answer.Failure);
                Assert.False(answer.IsSuccess);
                break;
            default:
                Assert.Fail($"Unexpected endpoint '{endpoint}'.");
                break;
        }

        Assert.Equal([null], handler.AuthorizationHeaders);
    }

    [Fact]
    public async Task TokenIsReadFromTheCurrentCircuitPrincipalForEachRequest()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var client = new BackendApiClient(
            httpClient,
            NullLogger<BackendApiClient>.Instance,
            new SequenceAuthenticationStateProvider(
                AuthenticatedPrincipal("first-request-token"),
                AuthenticatedPrincipal("second-request-token")));

        await client.GetPresenceAsync(new DateOnly(2026, 10, 25));
        await client.GetEmployeesAsync(null);

        Assert.Equal(
            ["Bearer first-request-token", "Bearer second-request-token"],
            handler.AuthorizationHeaders);
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(string token) =>
        new(new ClaimsIdentity(
            [new Claim(AccessTokenClaimType, token)],
            authenticationType: "test"));

    private sealed class SequenceAuthenticationStateProvider(params ClaimsPrincipal[] principals)
        : AuthenticationStateProvider
    {
        private int _index;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var principal = principals[Math.Min(_index, principals.Length - 1)];
            _index++;
            return Task.FromResult(new AuthenticationState(principal));
        }
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public List<string?> AuthorizationHeaders { get; } = [];

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            var responseBody = request.RequestUri?.AbsolutePath switch
            {
                "/health" => """{"status":"healthy"}""",
                "/presence" => """{"date":"2026-10-25","entries":[]}""",
                "/employees" => "[]",
                "/chat" => """{"answer":"hello","toolsUsed":[],"sources":[],"completed":true}""",
                _ => throw new InvalidOperationException("Unexpected API request path.")
            };

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    statusCode == HttpStatusCode.OK ? responseBody : string.Empty,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
