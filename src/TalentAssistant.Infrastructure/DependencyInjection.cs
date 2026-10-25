using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using TalentAssistant.Application.Assistant;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Diagnostics;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Assistant;
using TalentAssistant.Infrastructure.Attendance;
using TalentAssistant.Infrastructure.Authentication;
using TalentAssistant.Infrastructure.HcmPostgres;
using TalentAssistant.Infrastructure.Persistence;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        bool useDatasetAttendanceSource = false,
        string? hcmPostgresConnectionString = null,
        int hcmPostgresCommandTimeoutSeconds = 15)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "A database connection string is required.",
                nameof(connectionString));
        }

        services.AddDbContext<TalentAssistantDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IDatabaseAvailabilityCheck, TalentAssistantDatabaseAvailabilityCheck>();
        services.AddScoped<InitializeLocalSyntheticAttendanceData>();

        if (hcmPostgresConnectionString is not null)
        {
            // One switch governs BOTH ports (SPEC-011 S3): in Postgres HCM mode the
            // in-app dataset adapter is never registered, so a mixed source is
            // impossible. The data source connects lazily; the startup validation
            // of Program has already rejected an empty value.
            services.AddSingleton(new HcmPostgresAdapterOptions(hcmPostgresCommandTimeoutSeconds));
            services.AddSingleton<NpgsqlDataSource>(
                _ => NpgsqlDataSource.Create(hcmPostgresConnectionString));
            services.AddSingleton<IHcmWorkforceSource, PostgresWorkforceSource>();
            services.AddSingleton<IHcmAttendanceSource, PostgresAttendanceSource>();
            services.AddSingleton<IHcmDataQuerySource, PostgresHcmDataQuerySource>();
        }
        else
        {
            if (useDatasetAttendanceSource)
            {
                services.AddScoped<IHcmAttendanceSource, DatasetHcmAttendanceSource>();
            }
            else
            {
                services.AddSingleton<IHcmAttendanceSource, SyntheticHcmAttendanceSource>();
            }

            services.AddSingleton<IHcmWorkforceSource, SyntheticWorkforceSource>();
            services.AddScoped<IHcmDataQuerySource, SyntheticHcmDataQuerySource>();
        }

        return services;
    }

    /// <summary>
    /// Registers the OpenAI-compatible assistant provider and its limits from
    /// the "AI" configuration section (SPEC-009 S2). The API may boot with an
    /// empty ApiKey: the client then fails with NotConfigured before any HTTP
    /// call and the endpoint degrades in a controlled way (AC-031).
    /// </summary>
    public static IServiceCollection AddAssistantProvider(
        this IServiceCollection services,
        IConfiguration aiConfiguration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(aiConfiguration);

        var configuration = AssistantProviderConfiguration.Load(aiConfiguration);
        services.AddSingleton(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(provider => new AssistantProviderHealth(
            provider.GetRequiredService<TimeProvider>(),
            configuration.Routing.FailureThreshold,
            TimeSpan.FromSeconds(configuration.Routing.CooldownSeconds)));

        var maxTokens = aiConfiguration.GetValue<int?>("MaxTokens") ?? 700;
        var maxToolRounds = aiConfiguration.GetValue<int?>("MaxToolRounds") ?? 4;
        var maxHistoryMessages = aiConfiguration.GetValue<int?>("MaxHistoryMessages") ?? 20;
        services.AddSingleton(new AssistantOrchestratorOptions(
            maxToolRounds,
            maxHistoryMessages,
            maxTokens));

        services.AddHttpClient("assistant-groq", client =>
            ConfigureProviderClient(client, configuration.Groq));
        services.AddHttpClient("assistant-gemini", client =>
            ConfigureProviderClient(client, configuration.Gemini));

        services.AddScoped<IAssistantLanguageModel>(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var logger = provider.GetRequiredService<ILogger<OpenAiCompatibleLanguageModel>>();
            var slots = configuration.EnabledProviders()
                .Select(settings => new AssistantProviderSlot(
                    settings.Name,
                    configuration.Routing.WeightFor(settings.Name),
                    new OpenAiCompatibleLanguageModel(
                        factory.CreateClient($"assistant-{settings.Name}"),
                        settings.ToClientOptions(),
                        logger),
                    settings.Model))
                .ToArray();

            return new RoutingLanguageModel(
                slots,
                provider.GetRequiredService<AssistantProviderHealth>(),
                provider.GetRequiredService<ILogger<RoutingLanguageModel>>());
        });

        return services;
    }

    private static void ConfigureProviderClient(
        HttpClient client,
        AssistantProviderSettings settings)
    {
        // The trailing slash keeps relative "chat/completions" URIs from
        // replacing the last base path segment (e.g. "v1").
        client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
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
            identityOptions.UseNpgsql(connectionString));
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
