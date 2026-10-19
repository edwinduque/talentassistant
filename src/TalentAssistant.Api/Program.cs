using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using TalentAssistant.Api.Authentication;
using TalentAssistant.Application.Assistant;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Infrastructure.Attendance;
using TalentAssistant.Infrastructure;
using TalentAssistant.Infrastructure.Authentication;
using TalentAssistant.Infrastructure.Persistence;
using TalentAssistant.Application.Diagnostics;
using TalentAssistant.Application.Hcm;
using TalentAssistant.Application.Workforce;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TalentAssistant");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings:TalentAssistant with User Secrets or an environment variable.");
}

var attendanceSource = builder.Configuration["Attendance:Source"] ?? "Memory";
var useMySqlAttendanceSource = string.Equals(
    attendanceSource,
    "MySql",
    StringComparison.OrdinalIgnoreCase);
if (!useMySqlAttendanceSource
    && !string.Equals(attendanceSource, "Memory", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Attendance:Source must be configured as Memory or MySql.");
}

// Single HCM source switch (SPEC-011 S3): one value governs both ports at once
// and an invalid combination fails fast here, at startup, instead of silently
// serving synthetic data. The pure rules live in HcmPostgresOptions so unit
// tests reach them. The flat HcmPostgres:* keys (environment variables
// HcmPostgres__*) take precedence over the Hcm:Postgres:* appsettings defaults.
var hcmSource = builder.Configuration["Hcm:Source"] ?? HcmPostgresOptions.SyntheticSource;
var hcmPostgresConnectionString = FirstConfiguredValue(
    builder.Configuration["HcmPostgres:ConnectionString"],
    builder.Configuration["Hcm:Postgres:ConnectionString"]);
var hcmPostgresCommandTimeoutSeconds =
    builder.Configuration.GetValue<int?>("HcmPostgres:CommandTimeoutSeconds")
    ?? builder.Configuration.GetValue<int?>("Hcm:Postgres:CommandTimeoutSeconds")
    ?? 15;
HcmPostgresOptions.Validate(hcmSource, hcmPostgresConnectionString);
var usePostgresHcmSource = HcmPostgresOptions.IsPostgres(hcmSource);

var initializeLocalData = args.Contains("--initialize-local-data", StringComparer.Ordinal);

var seedUserName = builder.Configuration["Authentication:SeedUserName"]
    ?? "demo@talentassistant.local";
var lockoutMaxFailedAccessAttempts =
    builder.Configuration.GetValue<int?>("Authentication:LockoutMaxFailedAccessAttempts") ?? 5;
var lockoutMinutes = builder.Configuration.GetValue<int?>("Authentication:LockoutMinutes") ?? 5;

builder.Services.AddInfrastructure(
    connectionString,
    useMySqlAttendanceSource,
    usePostgresHcmSource ? hcmPostgresConnectionString : null,
    hcmPostgresCommandTimeoutSeconds);
builder.Services.AddIdentityInfrastructure(
    connectionString,
    new AuthenticationStoreOptions(
        lockoutMaxFailedAccessAttempts,
        TimeSpan.FromMinutes(lockoutMinutes),
        seedUserName,
        builder.Configuration["Authentication:SeedPassword"]));
builder.Services.AddScoped<GetAttendanceInsights>();
builder.Services.AddScoped<SearchEmployees>();
builder.Services.AddScoped<GetEmployeePresence>();
builder.Services.AddScoped<GetEmployeeTimeOff>();
builder.Services.AddScoped<GetEmployeeTasks>();
builder.Services.AddAssistantProvider(builder.Configuration.GetSection("AI"));
builder.Services.AddScoped<ChatOrchestrator>();

var authenticationIssuer = builder.Configuration["Authentication:Issuer"] ?? "TalentAssistant";
var authenticationAudience = builder.Configuration["Authentication:Audience"] ?? "TalentAssistant.Local";
var authenticationSigningKey = builder.Configuration["Authentication:SigningKey"];
var tokenLifetimeMinutes = builder.Configuration.GetValue<int?>("Authentication:TokenLifetimeMinutes") ?? 60;

if (!initializeLocalData)
{
    if (string.IsNullOrWhiteSpace(authenticationSigningKey)
        || authenticationSigningKey.Length < 32)
    {
        throw new InvalidOperationException(
            "Configure Authentication:SigningKey with at least 32 characters using User Secrets or an environment variable.");
    }

    var issuerSigningKey = new SymmetricSecurityKey(
        Encoding.UTF8.GetBytes(authenticationSigningKey));

    builder.Services
        .AddAuthentication(authenticationOptions =>
        {
            authenticationOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            authenticationOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            authenticationOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authenticationIssuer,
                ValidateAudience = true,
                ValidAudience = authenticationAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = issuerSigningKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
    builder.Services.AddAuthorization();
    builder.Services.AddScoped<IAccessTokenService>(_ =>
        new AccessTokenService(
            authenticationIssuer,
            authenticationAudience,
            authenticationSigningKey,
            tokenLifetimeMinutes));
}

var app = builder.Build();

if (initializeLocalData)
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider
        .GetRequiredService<InitializeLocalSyntheticAttendanceData>();
    await initializer.InitializeAsync();

    var authenticationInitializer = scope.ServiceProvider
        .GetRequiredService<InitializeLocalAuthenticationData>();
    await authenticationInitializer.InitializeAsync();
    return;
}

app.MapGet("/health", () => Results.Ok(new HealthResponse("healthy")))
    .WithName("Health")
    .AllowAnonymous();

app.MapGet("/health/database", async (
    IDatabaseAvailabilityCheck databaseCheck,
    CancellationToken cancellationToken) =>
{
    if (await databaseCheck.IsAvailableAsync(cancellationToken))
    {
        return Results.Ok(new HealthResponse("healthy"));
    }

    return Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Database unavailable",
        detail: "The database could not be reached.");
}).WithName("DatabaseHealth").AllowAnonymous();

app.MapPost("/auth/login", async (
    LoginRequest request,
    SignInManager<ApplicationUser> signInManager,
    IAccessTokenService accessTokenService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.UserName)
        || string.IsNullOrWhiteSpace(request.Password))
    {
        return InvalidCredentials();
    }

    var user = await signInManager.UserManager.FindByNameAsync(request.UserName);
    if (user is null)
    {
        return InvalidCredentials();
    }

    var signInResult = await signInManager.CheckPasswordSignInAsync(
        user,
        request.Password,
        lockoutOnFailure: true);
    if (!signInResult.Succeeded)
    {
        return InvalidCredentials();
    }

    var accessToken = accessTokenService.Create(user.Id, user.UserName ?? request.UserName);
    return Results.Ok(new LoginResponse(
        accessToken.Token,
        accessToken.ExpiresAtUtc,
        accessToken.UserName,
        user.FullName ?? string.Empty,
        user.PreferredLanguage));
}).WithName("Login").AllowAnonymous();

