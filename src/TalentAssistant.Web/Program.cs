using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.WebUtilities;
using TalentAssistant.Web.Authentication;
using TalentAssistant.Web.Components;
using TalentAssistant.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
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

// Culture comes only from the cookie set by the language selector (or the
// ?culture= query parameter); the Accept-Language provider is intentionally
// removed so the browser never overrides the explicit choice (SPEC-006).
var supportedCultures = new[] { new CultureInfo("es"), new CultureInfo("en") };
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("es"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
};
localizationOptions.RequestCultureProviders.Clear();
localizationOptions.RequestCultureProviders.Add(new CookieRequestCultureProvider());
localizationOptions.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
app.UseRequestLocalization(localizationOptions);

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
        AuthenticationMessageHandler.FullNameClaimType,
        login.FullName));
    identity.AddClaim(new Claim(
        AuthenticationMessageHandler.AccessTokenClaimType,
        login.AccessToken));
    identity.AddClaim(new Claim("expires", login.ExpiresAtUtc.ToString("O")));

    // "Keep me signed in": a persistent 30-day cookie when the user opts in,
    // otherwise a browser-session cookie (the previous behaviour).
    var rememberMe = string.Equals(
        form["rememberMe"].ToString(),
        "true",
        StringComparison.OrdinalIgnoreCase);
    await httpContext.SignInAsync(
        new ClaimsPrincipal(identity),
        new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null,
            AllowRefresh = rememberMe
        });

    // The session inherits the preferred language saved in the user profile.
    if (SessionCulture.IsSupported(login.PreferredLanguage))
    {
        AppendCultureCookie(httpContext.Response, login.PreferredLanguage);
    }

    return Results.Redirect(GetLocalReturnUrl(form["ReturnUrl"].ToString()));
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

// Same manual-form pattern as /session/login: the antiforgery middleware does
// not require a token because no parameter is bound with [FromForm].
app.MapPost("/session/culture", async (
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var culture = form["culture"].ToString();
    if (!SessionCulture.IsSupported(culture))
    {
        return Results.Redirect("/login?cultureError=1");
    }

    AppendCultureCookie(httpContext.Response, culture);

    return Results.Redirect(GetLocalReturnUrl(form["ReturnUrl"].ToString()));
}).WithName("SessionCulture").AllowAnonymous();

app.MapPost("/session/profile", async (
    ClaimsPrincipal user,
    AuthApiClient authApiClient,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var returnUrl = GetLocalReturnUrl(form["ReturnUrl"].ToString());
    var fullName = form["FullName"].ToString();
    var preferredLanguage = form["PreferredLanguage"].ToString();
    if (!SessionCulture.IsSupported(preferredLanguage))
    {
        return Results.Redirect(QueryHelpers.AddQueryString(returnUrl, "profileError", "1"));
    }

    var accessToken = user.FindFirst(AuthenticationMessageHandler.AccessTokenClaimType)?.Value;
    var profile = string.IsNullOrEmpty(accessToken)
        ? null
        : await authApiClient.UpdateProfileAsync(
            accessToken,
            fullName,
            preferredLanguage,
            cancellationToken);
    if (profile is null)
    {
        return Results.Redirect(QueryHelpers.AddQueryString(returnUrl, "profileError", "1"));
    }

    // The interface follows the saved language immediately.
    AppendCultureCookie(httpContext.Response, profile.PreferredLanguage);

    // Re-issue the session cookie with the saved display name so the layout
    // reflects it without requiring a new sign-in.
    var identity = new ClaimsIdentity(
        user.Claims.Where(claim => claim.Type != AuthenticationMessageHandler.FullNameClaimType),
        CookieAuthenticationDefaults.AuthenticationScheme);
    identity.AddClaim(new Claim(
        AuthenticationMessageHandler.FullNameClaimType,
        profile.FullName));
    await httpContext.SignInAsync(new ClaimsPrincipal(identity));

    return Results.Redirect(returnUrl);
}).WithName("SessionProfile").RequireAuthorization();

app.MapPost("/session/password", async (
    ClaimsPrincipal user,
    AuthApiClient authApiClient,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var returnUrl = GetLocalReturnUrl(form["ReturnUrl"].ToString());
    var currentPassword = form["CurrentPassword"].ToString();
    var newPassword = form["NewPassword"].ToString();
    var confirmPassword = form["ConfirmPassword"].ToString();
    if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
    {
        return Results.Redirect(QueryHelpers.AddQueryString(returnUrl, "passwordError", "confirm"));
    }

    var accessToken = user.FindFirst(AuthenticationMessageHandler.AccessTokenClaimType)?.Value;
    var outcome = string.IsNullOrEmpty(accessToken)
        ? ChangePasswordOutcome.Failed
        : await authApiClient.ChangePasswordAsync(
            accessToken,
            currentPassword,
            newPassword,
            cancellationToken);

    return outcome switch
    {
        ChangePasswordOutcome.Success =>
            Results.Redirect(QueryHelpers.AddQueryString(returnUrl, "passwordChanged", "1")),
        ChangePasswordOutcome.WrongCurrentPassword =>
            Results.Redirect(QueryHelpers.AddQueryString(returnUrl, "passwordError", "current")),
        ChangePasswordOutcome.Rejected =>
            Results.Redirect(QueryHelpers.AddQueryString(returnUrl, "passwordError", "policy")),
        _ =>
            Results.Redirect(QueryHelpers.AddQueryString(returnUrl, "passwordError", "service")),
    };
}).WithName("SessionPassword").RequireAuthorization();

app.Run();

// Only local paths are accepted to avoid open redirects.
static string GetLocalReturnUrl(string returnUrl, string fallback = "/")
{
    if (string.IsNullOrEmpty(returnUrl)
        || !returnUrl.StartsWith('/')
        || returnUrl.StartsWith("//"))
    {
        return fallback;
    }

    return returnUrl;
}

static void AppendCultureCookie(HttpResponse response, string culture)
{
    response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
        });
}
