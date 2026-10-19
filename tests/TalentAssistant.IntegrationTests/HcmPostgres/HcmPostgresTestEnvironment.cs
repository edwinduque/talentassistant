namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// Runner-provided configuration for the local contract database
/// (SPEC-011 S4). The connection string is read from the environment and never
/// printed, logged or written to disk (NFR-004): failure messages name the
/// environment variable only, never its value.
/// </summary>
internal static class HcmPostgresTestEnvironment
{
    internal const string ConnectionStringVariable = "HcmPostgres__ConnectionString";

    internal static string ConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Fail(
                $"Set {ConnectionStringVariable} to the local hcm-postgres application-role connection string. "
                + "is the hcm-postgres service running? is HcmPostgres__ConnectionString set?");
        }

        return connectionString!;
    }
}
