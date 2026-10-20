using Npgsql;

namespace TalentAssistant.Infrastructure.HcmPostgres;

/// <summary>
/// SPEC-011 AC-037, reforzado por el hallazgo SEC-001 de la revisión de
/// seguridad de 2026-10-19: el transporte hacia la base del HCM debe ir cifrado
/// y con validación de certificados para cualquier host no-loopback. Los hosts
/// de bucle quedan exentos porque la base local de pruebas del contrato corre
/// sin TLS por diseño (excepción solo-local documentada en la guía); la base
/// real exige Ssl Mode=VerifyFull y cualquier valor más débil aborta el
/// arranque, con la misma filosofía que el resto de validaciones fail-fast.
/// </summary>
public static class HcmPostgresTls
{
    private static readonly string[] LoopbackHosts =
        ["localhost", "127.0.0.1", "::1", "0:0:0:0:0:0:0:1"];

    public static void Validate(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        var rawHost = builder.Host;
        if (string.IsNullOrWhiteSpace(rawHost))
        {
            throw new InvalidOperationException(
                "HcmPostgres:ConnectionString does not define a host. "
                + "Set Host (and Ssl Mode=VerifyFull for non-loopback servers).");
        }

        // Npgsql keeps IPv6 hosts bracketed ("[::1]"); normalize before compare.
        var host = rawHost.TrimStart('[').TrimEnd(']');

        if (LoopbackHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (builder.SslMode != SslMode.VerifyFull)
        {
            throw new InvalidOperationException(
                $"HcmPostgres:ConnectionString targets the non-loopback host '{rawHost}' "
                + $"but its Ssl Mode is '{builder.SslMode}'. Set Ssl Mode=VerifyFull "
                + "(encryption with certificate validation) before using the real HCM database.");
        }
    }
}
