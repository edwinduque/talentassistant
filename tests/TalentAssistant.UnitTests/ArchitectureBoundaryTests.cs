using System.Reflection;
using TalentAssistant.Application;
using TalentAssistant.Domain;

namespace TalentAssistant.UnitTests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void DomainAndApplicationRespectTheirDependencyBoundaries()
    {
        var domain = typeof(DomainAssemblyMarker).Assembly;
        var application = typeof(ApplicationAssemblyMarker).Assembly;

        AssertNoForbiddenReferences(domain);
        Assert.Contains(
            application.GetReferencedAssemblies(),
            reference => reference.Name == domain.GetName().Name);
        AssertNoForbiddenReferences(application);
    }

    private static void AssertNoForbiddenReferences(Assembly assembly)
    {
        var forbiddenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TalentAssistant.Infrastructure",
            "TalentAssistant.Api",
            "TalentAssistant.Web",
            "Microsoft.EntityFrameworkCore",
            "MySql.EntityFrameworkCore",
            "MySql.Data"
        };

        var violations = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && forbiddenNames.Contains(name))
            .ToArray();

        Assert.Empty(violations);
    }
}
