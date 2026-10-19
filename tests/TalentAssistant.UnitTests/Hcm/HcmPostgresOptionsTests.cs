using TalentAssistant.Application.Hcm;

namespace TalentAssistant.UnitTests.Hcm;

/// <summary>
/// SPEC-011 S3 fail-fast configuration rules, kept as pure static logic so the
/// unit tests reach them directly: Postgres mode without a connection string is
/// a configuration error, an unknown source value must not silently fall back
/// to synthetic data, and Synthetic mode needs no connection string.
/// </summary>
public sealed class HcmPostgresOptionsTests
{
    [Fact]
    public void PostgresModeWithoutAConnectionStringThrows()
    {
        var nullConnectionString = Assert.Throws<InvalidOperationException>(
            () => HcmPostgresOptions.Validate("Postgres", null));
        Assert.Contains(
            "no connection string is configured",
            nullConnectionString.Message,
            StringComparison.Ordinal);

        var emptyConnectionString = Assert.Throws<InvalidOperationException>(
            () => HcmPostgresOptions.Validate("Postgres", "   "));
        Assert.Contains(
            "no connection string is configured",
            emptyConnectionString.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PostgresModeWithAConnectionStringIsAccepted()
    {
        HcmPostgresOptions.Validate("Postgres", "Host=127.0.0.1;Database=talentassistant_hcm");
        HcmPostgresOptions.Validate("postgres", "Host=127.0.0.1;Database=talentassistant_hcm");
    }

    [Fact]
    public void SyntheticModeNeedsNoConnectionString()
    {
        HcmPostgresOptions.Validate("Synthetic", null);
        HcmPostgresOptions.Validate("Synthetic", string.Empty);

        // SPEC-011 §3 names the value as "Sintética" too; both spellings select
        // the synthetic mode instead of failing.
        HcmPostgresOptions.Validate("Sintética", null);
        HcmPostgresOptions.Validate("Sintetica", null);
    }

    [Fact]
    public void AnUnknownSourceValueThrowsInsteadOfFallingBackToSynthetic()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => HcmPostgresOptions.Validate("postgresql", null));
        Assert.Contains("Synthetic", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Postgres", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceDetectionOnlyRecognizesPostgres()
    {
        Assert.True(HcmPostgresOptions.IsPostgres("Postgres"));
        Assert.True(HcmPostgresOptions.IsPostgres("POSTGRES"));
        Assert.False(HcmPostgresOptions.IsPostgres("Synthetic"));
        Assert.False(HcmPostgresOptions.IsPostgres("Sintética"));
        Assert.False(HcmPostgresOptions.IsPostgres(null));
        Assert.False(HcmPostgresOptions.IsPostgres("postgresql"));
    }
}
