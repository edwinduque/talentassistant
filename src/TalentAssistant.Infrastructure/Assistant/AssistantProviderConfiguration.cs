using Microsoft.Extensions.Configuration;

namespace TalentAssistant.Infrastructure.Assistant;

/// <summary>
/// Routing mode selected by <c>AI:Provider</c> (SPEC-022 S9). Router balances
/// between every configured provider; Groq and Gemini force a single provider
/// for diagnostics.
/// </summary>
public enum AssistantRoutingMode
{
    Router,
    Groq,
    Gemini
}

/// <summary>
/// One provider configuration. Groq keeps the original AI:BaseUrl/ApiKey/Model
/// keys so existing deployments keep working; Gemini reads AI:Gemini:*.
/// </summary>
public sealed record AssistantProviderSettings(
    string Name,
    string BaseUrl,
    string ApiKey,
    string Model,
    string? ReasoningEffort,
    int TimeoutSeconds,
    int MaxAttempts,
    bool NormalizeToolSchemas,
    double? Temperature = 0.1)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public AssistantProviderOptions ToClientOptions() =>
        new(ApiKey, Model, Name, ReasoningEffort, MaxAttempts, NormalizeToolSchemas, Temperature);
}

/// <summary>
/// Weighted, sticky routing configuration (SPEC-022 §3.2, §3.4).
/// </summary>
public sealed record AssistantRoutingOptions(
    int GroqWeight,
    int GeminiWeight,
    int FailureThreshold,
    int CooldownSeconds)
{
    public int WeightFor(string providerName) =>
        string.Equals(providerName, "gemini", StringComparison.OrdinalIgnoreCase)
            ? GeminiWeight
            : GroqWeight;
}

