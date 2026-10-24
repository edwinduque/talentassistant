extern alias Web;

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using LoginLayout = Web::TalentAssistant.Web.Components.Layout.LoginLayout;

namespace TalentAssistant.IntegrationTests.Web;

public sealed class PublicLandingRouteTests : IDisposable
{
    private readonly WebApplicationFactory<LoginLayout> factory;
    private readonly HttpClient client;

    public PublicLandingRouteTests()
    {
        factory = new WebApplicationFactory<LoginLayout>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("BackendApi:BaseUrl", "http://127.0.0.1"));
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Root_displays_the_public_landing_in_both_supported_languages()
    {
        var spanish = await client.GetAsync("/");
        var spanishHtml = WebUtility.HtmlDecode(await spanish.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, spanish.StatusCode);
        Assert.Contains("La información del HCM, más clara para revisar cada caso.", spanishHtml);
        Assert.Contains("href=\"/login\"", spanishHtml);
        Assert.Contains("href=\"#project\"", spanishHtml);
        Assert.Contains("href=\"#how-it-works\"", spanishHtml);

        var english = await client.GetAsync("/?culture=en&ui-culture=en");
        var englishHtml = WebUtility.HtmlDecode(await english.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, english.StatusCode);
        Assert.Contains("Make HCM data easier to review, one case at a time.", englishHtml);
    }

    [Fact]
    public async Task Overview_remains_protected_and_redirects_anonymous_visitors_to_login()
    {
        var response = await client.GetAsync("/overview");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Attendance_page_is_removed_and_its_route_stays_non_public()
    {
        // SPEC-019: the component itself must be gone from the Web assembly.
        var pageType = typeof(LoginLayout).Assembly.GetType(
            "TalentAssistant.Web.Components.Pages.AttendanceInsights");
        Assert.Null(pageType);

        // The retired route must not serve the page to anonymous visitors.
        var attendance = await client.GetAsync("/attendance-insights");

        Assert.Equal(HttpStatusCode.Redirect, attendance.StatusCode);
        Assert.Equal("/login", attendance.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Login_remains_public_and_defaults_to_the_protected_overview()
    {
        var response = await client.GetAsync("/login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"ReturnUrl\" value=\"/overview\"", html);

        var requestedPage = await client.GetAsync("/login?ReturnUrl=%2Fassistant");
        var requestedPageHtml = WebUtility.HtmlDecode(await requestedPage.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, requestedPage.StatusCode);
        Assert.Contains("name=\"ReturnUrl\" value=\"/assistant\"", requestedPageHtml);
    }

    [Fact]
    public async Task Login_uses_the_landing_brand_tokens_from_the_shared_stylesheet()
    {
        var login = await client.GetAsync("/login");
        var loginHtml = await login.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.DoesNotContain("#f97316", loginHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"rememberMe\"", loginHtml);
        Assert.Contains("class=\"form-checkbox\"", loginHtml);

        var stylesheet = await client.GetAsync("/app.css");
        var css = await stylesheet.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, stylesheet.StatusCode);
        Assert.Contains("--brand-deep: #17383b", css);
        Assert.Contains("--brand-highlight: #e5ad48", css);
        Assert.Contains(".status-card", css);
        Assert.Contains(".filter-panel", css);
        Assert.Contains(".admin-users-table", css);
        Assert.Contains(".brand-mark", css);
        Assert.Contains(".sr-only", css);
        Assert.Contains(".visually-hidden", css);
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }
}
