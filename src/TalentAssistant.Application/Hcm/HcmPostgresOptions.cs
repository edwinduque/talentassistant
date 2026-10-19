namespace TalentAssistant.Application.Hcm;

/// <summary>
/// Pure configuration rules of the single HCM source switch (SPEC-011 S3):
/// one value governs both ports, an invalid combination fails fast at startup,
/// and an unknown value never falls back silently to synthetic data. The logic
/// lives outside any DI closure so the unit tests reach it directly.
/// </summary>
public static class HcmPostgresOptions
{
    /// <summary>Default mode: today's synthetic fixtures (no behavior change).</summary>
    public const string SyntheticSource = "Synthetic";

    /// <summary>Read-only PostgreSQL connector behind both ports (SPEC-011 S2).</summary>
    public const string PostgresSource = "Postgres";

    // SPEC-011 §3 documents the value as "Sintética"; both spellings select the
    // synthetic mode so the documented value never fails at startup.
    private static readonly string[] SyntheticAliases =
        ["Synthetic", "Sintética", "Sintetica"];

    /// <summary>True only for the exact Postgres switch value (any casing).</summary>
    public static bool IsPostgres(string? source) =>
        string.Equals(source?.Trim(), PostgresSource, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for a missing value (the appsettings default) and for every
    /// documented synthetic spelling.
    /// </summary>
    public static bool IsSynthetic(string? source) =>
        string.IsNullOrWhiteSpace(source)
        || SyntheticAliases.Any(alias => string.Equals(
            source.Trim(),
            alias,
            StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Startup validation: Postgres mode requires a connection string and any
    /// unrecognized source value is a configuration error - a typo must not
    /// serve synthetic data as if it were the real HCM (SPEC-011 S3).
    /// </summary>
    public static void Validate(string? source, string? connectionString)
    {
        if (IsPostgres(source))
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Hcm:Source is configured as Postgres but no connection string is configured. "
                    + "Set HcmPostgres:ConnectionString (environment variable HcmPostgres__ConnectionString) "
                    + "before starting in Postgres mode.");
            }

            return;
        }

        if (!IsSynthetic(source))
        {
            throw new InvalidOperationException(
                $"Hcm:Source must be '{SyntheticSource}' or '{PostgresSource}'; received '{source}'.");
        }
    }
}
