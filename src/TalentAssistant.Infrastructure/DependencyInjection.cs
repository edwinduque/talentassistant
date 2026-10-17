using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Diagnostics;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Attendance;
using TalentAssistant.Infrastructure.Authentication;
using TalentAssistant.Infrastructure.Persistence;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        bool useMySqlAttendanceSource = false)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "A database connection string is required.",
                nameof(connectionString));
        }

        services.AddDbContext<TalentAssistantDbContext>(options =>
            options.UseMySQL(connectionString));
        services.AddScoped<IDatabaseAvailabilityCheck, MySqlDatabaseAvailabilityCheck>();
        services.AddScoped<InitializeLocalSyntheticAttendanceData>();

        if (useMySqlAttendanceSource)
        {
            services.AddScoped<IHcmAttendanceSource, MySqlHcmAttendanceSource>();
        }
        else
        {
            services.AddSingleton<IHcmAttendanceSource, SyntheticHcmAttendanceSource>();
        }

        services.AddSingleton<IHcmWorkforceSource, SyntheticWorkforceSource>();

        return services;
    }

    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        string connectionString,
        AuthenticationStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "A database connection string is required.",
                nameof(connectionString));
        }

        services.AddSingleton(options);
        services.AddDbContext<TalentAssistantIdentityDbContext>(identityOptions =>
            identityOptions.UseMySQL(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole>(identity =>
            {
                // Identity defaults are kept for password policy; only lockout is configurable.
                identity.Lockout.MaxFailedAccessAttempts = options.LockoutMaxFailedAccessAttempts;
                identity.Lockout.DefaultLockoutTimeSpan = options.LockoutTime;
            })
            .AddEntityFrameworkStores<TalentAssistantIdentityDbContext>();
        services.AddScoped<InitializeLocalAuthenticationData>();

        return services;
    }
}
