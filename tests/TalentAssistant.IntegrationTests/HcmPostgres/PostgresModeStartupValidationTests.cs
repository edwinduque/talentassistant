namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// SPEC-011 S3 fail-fast proof: booting with <c>Hcm:Source=Postgres</c> and no
/// connection string must abort startup with a clear configuration error
/// instead of silently degrading to synthetic data. The factory forces an
/// explicit empty <c>HcmPostgres:ConnectionString</c>, which also overrides the
/// runner's process variable, so the configuration path under test is exactly
/// the empty one.
/// </summary>
public sealed class PostgresModeStartupValidationTests
{
    [Fact]
    public void PostgresModeWithoutAConnectionStringFailsFastAtStartup()
    {
        using var factory = new PostgresModeWithoutConnectionStringFactory();

        var exception = Record.Exception(() => { factory.CreateClient(); });

        Assert.NotNull(exception);
        Assert.Contains(
            "no connection string is configured",
            exception.ToString(),
            StringComparison.Ordinal);
    }
}
