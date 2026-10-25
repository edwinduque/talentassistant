using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TalentAssistant.Infrastructure.Persistence;

namespace TalentAssistant.Infrastructure.Authentication;

/// <summary>
/// Seeds one synthetic local user, idempotently and only from external configuration.
/// Applies the identity migration first so the one-shot initializer works on an empty
/// database. The password is never stored in the repository and never overwritten
/// once the user exists. SPEC-016: the seed user is the demo administrator — the
/// seeder also ensures the "admin" role exists and is attached to that user.
/// </summary>
public sealed class InitializeLocalAuthenticationData(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    TalentAssistantIdentityDbContext identityDbContext,
    AuthenticationStoreOptions options)
{
    public const string AdminRoleName = "admin";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await identityDbContext.Database.MigrateAsync(cancellationToken);

        if (!await roleManager.RoleExistsAsync(AdminRoleName))
        {
            var adminRole = new IdentityRole(AdminRoleName);
            try
            {
                var roleResult = await roleManager.CreateAsync(adminRole);
                if (!roleResult.Succeeded)
                {
                    var descriptions = string.Join(
                        " ",
                        roleResult.Errors.Select(error => error.Description));
                    throw new InvalidOperationException(
                        $"The admin role could not be seeded: {descriptions}");
                }
            }
            // PostgreSQL creates the unique index in a committed transaction, so a
            // concurrent initializer can slip past RoleExistsAsync and lose the
            // race on "RoleNameIndex". Only swallow that failure when the role now
            // exists (another initializer seeded it); otherwise the original error
            // keeps its identity for the caller.
            catch (DbUpdateException exception)
            {
                // A failed insert stays tracked as "Added"; detach it or the next
                // SaveChanges (creating the seed user) replays the role INSERT and
                // fails again with the same duplicate key.
                identityDbContext.Entry(adminRole).State = EntityState.Detached;

                bool seededByAnotherInitializer;
                try
                {
                    seededByAnotherInitializer =
                        await roleManager.RoleExistsAsync(AdminRoleName);
                }
                catch (DbException)
                {
                    // The database is unavailable instead of racing: report that.
                    throw new InvalidOperationException(
                        "The admin role could not be seeded because the identification store is unavailable.",
                        exception);
                }

                if (!seededByAnotherInitializer)
                {
                    throw;
                }

                // Another initializer wins the race: the role already exists, which
                // is the intended end state.
            }
        }

        var existingUser = await userManager.FindByNameAsync(options.SeedUserName);
        if (existingUser is not null)
        {
            await EnsureSeedUserIsAdminAsync(existingUser);
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
            // Same tracker hazard as the role: drop the failed insert so a later
            // SaveChanges in this scope cannot replay it.
            identityDbContext.Entry(user).State = EntityState.Detached;

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
                    await EnsureSeedUserIsAdminAsync(
                        (await userManager.FindByNameAsync(options.SeedUserName))!);
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

        await EnsureSeedUserIsAdminAsync(user);
    }

    private async Task EnsureSeedUserIsAdminAsync(ApplicationUser user)
    {
        if (await userManager.IsInRoleAsync(user, AdminRoleName))
        {
            return;
        }

        var result = await userManager.AddToRoleAsync(user, AdminRoleName);
        if (!result.Succeeded)
        {
            var descriptions = string.Join(
                " ",
                result.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"The seed user could not be assigned the admin role: {descriptions}");
        }
    }
}
