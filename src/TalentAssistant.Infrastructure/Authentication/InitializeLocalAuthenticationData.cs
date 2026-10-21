using System.Data.Common;
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
    UserManager<ApplicationUser> userManager,
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

        var user = new ApplicationUser
        {
            UserName = options.SeedUserName,
            Email = options.SeedUserName,
            EmailConfirmed = true,
            FullName = options.SeedUserName,
            PreferredLanguage = ApplicationUser.DefaultPreferredLanguage
        };

        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, options.SeedPassword);
        }
        // PostgreSQL creates the unique index in a committed transaction, so a
        // concurrent initializer can slip past FindByNameAsync and lose the
        // race on "UserNameIndex". Only swallow that failure when the user now
        // exists (another initializer seeded it); otherwise the original error
        // keeps its identity for the caller.
        catch (DbUpdateException exception)
        {
            bool seededByAnotherInitializer;
            try
            {
                seededByAnotherInitializer =
                    await userManager.FindByNameAsync(options.SeedUserName) is not null;
            }
            catch (DbException)
            {
                // The database is unavailable instead of racing: report that.
                throw new InvalidOperationException(
                    "The local user could not be seeded because the identification store is unavailable.",
                    exception);
            }

            if (!seededByAnotherInitializer)
            {
                throw;
            }

            // Another initializer wins the race: for this one the seed is
            // already done.
            return;
        }

        if (!result.Succeeded)
        {
            try
            {
                if (await userManager.FindByNameAsync(options.SeedUserName) is not null)
                {
                    // The user materialized between the first find and the
                    // create (concurrent bootstrap): the seed already exists,
                    // and the seeded password is never overwritten.
                    return;
                }
            }
            catch (DbException)
            {
                // Unavailability must surface from the original error below.
            }

            var descriptions = string.Join(
                " ",
                result.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"The local user could not be seeded: {descriptions}");
        }
    }
}
