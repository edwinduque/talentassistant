using System.Net.Http.Headers;

namespace TalentAssistant.Web.Authentication;

/// <summary>
/// Attaches the JWT issued at login (stored in the session cookie claims)
/// as a bearer token on outgoing backend API requests.
/// </summary>
public sealed class AuthenticationMessageHandler(
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    public const string AccessTokenClaimType = "access_token";
    public const string FullNameClaimType = "full_name";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var accessToken = httpContextAccessor.HttpContext?.User?
            .FindFirst(AccessTokenClaimType)?.Value;

        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                accessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
