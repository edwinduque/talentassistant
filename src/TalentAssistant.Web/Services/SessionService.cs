using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TalentAssistant.Web.Services;

/// <summary>
/// Starts and ends the browser session by calling the Web app's own
/// <c>/session/*</c> endpoints from the BROWSER (same-origin fetch), so the
/// <c>Set-Cookie</c> response reaches the browser cookie jar. A server-side
/// HttpClient loopback could not deliver the HttpOnly session cookie.
/// </summary>
public sealed class SessionService(
    IJSRuntime jsRuntime,
    NavigationManager navigation)
{
    public async Task<bool> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var url = navigation.ToAbsoluteUri("/session/login").ToString();
        try
        {
            return await jsRuntime.InvokeAsync<bool>(
                "talentAssistantSession.login",
                cancellationToken,
                url,
                userName,
                password);
        }
        catch (JSException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var url = navigation.ToAbsoluteUri("/session/logout").ToString();
        try
        {
            await jsRuntime.InvokeVoidAsync(
                "talentAssistantSession.logout",
                cancellationToken,
                url);
        }
        catch (JSException)
        {
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }
}
