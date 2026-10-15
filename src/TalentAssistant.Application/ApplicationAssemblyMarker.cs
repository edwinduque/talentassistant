namespace TalentAssistant.Application;

using TalentAssistant.Domain;

public static class ApplicationAssemblyMarker
{
    public static Type DomainAssembly => typeof(DomainAssemblyMarker);
}
