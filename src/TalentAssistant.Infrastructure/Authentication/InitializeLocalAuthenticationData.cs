using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TalentAssistant.Infrastructure.Persistence;

namespace TalentAssistant.Infrastructure.Authentication;

/// <summary>
/// Seeds one synthetic local user, idempotently and only from external configuration.
/// Applies the identity migration first so the one-shot initializer works on an empty
/// database. The password is never stored in the repository and never overwritten
/// once the user exists.
/// </summary>
public sealed class InitializeLocalAuthenticationData(
    UserManager<IdentityUser> userManager,
    TalentAssistantIdentityDbContext identityDbContext,
    AuthenticationStoreOptions options)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await identityDbContext.Database.MigrateAsync(cancellationToken);

        var existingUser = await userManager.FindByNameAsync(options.SeedUserName);
        if (existingUser is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.SeedPassword))
        {
            throw new InvalidOperationException(
                "Configure Authentication:SeedPassword to seed the local user; no default password exists.");
        }

        var user = new IdentityUser
        {
            UserName = options.SeedUserName,
            Email = options.SeedUserName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, options.SeedPassword);
        if (!result.Succeeded)
        {
            var descriptions = string.Join(
                " ",
                result.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"The local user could not be seeded: {descriptions}");
        }
    }
}
