namespace TalentAssistant.Web.Services;

/// <summary>
/// Closed catalog of UI cultures the Web app can render. This mirrors the API's
/// <c>SupportedLanguages</c> catalog, which lives in the API project and cannot be
/// referenced from Web without breaking the client/API separation (ADR-0003).
/// </summary>
public static class SessionCulture
{
    public const string Default = "es";

    public static readonly IReadOnlyList<string> Supported = ["es", "en"];

    public static bool IsSupported(string? culture) =>
        !string.IsNullOrWhiteSpace(culture)
        && Supported.Contains(culture, StringComparer.OrdinalIgnoreCase);
}
