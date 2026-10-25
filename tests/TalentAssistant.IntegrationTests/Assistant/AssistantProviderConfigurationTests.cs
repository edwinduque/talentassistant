using Microsoft.Extensions.Configuration;
using TalentAssistant.Infrastructure.Assistant;

namespace TalentAssistant.IntegrationTests.Assistant;

/// <summary>
/// SPEC-022 S9/S11: configuration parsing is deterministic and fail-fast only
/// for contradictory values; an empty configuration still boots and lets the
/// assistant degrade with NotConfigured.
/// </summary>
public sealed class AssistantProviderConfigurationTests
{
    [Fact]
    public void DefaultsUseRouterWithFiftyFiftyWeights()
    {
        var configuration = Load();

        Assert.Equal(AssistantRoutingMode.Router, configuration.Mode);
        Assert.False(configuration.Groq.IsConfigured);
        Assert.False(configuration.Gemini.IsConfigured);
        Assert.Equal(AssistantProviderConfiguration.GeminiDefaultBaseUrl, configuration.Gemini.BaseUrl);
        Assert.Equal(AssistantProviderConfiguration.GeminiDefaultModel, configuration.Gemini.Model);
        Assert.Equal("low", configuration.Gemini.ReasoningEffort);
        Assert.True(configuration.Gemini.NormalizeToolSchemas);
        Assert.Equal(50, configuration.Routing.GroqWeight);
        Assert.Equal(50, configuration.Routing.GeminiWeight);
        Assert.Equal(2, configuration.Routing.FailureThreshold);
        Assert.Equal(120, configuration.Routing.CooldownSeconds);
        Assert.Empty(configuration.EnabledProviders());
    }

    [Fact]
    public void LegacyGroqKeysStayValidAndSingleProviderKeepsTwoAttempts()
    {
        var configuration = Load(new()
        {
            ["ApiKey"] = "groq-test-key",
            ["Model"] = "openai/gpt-oss-120b"
        });

        Assert.True(configuration.Groq.IsConfigured);
        Assert.False(configuration.Gemini.IsConfigured);
        Assert.Equal("groq-test-key", configuration.Groq.ApiKey);
        Assert.Equal(2, configuration.Groq.MaxAttempts);
        var provider = Assert.Single(configuration.EnabledProviders());
        Assert.Equal("groq", provider.Name);
    }

    [Fact]
    public void BothProvidersConfiguredUseRouterWithOneAttemptPerProvider()
    {
        var configuration = Load(new()
        {
            ["ApiKey"] = "groq-test-key",
            ["Model"] = "openai/gpt-oss-120b",
            ["Gemini:ApiKey"] = "gemini-test-key"
        });

        Assert.Equal(AssistantRoutingMode.Router, configuration.Mode);
        Assert.Equal(2, configuration.EnabledProviders().Count);
        Assert.Equal(1, configuration.Groq.MaxAttempts);
        Assert.Equal(1, configuration.Gemini.MaxAttempts);
    }

    [Fact]
    public void ForcedProviderModeLimitsTheCatalog()
    {
        var configuration = Load(new()
        {
            ["Provider"] = "gemini",
            ["ApiKey"] = "groq-test-key",
            ["Model"] = "openai/gpt-oss-120b",
            ["Gemini:ApiKey"] = "gemini-test-key"
        });

        Assert.Equal(AssistantRoutingMode.Gemini, configuration.Mode);
        var provider = Assert.Single(configuration.EnabledProviders());
        Assert.Equal("gemini", provider.Name);
        Assert.Equal(2, provider.MaxAttempts);
    }

    [Fact]
    public void UnknownModeFailsFast()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Load(new() { ["Provider"] = "openai" }));

        Assert.Contains("AI:Provider", exception.Message);
    }

    [Fact]
    public void NegativeWeightFailsFast()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Load(new() { ["Routing:GroqWeight"] = "-1" }));
    }

    [Fact]
    public void BothWeightsZeroWithBothProvidersFailsFast()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Load(new()
            {
                ["ApiKey"] = "groq-test-key",
                ["Model"] = "openai/gpt-oss-120b",
                ["Gemini:ApiKey"] = "gemini-test-key",
                ["Routing:GroqWeight"] = "0",
                ["Routing:GeminiWeight"] = "0"
            }));

        Assert.Contains("greater than zero", exception.Message);
    }

    [Fact]
    public void InvalidGeminiReasoningEffortFailsFast()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Load(new() { ["Gemini:ReasoningEffort"] = "maximum" }));
    }

    private static AssistantProviderConfiguration Load(
        Dictionary<string, string?>? values = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? [])
            .Build();
        return AssistantProviderConfiguration.Load(configuration);
    }
}
