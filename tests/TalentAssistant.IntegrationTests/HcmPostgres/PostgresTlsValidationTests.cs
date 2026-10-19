using TalentAssistant.Infrastructure.HcmPostgres;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// SPEC-011 AC-037 hardened by security-review finding SEC-001: transport to a
/// non-loopback HCM database must be Ssl Mode=VerifyFull or startup fails;
/// loopback hosts are exempt because the local contract database runs without
/// TLS by design. Pure validation, no database contact.
/// </summary>
public sealed class PostgresTlsValidationTests
{
    // Synthetic values only; never real credentials (NFR-004).
    private const string NonLoopbackHost = "Host=hcm.example.com;Port=5432;Database=talentassistant_hcm;Username=talentassistant_hcm_app;Password=test-only-synthetic";

    [Fact]
    public void NonLoopbackHostWithoutSslModeFailsFast()
    {
        var exception = Record.Exception(() => HcmPostgresTls.Validate(NonLoopbackHost));

        Assert.NotNull(exception);
        Assert.Contains(
            "Ssl Mode=VerifyFull",
            exception!.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NonLoopbackHostWithWeakerSslModeFailsFast()
    {
        var exception = Record.Exception(
            () => HcmPostgresTls.Validate(NonLoopbackHost + ";Ssl Mode=Require"));

        Assert.NotNull(exception);
        Assert.Contains(
            "Ssl Mode=VerifyFull",
            exception!.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NonLoopbackHostWithVerifyFullPasses()
    {
        var exception = Record.Exception(
            () => HcmPostgresTls.Validate(NonLoopbackHost + ";Ssl Mode=VerifyFull"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Host=127.0.0.1;Port=5433;Database=talentassistant_hcm;Username=talentassistant_hcm_app;Password=test-only-synthetic")]
    [InlineData("Host=localhost;Port=5433;Database=talentassistant_hcm;Username=talentassistant_hcm_app;Password=test-only-synthetic")]
    [InlineData("Host=[::1];Port=5433;Database=talentassistant_hcm;Username=talentassistant_hcm_app;Password=test-only-synthetic")]
    public void LoopbackHostsStayExemptForTheLocalTestDatabase(string connectionString)
    {
        var exception = Record.Exception(() => HcmPostgresTls.Validate(connectionString));

        Assert.Null(exception);
    }
}
