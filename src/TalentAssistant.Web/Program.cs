using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using TalentAssistant.Web.Authentication;
using TalentAssistant.Web.Components;
using TalentAssistant.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
var backendApiUrl = builder.Configuration["BackendApi:BaseUrl"];
if (!Uri.TryCreate(backendApiUrl, UriKind.Absolute, out var backendApiBaseAddress)
    || (backendApiBaseAddress.Scheme != Uri.UriSchemeHttp
        && backendApiBaseAddress.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException(
        "Configure BackendApi:BaseUrl with an absolute HTTP or HTTPS address.");
}

builder.Services.AddHttpClient<BackendApiClient>(client =>
{
    client.BaseAddress = backendApiBaseAddress;
    client.Timeout = TimeSpan.FromSeconds(5);
}).AddHttpMessageHandler<AuthenticationMessageHandler>();

builder.Services.AddHttpClient<AuthApiClient>(client =>
{
    client.BaseAddress = backendApiBaseAddress;
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationMessageHandler>();
builder.Services.AddScoped<SessionService>();

builder.Services.AddCascadingAuthenticationState();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

// Static assets (CSS, blazor.web.js, favicon) must stay reachable without a
// session, or the anonymous /login page would render unstyled and non-interactive.
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/session/login", async (
    SessionLoginRequest request,
    AuthApiClient authApiClient,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var login = await authApiClient.LoginAsync(
        request.UserName,
        request.Password,
        cancellationToken);
    if (login is null)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Invalid credentials",
            detail: "The provided credentials are not valid.");
    }

    var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
    identity.AddClaim(new Claim(ClaimTypes.Name, login.UserName));
    identity.AddClaim(new Claim(
        AuthenticationMessageHandler.AccessTokenClaimType,
        login.AccessToken));
    identity.AddClaim(new Claim("expires", login.ExpiresAtUtc.ToString("O")));
    await httpContext.SignInAsync(new ClaimsPrincipal(identity));

    return Results.Ok();
}).WithName("SessionLogin").AllowAnonymous();

app.MapPost("/session/logout", async (
    ClaimsPrincipal user,
    AuthApiClient authApiClient,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var accessToken = user.FindFirst(AuthenticationMessageHandler.AccessTokenClaimType)?.Value;
    if (!string.IsNullOrEmpty(accessToken))
    {
        // Best effort: the API revocation gap documented in ADR-0007 still applies.
        await authApiClient.LogoutAsync(accessToken, cancellationToken);
    }

    await httpContext.SignOutAsync();

    return Results.Ok();
}).WithName("SessionLogout").RequireAuthorization();

app.Run();

internal sealed record SessionLoginRequest(string UserName, string Password);
