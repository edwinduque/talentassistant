using System.IO;

namespace TalentAssistant.UnitTests.Architecture;

/// <summary>
/// SPEC-013 S1/S6/S3: after the PostgreSQL-only migration no MySQL package,
/// service or provider annotation may reappear in the reproducible surface
/// (project files, Compose, Docker assets, EF migrations).
/// </summary>
public sealed class PostgresOnlyAcceptanceTests
{
    [Fact]
    public void NoMysqlReferencesInProjectsComposeOrDockerAssets()
    {
        var root = FindRepoRoot(AppContext.BaseDirectory);
        Assert.All(
            Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories),
            f => Assert.DoesNotContain("mysql", File.ReadAllText(f), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("mysql", File.ReadAllText(Path.Combine(root, "compose.yaml")), StringComparison.OrdinalIgnoreCase);
        Assert.All(
            Directory.GetFiles(Path.Combine(root, "docker"), "*", SearchOption.AllDirectories),
            f => Assert.DoesNotContain("mysql", File.ReadAllText(f), StringComparison.OrdinalIgnoreCase));
        Assert.All(
            Directory.GetFiles(Path.Combine(root, "src", "TalentAssistant.Infrastructure", "Migrations"), "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.Combine(root, "tests", "TalentAssistant.IntegrationTests", "Migrations"), "*.cs")),
            f => Assert.DoesNotContain("MySQL:", File.ReadAllText(f), StringComparison.Ordinal));
    }

    private static string FindRepoRoot(string start) =>
        Directory.GetFiles(start, "compose.yaml").Length > 0
            ? start
            : FindRepoRoot(Directory.GetParent(start)!.FullName);
}
