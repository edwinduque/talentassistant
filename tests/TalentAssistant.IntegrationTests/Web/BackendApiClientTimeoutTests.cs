extern alias Web;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using LoginLayout = Web::TalentAssistant.Web.Components.Layout.LoginLayout;

namespace TalentAssistant.IntegrationTests.Web;

/// <summary>
/// SPEC-022: the chat endpoint runs the provider tool loop synchronously, so
/// the Web client must wait longer than the short 5 s used by non-chat calls.
/// </summary>
public sealed class BackendApiClientTimeoutTests
{
    [Fact]
    public void ChatClientAllowsTheProviderLoopToFinish()
    {
        using var factory = new WebApplicationFactory<LoginLayout>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("BackendApi:BaseUrl", "http://127.0.0.1"));
        var httpClientFactory = factory.Services.GetRequiredService<IHttpClientFactory>();

        var chatClient = httpClientFactory.CreateClient(
            "TalentAssistant.Web.Services.BackendApiClient");

        Assert.True(
            chatClient.Timeout >= TimeSpan.FromSeconds(100),
            $"The chat client timeout was {chatClient.Timeout}; it must cover the provider loop.");
    }
}
