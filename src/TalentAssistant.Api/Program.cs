using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using TalentAssistant.Api.Authentication;
using TalentAssistant.Application.Attendance;
using TalentAssistant.Infrastructure.Attendance;
using TalentAssistant.Infrastructure;
using TalentAssistant.Infrastructure.Authentication;
using TalentAssistant.Infrastructure.Persistence;
using TalentAssistant.Application.Diagnostics;

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

var initializeLocalData = args.Contains("--initialize-local-data", StringComparer.Ordinal);

var seedUserName = builder.Configuration["Authentication:SeedUserName"]
    ?? "demo@talentassistant.local";
var lockoutMaxFailedAccessAttempts =
    builder.Configuration.GetValue<int?>("Authentication:LockoutMaxFailedAccessAttempts") ?? 5;
var lockoutMinutes = builder.Configuration.GetValue<int?>("Authentication:LockoutMinutes") ?? 5;

builder.Services.AddInfrastructure(connectionString, useMySqlAttendanceSource);
builder.Services.AddIdentityInfrastructure(
    connectionString,
    new AuthenticationStoreOptions(
        lockoutMaxFailedAccessAttempts,
        TimeSpan.FromMinutes(lockoutMinutes),
        seedUserName,
        builder.Configuration["Authentication:SeedPassword"]));
builder.Services.AddScoped<GetAttendanceInsights>();

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
        var query = new AttendanceInsightQuery(fromDate, toDate, employeeId);
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
}).WithName("AttendanceInsights").RequireAuthorization();

app.Run();

static IResult InvalidCredentials() => Results.Problem(
    statusCode: StatusCodes.Status401Unauthorized,
    title: "Invalid credentials",
    detail: "The provided credentials are not valid.");

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
