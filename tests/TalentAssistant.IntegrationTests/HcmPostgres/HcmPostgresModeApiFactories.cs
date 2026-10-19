using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using TalentAssistant.Infrastructure.Authentication;
using TalentAssistant.IntegrationTests.Http;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// Hosts the real API pipeline with <c>Hcm:Source=Postgres</c> for the
/// SPEC-011 S3 mode tests (single switch, no mixed mode). Every subclass
/// differs only in the value it forces for <c>HcmPostgres:ConnectionString</c>:
/// <list type="bullet">
/// <item><see cref="PostgresModeApiFactory"/>: the local contract database from
/// the runner environment (mode and contract proofs).</item>
/// <item><see cref="DegradedPostgresModeApiFactory"/>: a closed loopback port
/// (database down; AC-038 degradation proof, no external network).</item>
/// <item><see cref="PostgresModeWithoutConnectionStringFactory"/>: an explicit
/// empty value (fail-fast validation proof).</item>
/// </list>
/// Overrides ride both configuration entry points of the deferred host
/// (in-memory sources and host settings), the same dual-injection trick as
/// <see cref="ChatContractApiFactory"/>, so even an explicit empty value wins
/// over the runner's <c>HcmPostgres__*</c> process variable. No secret is ever
/// printed: the environment variable is read, never echoed.
/// </summary>
public abstract class HcmPostgresModeApiFactoryBase : WebApplicationFactory<Program>
{
    /// <summary>
    /// Closed loopback port only: the degradation proof must not depend on any
    /// network beyond this machine (SPEC-011 S4). Npgsql names the connect
    /// timeout "Timeout" (the Npgsql 10 builder rejects "Connect Timeout").
    /// </summary>
    public const string UnreachableConnectionString = "Host=127.0.0.1;Port=59999;Timeout=2";

    // Synthetic value; never a real credential (NFR-004).
    private const string SigningKey = "hcm-postgres-factory-signing-key-0123456789abcdef";
    private const string DefaultAiModel = "openai/gpt-oss-120b";
    private const string DefaultAiBaseUrl = "https://api.groq.com/openai/v1";

    private readonly string _hcmPostgresConnectionString;

    protected HcmPostgresModeApiFactoryBase(string hcmPostgresConnectionString)
    {
        _hcmPostgresConnectionString = hcmPostgresConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(Overrides());
        });

        foreach (var (key, value) in Overrides())
        {
            builder.UseSetting(key, value);
        }
    }

    private Dictionary<string, string?> Overrides() =>
        new()
        {
            ["ConnectionStrings:TalentAssistant"] = TestsConnectionString(),
            ["Hcm:Source"] = "Postgres",
            ["HcmPostgres:ConnectionString"] = _hcmPostgresConnectionString,
            ["Authentication:SigningKey"] = SigningKey,
            ["Authentication:SeedUserName"] = ChatContractApiFactory.SeedUserName,
            ["Authentication:SeedPassword"] = ChatContractApiFactory.SeedPassword,
            ["AI:ApiKey"] = string.Empty,
            ["AI:Model"] = DefaultAiModel,
            ["AI:BaseUrl"] = DefaultAiBaseUrl
        };

    private static string TestsConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__TalentAssistantTests");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__TalentAssistantTests to the dedicated talentassistant_tests database.");
        }

        return connectionString;
    }
}

/// <summary>Postgres mode against the seeded local contract database.</summary>
public sealed class PostgresModeApiFactory : HcmPostgresModeApiFactoryBase
{
    public PostgresModeApiFactory()
        : base(HcmPostgresTestEnvironment.ConnectionString())
    {
    }
}

/// <summary>Postgres mode with the database down (closed loopback port).</summary>
public sealed class DegradedPostgresModeApiFactory : HcmPostgresModeApiFactoryBase
{
    public DegradedPostgresModeApiFactory()
        : base(UnreachableConnectionString)
    {
    }
}

/// <summary>Postgres mode without any connection string: must fail fast.</summary>
public sealed class PostgresModeWithoutConnectionStringFactory : HcmPostgresModeApiFactoryBase
{
    public PostgresModeWithoutConnectionStringFactory()
        : base(string.Empty)
    {
    }
}
