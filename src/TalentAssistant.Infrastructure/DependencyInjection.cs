using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Diagnostics;
using TalentAssistant.Infrastructure.Attendance;
using TalentAssistant.Infrastructure.Persistence;

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

        return services;
    }
}
