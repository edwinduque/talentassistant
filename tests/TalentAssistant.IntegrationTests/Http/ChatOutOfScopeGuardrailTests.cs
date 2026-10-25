using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TalentAssistant.Application.Assistant;
using TalentAssistant.Infrastructure.Authentication;

namespace TalentAssistant.IntegrationTests.Http;

public sealed class ChatOutOfScopeGuardrailTests : IClassFixture<OutOfScopeChatApiFactory>, IAsyncLifetime
{
    private const string UserName = OutOfScopeChatApiFactory.SeedUserName;
    private const string Password = OutOfScopeChatApiFactory.SeedPassword;
    private readonly OutOfScopeChatApiFactory _factory;

    public ChatOutOfScopeGuardrailTests(OutOfScopeChatApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__TalentAssistantTests");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Set ConnectionStrings__TalentAssistantTests to the dedicated talentassistant_tests database.");

        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        Assert.Equal(connectionString, configuration["ConnectionStrings:TalentAssistant"]);

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<InitializeLocalAuthenticationData>()
            .InitializeAsync();
        _factory.LanguageModel.Reset();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PythonFunctionRequestIsDeclinedThroughChatWithoutTools()
    {
        const string requestText = "creame una funcion en python para retornar el numero mayor.";
        var client = _factory.CreateClient();
        var token = await LoginAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/chat")
        {
            Content = JsonContent.Create(new { message = requestText })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.Contains("asistente", root.GetProperty("answer").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(root.GetProperty("toolsUsed").EnumerateArray());
        Assert.Empty(root.GetProperty("sources").EnumerateArray());

        Assert.Equal(1, _factory.LanguageModel.CallCount);
        var system = Assert.Single(
            _factory.LanguageModel.LastRequest!.Messages,
            message => message.Role == "system");
        Assert.Contains("outside HR/HCM", system.Content);
        Assert.Contains("never provide the unrelated content", system.Content);
    }

    [Fact]
    public async Task ChatForwardsTheConversationIdToTheProvider()
    {
        const string conversationId = "conversation-http-1";
        var client = _factory.CreateClient();
        var token = await LoginAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/chat")
        {
            Content = JsonContent.Create(new
            {
                message = "Hola",
                conversationId
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(conversationId, _factory.LanguageModel.LastRequest?.ConversationId);
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName = UserName, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty("accessToken").GetString()!;
    }
}

public sealed class OutOfScopeChatApiFactory : WebApplicationFactory<Program>
{
    private const string DefaultAiModel = "openai/gpt-oss-120b";
    public const string SeedUserName = "assistant-guardrail@talentassistant.local";
    public const string SeedPassword = "Guardrail!Integration123";

    public GuardrailLanguageModel LanguageModel { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__TalentAssistantTests");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__TalentAssistantTests to the dedicated talentassistant_tests database.");
        }

        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:TalentAssistant"] = connectionString,
            ["Attendance:Source"] = "Dataset",
            ["Authentication:SigningKey"] = new string('s', 64),
            ["Authentication:SeedUserName"] = SeedUserName,
            ["Authentication:SeedPassword"] = SeedPassword,
            ["AI:ApiKey"] = string.Empty,
            ["AI:Model"] = DefaultAiModel
        };

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(overrides));
        foreach (var (key, value) in overrides)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAssistantLanguageModel>();
            services.AddSingleton<IAssistantLanguageModel>(LanguageModel);
        });
    }
}

public sealed class GuardrailLanguageModel : IAssistantLanguageModel
{
    public int CallCount { get; private set; }

    public AssistantRequest? LastRequest { get; private set; }

    public void Reset()
    {
        CallCount = 0;
        LastRequest = null;
    }

    public Task<AssistantCompletion> CompleteAsync(
        AssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        LastRequest = request;
        return Task.FromResult(new AssistantCompletion(
            "Soy un asistente de RR. HH./HCM y solo puedo ayudar con preguntas sobre el equipo.",
            []));
    }
}
