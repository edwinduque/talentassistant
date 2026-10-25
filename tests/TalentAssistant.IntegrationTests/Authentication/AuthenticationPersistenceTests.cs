using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
            options.UseNpgsql(connectionString));
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
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await new InitializeLocalAuthenticationData(userManager, roleManager, context, options)
                .InitializeAsync();

            var changedPasswordOptions = options with { SeedPassword = "Changed!Pass123" };
            await new InitializeLocalAuthenticationData(userManager, roleManager, context, changedPasswordOptions)
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

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var initializer = new InitializeLocalAuthenticationData(userManager, roleManager, context, options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
    }

    [Fact]
    public async Task SeedInitializer_WhenRoleCreationLosesRaceToExistingRole_ThrowsControlledErrorNotDbUpdate()
    {
        // SPEC-016 hardening: a concurrent initializer can slip past RoleExistsAsync
        // and lose the race on "RoleNameIndex" (PostgreSQL unique index committed in a
        // separate transaction). The loser must not surface a raw DbUpdateException;
        // it must swallow that failure once the role is observed to exist and keep
        // seeding. A blank password stops the flow right after the role block, so the
        // controlled InvalidOperationException from the password guard proves the role
        // race was swallowed instead of escaping.
        await using var provider = await BuildProviderAsync(ConnectionString());
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider
            .GetRequiredService<TalentAssistantIdentityDbContext>();
        var realRoleStore = scope.ServiceProvider.GetRequiredService<IRoleStore<IdentityRole>>();
        var options = new AuthenticationStoreOptions(
            5,
            TimeSpan.FromMinutes(5),
            $"integration-role-race-{Guid.NewGuid():N}",
            SeedPassword: " ");

        var roleManager = new RoleCreateRaceLosingRoleManager(realRoleStore);
        var initializer = new InitializeLocalAuthenticationData(userManager, roleManager, context, options);

        // RED proves a raw DbUpdateException escapes; GREEN proves the initializer
        // observes the role now exists and proceeds to the controlled password error.
        await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
    }

    [Fact]
    public async Task SeedInitializer_SwallowsAdminRoleAssignmentRace()
    {
        // A concurrent initializer can insert the AspNetUserRoles join row between
        // IsInRoleAsync and AddToRoleAsync; PostgreSQL raises 23505 on
        // PK_AspNetUserRoles. The loser must detach the failed join row and accept
        // the role only when the second check observes it.
        await using var provider = await BuildProviderAsync(ConnectionString());
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<TalentAssistantIdentityDbContext>();
        var realUserManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();
        var store = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        var options = new AuthenticationStoreOptions(
            5,
            TimeSpan.FromMinutes(5),
            $"integration-role-assignment-race-{Guid.NewGuid():N}",
            SeedPassword: Password);

        var userManager = new RoleAssignmentRaceLosingUserManager(
            store,
            scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>(),
            scope.ServiceProvider.GetServices<IUserValidator<ApplicationUser>>(),
            scope.ServiceProvider.GetServices<IPasswordValidator<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<ILookupNormalizer>(),
            scope.ServiceProvider.GetRequiredService<IdentityErrorDescriber>(),
            scope.ServiceProvider,
            NullLogger<UserManager<ApplicationUser>>.Instance);
        var initializer = new InitializeLocalAuthenticationData(
            userManager,
            roleManager,
            context,
            options);

        await initializer.InitializeAsync();

        Assert.NotNull(await realUserManager.FindByNameAsync(options.SeedUserName));
    }

    // Simulates the SPEC-016 admin-role race: the role reads as absent on the first
    // check, CreateAsync loses to a concurrent writer with a "RoleNameIndex" unique
    // violation, and the role reads as present on the recovery check.
    private sealed class RoleCreateRaceLosingRoleManager(IRoleStore<IdentityRole> store)
        : RoleManager<IdentityRole>(
            store,
            Array.Empty<IRoleValidator<IdentityRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance)
    {
        private int _roleExistsCalls;

        public override Task<bool> RoleExistsAsync(string roleName)
        {
            // First call: absent (so the initializer attempts the create). Recovery
            // call inside the race handler: present (another initializer won).
            return Task.FromResult(Interlocked.Increment(ref _roleExistsCalls) > 1);
        }

        public override Task<IdentityResult> CreateAsync(IdentityRole role)
        {
            return Task.FromException<IdentityResult>(
                new DbUpdateException(
                    "An error occurred while saving the entity changes.",
                    new Exception(
                        "duplicate key value violates unique constraint \"RoleNameIndex\"")));
        }
    }

    // Simulates the admin-role assignment race: the first membership check is
    // false, the insert loses to a concurrent writer on PK_AspNetUserRoles, and
    // the recovery check observes the role already assigned.
    private sealed class RoleAssignmentRaceLosingUserManager(
        IUserStore<ApplicationUser> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IEnumerable<IUserValidator<ApplicationUser>> userValidators,
        IEnumerable<IPasswordValidator<ApplicationUser>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        Microsoft.Extensions.Logging.ILogger<UserManager<ApplicationUser>> logger)
        : UserManager<ApplicationUser>(
            store,
            optionsAccessor,
            passwordHasher,
            userValidators,
            passwordValidators,
            keyNormalizer,
            errors,
            services,
            logger)
    {
        private int _isInRoleCalls;

        public override Task<bool> IsInRoleAsync(ApplicationUser user, string role)
        {
            // First call: not assigned (so the initializer attempts the insert).
            // Recovery call inside the race handler: assigned by the winner.
            return Task.FromResult(Interlocked.Increment(ref _isInRoleCalls) > 1);
        }

        public override Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
        {
            return Task.FromException<IdentityResult>(
                new DbUpdateException(
                    "An error occurred while saving the entity changes.",
                    new Exception(
                        "duplicate key value violates unique constraint \"PK_AspNetUserRoles\"")));
        }
    }
}
