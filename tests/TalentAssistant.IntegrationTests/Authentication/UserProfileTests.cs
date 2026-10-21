using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalentAssistant.Api.Authentication;
using TalentAssistant.Infrastructure.Persistence;

namespace TalentAssistant.IntegrationTests.Authentication;

public sealed class UserProfileTests
{
    private const string Password = "Integration!Pass123";
    private const string NewPassword = "Changed!Pass123";

    private static async Task<ServiceProvider> BuildProviderAsync(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TalentAssistantIdentityDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<TalentAssistantIdentityDbContext>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<TalentAssistantIdentityDbContext>();
        await context.Database.MigrateAsync();
        return provider;
    }

    private static string ConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__TalentAssistantTests");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Set ConnectionStrings__TalentAssistantTests to the dedicated talentassistant_tests database.");
        return connectionString!;
    }

    [Fact]
    public async Task ProfileFieldsRoundTripAndDefaultToSpanish()
    {
        await using var provider = await BuildProviderAsync(ConnectionString());
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider
            .GetRequiredService<TalentAssistantIdentityDbContext>();
        var userName = $"integration-profile-{Guid.NewGuid():N}";

        try
        {
            var user = new ApplicationUser
            {
                UserName = userName,
                Email = userName,
                EmailConfirmed = true
            };
            Assert.True((await userManager.CreateAsync(user, Password)).Succeeded);

            // A fresh user defaults to Spanish and has no display name yet.
            var stored = await context.Users.AsNoTracking()
                .SingleAsync(candidate => candidate.UserName == userName);
            Assert.Equal("es", stored.PreferredLanguage);
            Assert.True(string.IsNullOrEmpty(stored.FullName));

            // The migration is additive: the two profile columns exist and are writable.
            // Trimming and catalog validation live in the API endpoint, not in the entity.
            user.FullName = "Demo Person";
            user.PreferredLanguage = "en";
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);

            var updated = await context.Users.AsNoTracking()
                .SingleAsync(candidate => candidate.UserName == userName);
            Assert.Equal("Demo Person", updated.FullName);
            Assert.Equal("en", updated.PreferredLanguage);
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
    public async Task ChangePasswordReplacesThePreviousPassword()
    {
        await using var provider = await BuildProviderAsync(ConnectionString());
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userName = $"integration-password-{Guid.NewGuid():N}";

        try
        {
            var user = new ApplicationUser
            {
                UserName = userName,
                Email = userName,
                EmailConfirmed = true
            };
            Assert.True((await userManager.CreateAsync(user, Password)).Succeeded);

            var wrongCurrent = await userManager.ChangePasswordAsync(user, "Wrong!Pass123", NewPassword);
            Assert.False(wrongCurrent.Succeeded);
            Assert.True(await userManager.CheckPasswordAsync(user, Password));

            var policyViolating = await userManager.ChangePasswordAsync(user, Password, "weak");
            Assert.False(policyViolating.Succeeded);
            Assert.True(await userManager.CheckPasswordAsync(user, Password));

            var change = await userManager.ChangePasswordAsync(user, Password, NewPassword);
            Assert.True(change.Succeeded, string.Join(" ", change.Errors.Select(error => error.Description)));
            Assert.False(await userManager.CheckPasswordAsync(user, Password));
            Assert.True(await userManager.CheckPasswordAsync(user, NewPassword));
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

    [Theory]
    [InlineData("es", true)]
    [InlineData("en", true)]
    [InlineData("fr", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SupportedLanguagesAcceptsOnlyTheClosedCatalog(string? language, bool expected)
    {
        Assert.Equal(expected, SupportedLanguages.IsSupported(language));
    }
}
