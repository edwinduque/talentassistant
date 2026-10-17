using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalentAssistant.Infrastructure.Authentication;
using TalentAssistant.Infrastructure.Persistence;

namespace TalentAssistant.IntegrationTests.Authentication;

public sealed class AuthenticationPersistenceTests
{
    private const string Password = "Integration!Pass123";

    private static string ConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__TalentAssistantTests");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Set ConnectionStrings__TalentAssistantTests to the dedicated talentassistant_tests database.");
        return connectionString!;
    }

    private static async Task<ServiceProvider> BuildProviderAsync(
        string connectionString,
        int lockoutMaxFailedAccessAttempts = 2)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TalentAssistantIdentityDbContext>(options =>
            options.UseMySQL(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole>(identity =>
            {
                identity.Lockout.MaxFailedAccessAttempts = lockoutMaxFailedAccessAttempts;
                identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<TalentAssistantIdentityDbContext>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<TalentAssistantIdentityDbContext>();
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        return provider;
    }

    [Fact]
    public async Task AppliesMigrationsAndStoresOnlyHashedPasswords()
    {
        await using var provider = await BuildProviderAsync(ConnectionString());
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider
            .GetRequiredService<TalentAssistantIdentityDbContext>();
        var userName = $"integration-auth-{Guid.NewGuid():N}";

        try
        {
            var user = new ApplicationUser
            {
                UserName = userName,
                Email = userName,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, Password);
            Assert.True(
                createResult.Succeeded,
                string.Join(" ", createResult.Errors.Select(error => error.Description)));

            var stored = await context.Users
                .AsNoTracking()
                .SingleAsync(candidate => candidate.UserName == userName);

            Assert.False(string.IsNullOrWhiteSpace(stored.PasswordHash));
            Assert.NotEqual(Password, stored.PasswordHash);
            Assert.DoesNotContain(Password, stored.PasswordHash);
            Assert.True(await userManager.CheckPasswordAsync(stored, Password));
            Assert.False(await userManager.CheckPasswordAsync(stored, "Wrong!Pass123"));
        }
        finally
        {
            var existing = await userManager.FindByNameAsync(userName);
            if (existing is not null)
            {
                await userManager.DeleteAsync(existing);
            }
        }
    }

    [Fact]
    public async Task LocksOutAfterConfiguredFailedAttempts()
    {
        await using var provider = await BuildProviderAsync(
            ConnectionString(),
            lockoutMaxFailedAccessAttempts: 2);
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = $"integration-lockout-{Guid.NewGuid():N}";

        try
        {
            var user = new ApplicationUser
            {
                UserName = userName,
                Email = userName,
                EmailConfirmed = true
            };
            Assert.True((await userManager.CreateAsync(user, Password)).Succeeded);

            await userManager.AccessFailedAsync(user);
            Assert.False(await userManager.IsLockedOutAsync(user));

            await userManager.AccessFailedAsync(user);
            var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
            Assert.NotNull(lockoutEnd);
            Assert.True(lockoutEnd > DateTimeOffset.UtcNow);
            Assert.True(await userManager.IsLockedOutAsync(user));
        }
        finally
        {
            var existing = await userManager.FindByNameAsync(userName);
            if (existing is not null)
            {
                await userManager.DeleteAsync(existing);
            }
        }
    }

    [Fact]
    public async Task SeedInitializer_IsIdempotentAndNeverOverwritesThePassword()
    {
        await using var provider = await BuildProviderAsync(ConnectionString());
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider
            .GetRequiredService<TalentAssistantIdentityDbContext>();
        var seedUserName = $"integration-seed-{Guid.NewGuid():N}";
        var options = new AuthenticationStoreOptions(
            5,
            TimeSpan.FromMinutes(5),
            seedUserName,
            Password);

        try
        {
            await new InitializeLocalAuthenticationData(userManager, context, options)
                .InitializeAsync();

            var changedPasswordOptions = options with { SeedPassword = "Changed!Pass123" };
            await new InitializeLocalAuthenticationData(userManager, context, changedPasswordOptions)
                .InitializeAsync();

            var user = await userManager.FindByNameAsync(seedUserName);
            Assert.NotNull(user);
            Assert.True(await userManager.CheckPasswordAsync(user!, Password));
            Assert.False(await userManager.CheckPasswordAsync(user!, "Changed!Pass123"));
        }
        finally
        {
            var existing = await userManager.FindByNameAsync(seedUserName);
            if (existing is not null)
            {
                await userManager.DeleteAsync(existing);
            }
        }
    }

    [Fact]
    public async Task SeedInitializer_WithoutPasswordOrExistingUser_ThrowsControlledError()
    {
        await using var provider = await BuildProviderAsync(ConnectionString());
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider
            .GetRequiredService<TalentAssistantIdentityDbContext>();
        var options = new AuthenticationStoreOptions(
            5,
            TimeSpan.FromMinutes(5),
            $"integration-missing-{Guid.NewGuid():N}",
            SeedPassword: " ");

        var initializer = new InitializeLocalAuthenticationData(userManager, context, options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
    }
}
