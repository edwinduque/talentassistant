namespace TalentAssistant.Infrastructure.Authentication;

public sealed record AuthenticationStoreOptions(
    int LockoutMaxFailedAccessAttempts,
    TimeSpan LockoutTime,
    string SeedUserName,
    string? SeedPassword);
