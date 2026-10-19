namespace TalentAssistant.Infrastructure.HcmPostgres;

/// <summary>
/// Command settings of the Postgres adapters, resolved once from configuration
/// (<c>HcmPostgres:CommandTimeoutSeconds</c>, default 15) and injected into
/// both adapters together with the shared <c>NpgsqlDataSource</c>.
/// </summary>
public sealed record HcmPostgresAdapterOptions(int CommandTimeoutSeconds)
{
    public static HcmPostgresAdapterOptions Default { get; } = new(15);
}