app.MapPost("/auth/logout", () => Results.Ok(new LogoutResponse("signed out")))
    .WithName("Logout")
    .RequireAuthorization();

app.MapGet("/auth/me", async (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(principal);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(new ProfileResponse(
            user.UserName ?? string.Empty,
            user.FullName ?? string.Empty,
            user.PreferredLanguage));
}).WithName("AuthenticatedUser")
    .RequireAuthorization();

app.MapPost("/auth/profile", async (
    ProfileRequest request,
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    CancellationToken cancellationToken) =>
{
    if (!SupportedLanguages.IsSupported(request.PreferredLanguage))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Unsupported language",
            detail: "Provide 'es' or 'en' as the preferred language.");
    }

    var user = await userManager.GetUserAsync(principal);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    user.FullName = string.IsNullOrWhiteSpace(request.FullName)
        ? null
        : request.FullName.Trim();
    user.PreferredLanguage = request.PreferredLanguage!;

    var updateResult = await userManager.UpdateAsync(user);
    if (!updateResult.Succeeded)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Profile update failed",
            detail: string.Join(" ", updateResult.Errors.Select(error => error.Description)));
    }

    return Results.Ok(new ProfileResponse(
        user.UserName ?? string.Empty,
        user.FullName ?? string.Empty,
        user.PreferredLanguage));
}).WithName("UpdateProfile").RequireAuthorization();

