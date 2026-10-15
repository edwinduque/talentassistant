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
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
