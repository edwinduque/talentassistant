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
        // The Blazor circuit negotiates anonymously; every other endpoint
        // requires an authenticated user. Page-level authorization is also
        // enforced by AuthorizeRouteView.
        .RequireAssertion(context =>
            context.Resource is not HttpContext httpContext
            || httpContext.Request.Path.StartsWithSegments("/_blazor")
            || httpContext.User?.Identity?.IsAuthenticated == true)
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
    AuthApiClient authApiClient,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    // The form is read manually (no [FromForm] binding), so the antiforgery
    // middleware does not require a token for this loopback-only, SameSite=Lax
    // local endpoint; see ADR-0007 for the accepted local-profile tradeoff.
    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var login = await authApiClient.LoginAsync(
        form["UserName"].ToString(),
        form["Password"].ToString(),
        cancellationToken);
    if (login is null)
    {
        return Results.Redirect("/login?error=1");
    }

    var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
    identity.AddClaim(new Claim(ClaimTypes.Name, login.UserName));
    identity.AddClaim(new Claim(
        AuthenticationMessageHandler.AccessTokenClaimType,
        login.AccessToken));
    identity.AddClaim(new Claim("expires", login.ExpiresAtUtc.ToString("O")));
    await httpContext.SignInAsync(new ClaimsPrincipal(identity));

    // Only local paths are accepted to avoid open redirects.
    var returnUrl = form["ReturnUrl"].ToString();
    if (string.IsNullOrEmpty(returnUrl)
        || !returnUrl.StartsWith('/')
        || returnUrl.StartsWith("//"))
    {
        returnUrl = "/";
    }

    return Results.Redirect(returnUrl);
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
