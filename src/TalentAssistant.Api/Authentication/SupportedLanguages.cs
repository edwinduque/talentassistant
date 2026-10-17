namespace TalentAssistant.Api.Authentication;

/// <summary>
/// Closed catalog of UI languages accepted by SPEC-006. Data from the HCM and
/// API problem details are intentionally not localized.
/// </summary>
public static class SupportedLanguages
{
    public const string Spanish = "es";
    public const string English = "en";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { Spanish, English };

    public static bool IsSupported(string? language) =>
        language is not null && All.Contains(language);
}