/// <summary>
/// Parsed AI configuration. Validation fails fast on contradictory weights or
/// an unknown mode, but an empty configuration is valid: the API boots and the
/// assistant degrades with NotConfigured (SPEC-009 §8.6, SPEC-022 S9).
/// </summary>
public sealed record AssistantProviderConfiguration(
    AssistantRoutingMode Mode,
    AssistantProviderSettings Groq,
    AssistantProviderSettings Gemini,
    AssistantRoutingOptions Routing)
{
    public const string GroqDefaultBaseUrl = "https://api.groq.com/openai/v1";
    public const string GeminiDefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
    public const string GeminiDefaultModel = "gemini-3.8-flash";

    private static readonly string[] AllowedReasoningEfforts =
        ["none", "minimal", "low", "medium", "high"];

    public static AssistantProviderConfiguration Load(IConfiguration ai)
    {
        ArgumentNullException.ThrowIfNull(ai);

        var mode = ParseMode(ai["Provider"]);
        var groq = new AssistantProviderSettings(
            Name: "groq",
            BaseUrl: NormalizeBaseUrl(ai["BaseUrl"], GroqDefaultBaseUrl, "AI:BaseUrl"),
            ApiKey: ai["ApiKey"]?.Trim() ?? string.Empty,
            Model: (ai["Model"] ?? string.Empty).Trim(),
            ReasoningEffort: null,
            TimeoutSeconds: ReadTimeout(ai, "TimeoutSeconds"),
            MaxAttempts: 2,
            NormalizeToolSchemas: false,
            Temperature: 0.1);
        var gemini = new AssistantProviderSettings(
            Name: "gemini",
            BaseUrl: NormalizeBaseUrl(ai["Gemini:BaseUrl"], GeminiDefaultBaseUrl, "AI:Gemini:BaseUrl"),
            ApiKey: ai["Gemini:ApiKey"]?.Trim() ?? string.Empty,
            Model: string.IsNullOrWhiteSpace(ai["Gemini:Model"])
                ? GeminiDefaultModel
                : ai["Gemini:Model"]!.Trim(),
            ReasoningEffort: ParseReasoningEffort(ai["Gemini:ReasoningEffort"]),
            TimeoutSeconds: ReadTimeout(ai, "Gemini:TimeoutSeconds"),
            MaxAttempts: 2,
            NormalizeToolSchemas: true,
            Temperature: null);

        var routing = new AssistantRoutingOptions(
            GroqWeight: ReadWeight(ai, "Routing:GroqWeight"),
            GeminiWeight: ReadWeight(ai, "Routing:GeminiWeight"),
            FailureThreshold: ai.GetValue<int?>("Routing:FailureThreshold") ?? 2,
            CooldownSeconds: ai.GetValue<int?>("Routing:CooldownSeconds") ?? 120);

        if (routing.FailureThreshold < 1)
        {
            throw new InvalidOperationException(
                "AI:Routing:FailureThreshold must be at least 1.");
        }

        if (routing.CooldownSeconds < 0)
        {
            throw new InvalidOperationException(
                "AI:Routing:CooldownSeconds cannot be negative.");
        }

        if (mode == AssistantRoutingMode.Router
            && groq.IsConfigured
            && gemini.IsConfigured
            && routing.GroqWeight + routing.GeminiWeight == 0)
        {
            throw new InvalidOperationException(
                "At least one of AI:Routing:GroqWeight or AI:Routing:GeminiWeight must be greater than zero when both providers are configured.");
        }

        if (groq.IsConfigured && groq.Model.Length == 0)
        {
            throw new InvalidOperationException(
                "AI:Model is required when AI:ApiKey is configured.");
        }

        if (gemini.IsConfigured && gemini.Model.Length == 0)
        {
            throw new InvalidOperationException(
                "AI:Gemini:Model is required when AI:Gemini:ApiKey is configured.");
        }

        // Router mode spends at most one HTTP attempt per provider so a
        // transient failure moves to the other provider instead of burning
        // the same quota twice (SPEC-022 §3.4). Single-provider mode keeps the
        // existing retry for 5xx and tool_use_failed.
        var maxAttempts = mode == AssistantRoutingMode.Router
            && groq.IsConfigured
            && gemini.IsConfigured
                ? 1
                : 2;

        return new AssistantProviderConfiguration(
            mode,
            groq with { MaxAttempts = maxAttempts },
            gemini with { MaxAttempts = maxAttempts },
            routing);
    }

    public IReadOnlyList<AssistantProviderSettings> EnabledProviders() =>
        Mode switch
        {
            AssistantRoutingMode.Groq => Groq.IsConfigured ? [Groq] : [],
            AssistantRoutingMode.Gemini => Gemini.IsConfigured ? [Gemini] : [],
            _ => new[] { Groq, Gemini }.Where(provider => provider.IsConfigured).ToArray()
        };

    private static AssistantRoutingMode ParseMode(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "router" => AssistantRoutingMode.Router,
            "groq" => AssistantRoutingMode.Groq,
            "gemini" => AssistantRoutingMode.Gemini,
            _ => throw new InvalidOperationException(
                "AI:Provider must be 'router', 'groq' or 'gemini'.")
        };

    private static string NormalizeBaseUrl(string? value, string fallback, string key)
    {
        var baseUrl = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{key} must be an absolute HTTP or HTTPS URL.");
        }

        return baseUrl;
    }

    private static int ReadTimeout(IConfiguration ai, string key)
    {
        var timeout = ai.GetValue<int?>(key) ?? 30;
        if (timeout <= 0)
        {
            throw new InvalidOperationException($"AI:{key} must be greater than zero.");
        }

        return timeout;
    }

    private static int ReadWeight(IConfiguration ai, string key)
    {
        var weight = ai.GetValue<int?>(key) ?? 50;
        if (weight < 0)
        {
            throw new InvalidOperationException($"AI:{key} cannot be negative.");
        }

        return weight;
    }

    private static string ParseReasoningEffort(string? value)
    {
        var effort = string.IsNullOrWhiteSpace(value) ? "low" : value.Trim().ToLowerInvariant();
        if (!AllowedReasoningEfforts.Contains(effort, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "AI:Gemini:ReasoningEffort must be one of none, minimal, low, medium or high.");
        }

        return effort;
    }
}