app.MapPost("/auth/change-password", async (
    ChangePasswordRequest request,
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    CancellationToken cancellationToken) =>
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var changeResult = await userManager.ChangePasswordAsync(
        user,
        request.CurrentPassword,
        request.NewPassword);
    if (!changeResult.Succeeded)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Password change failed",
            detail: string.Join(" ", changeResult.Errors.Select(error => error.Description)));
    }

    return Results.Ok(new ChangePasswordResponse("password changed"));
}).WithName("ChangePassword").RequireAuthorization();

app.MapGet("/attendance/insights", async (
    string? from,
    string? to,
    string? employeeId,
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    GetAttendanceInsights getAttendanceInsights,
    CancellationToken cancellationToken) =>
{
    if (!DateOnly.TryParseExact(
            from,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var fromDate)
        || !DateOnly.TryParseExact(
            to,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var toDate)
        || fromDate >= toDate)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid date range",
            detail: "Provide from and to dates as yyyy-MM-dd, with from earlier than to.");
    }

    try
    {
        // The data language always comes from the authenticated user's profile,
        // so a profile language change applies to the next query without a
        // re-login (SPEC-007 §8).
        var user = await userManager.GetUserAsync(principal);
        var language = user?.PreferredLanguage ?? ApplicationUser.DefaultPreferredLanguage;
        var query = new AttendanceInsightQuery(fromDate, toDate, employeeId, language);
        var insights = await getAttendanceInsights.ExecuteAsync(query, cancellationToken);
        return Results.Ok(insights);
    }
    catch (TimeZoneNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Attendance source unavailable",
            detail: "Attendance insights are temporarily unavailable.");
    }
    catch (InvalidTimeZoneException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Attendance source unavailable",
            detail: "Attendance insights are temporarily unavailable.");
    }
    catch (AttendanceSourceUnavailableException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Attendance source unavailable",
            detail: "Attendance insights are temporarily unavailable.");
    }
    catch (HcmSourceUnavailableException)
    {
        // Machine-readable reason only; the Web layer localizes the copy
        // (SPEC-011 S3 degradation: honest "source unavailable", never
        // synthetic data).
        return Results.Json(
            new { reason = "SourceUnavailable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).WithName("AttendanceInsights").RequireAuthorization();

app.MapGet("/employees", async (
    string? name,
    string? officeId,
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    SearchEmployees searchEmployees,
    CancellationToken cancellationToken) =>
{
    var user = await userManager.GetUserAsync(principal);
    var language = user?.PreferredLanguage ?? ApplicationUser.DefaultPreferredLanguage;
    try
    {
        var employees = await searchEmployees.ExecuteAsync(
            new EmployeeSearchQuery(name, officeId, language),
            cancellationToken);
        return Results.Ok(employees);
    }
    catch (HcmSourceUnavailableException)
    {
        // Machine-readable reason only; the Web layer localizes the copy
        // (SPEC-011 S3 degradation: honest "source unavailable", never
        // synthetic data).
        return Results.Json(
            new { reason = "SourceUnavailable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).WithName("Employees").RequireAuthorization();

app.MapGet("/presence", async (
    string? date,
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    GetEmployeePresence getPresence,
    CancellationToken cancellationToken) =>
{
    var parsedDate = DateOnly.FromDateTime(DateTime.Today);
    if (!string.IsNullOrWhiteSpace(date)
        && !DateOnly.TryParseExact(
            date,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsedDate))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid date",
            detail: "Provide date as yyyy-MM-dd.");
    }

    var user = await userManager.GetUserAsync(principal);
    var language = user?.PreferredLanguage ?? ApplicationUser.DefaultPreferredLanguage;
    try
    {
        var snapshot = await getPresence.ExecuteAsync(
            new PresenceQuery(parsedDate, language),
            cancellationToken);
        return Results.Ok(snapshot);
    }
    catch (HcmSourceUnavailableException)
    {
        // Machine-readable reason only; the Web layer localizes the copy
        // (SPEC-011 S3 degradation: honest "source unavailable", never
        // synthetic data).
        return Results.Json(
            new { reason = "SourceUnavailable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).WithName("Presence").RequireAuthorization();

app.MapGet("/timeoff", async (
    string? employeeId,
    string? name,
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    GetEmployeeTimeOff getTimeOff,
    CancellationToken cancellationToken) =>
{
    var user = await userManager.GetUserAsync(principal);
    var language = user?.PreferredLanguage ?? ApplicationUser.DefaultPreferredLanguage;
    try
    {
        var requests = await getTimeOff.ExecuteAsync(
            new EmployeeDataQuery(employeeId, name, language),
            cancellationToken);
        return Results.Ok(requests);
    }
    catch (HcmSourceUnavailableException)
    {
        // Machine-readable reason only; the Web layer localizes the copy
        // (SPEC-011 S3 degradation: honest "source unavailable", never
        // synthetic data).
        return Results.Json(
            new { reason = "SourceUnavailable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).WithName("TimeOffByEmployee").RequireAuthorization();

app.MapGet("/tasks", async (
    string? employeeId,
    string? name,
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    GetEmployeeTasks getTasks,
    CancellationToken cancellationToken) =>
{
    var user = await userManager.GetUserAsync(principal);
    var language = user?.PreferredLanguage ?? ApplicationUser.DefaultPreferredLanguage;
    try
    {
        var tasks = await getTasks.ExecuteAsync(
            new EmployeeDataQuery(employeeId, name, language),
            cancellationToken);
        return Results.Ok(tasks);
    }
    catch (HcmSourceUnavailableException)
    {
        // Machine-readable reason only; the Web layer localizes the copy
        // (SPEC-011 S3 degradation: honest "source unavailable", never
        // synthetic data).
        return Results.Json(
            new { reason = "SourceUnavailable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).WithName("TasksByEmployee").RequireAuthorization();

app.MapPost("/chat", async (
    ChatRequest request,
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    ChatOrchestrator chatOrchestrator,
    AssistantOrchestratorOptions orchestratorOptions,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Missing message",
            detail: "Provide a non-empty message.");
    }

    // The client holds the history; the server caps it so a client cannot
    // inflate the provider payload (SPEC-009 §8.1, §8.5).
    var historyTurns = request.History ?? Array.Empty<ChatTurn>();
    var history = historyTurns
        .Where(turn => turn is not null
            && !string.IsNullOrWhiteSpace(turn.Role)
            && !string.IsNullOrWhiteSpace(turn.Content))
        .TakeLast(orchestratorOptions.MaxHistoryMessages)
        .Select(turn => new ChatMessage(turn.Role, turn.Content))
        .ToArray();

    try
    {
        // The data language always comes from the authenticated user's profile,
        // same resolution as /attendance/insights (SPEC-007 §8).
        var user = await userManager.GetUserAsync(principal);
        var language = user?.PreferredLanguage ?? ApplicationUser.DefaultPreferredLanguage;
        var result = await chatOrchestrator.ExecuteAsync(
            new ChatQuery(request.Message, history, language),
            cancellationToken);
        return Results.Ok(result);
    }
    catch (AssistantProviderUnavailableException exception)
    {
        // Machine-readable reason only; the UI localizes the message (SPEC-009 S1).
        return Results.Json(
            new { reason = exception.Reason.ToString() },
            statusCode: exception.Reason == AssistantProviderFailureReason.RateLimited
                ? StatusCodes.Status429TooManyRequests
                : StatusCodes.Status503ServiceUnavailable);
    }
}).WithName("Chat").RequireAuthorization();

app.Run();

static IResult InvalidCredentials() => Results.Problem(
    statusCode: StatusCodes.Status401Unauthorized,
    title: "Invalid credentials",
    detail: "The provided credentials are not valid.");

static string? FirstConfiguredValue(params string?[] values) =>
    values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

internal sealed record HealthResponse(string Status);
internal sealed record LoginRequest(string UserName, string Password);
internal sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string UserName,
    string FullName,
    string PreferredLanguage);
internal sealed record LogoutResponse(string Status);
internal sealed record ProfileRequest(string FullName, string? PreferredLanguage);
internal sealed record ProfileResponse(string UserName, string FullName, string PreferredLanguage);
internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
internal sealed record ChangePasswordResponse(string Status);
internal sealed record ChatRequest(string Message, IReadOnlyList<ChatTurn>? History);
internal sealed record ChatTurn(string Role, string Content);

// Top-level-statements entry point marker: exposes the generated Program class
// to WebApplicationFactory<Program> in the integration test project.
public partial class Program;
