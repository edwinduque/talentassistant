namespace TalentAssistant.Web.Authentication;

/// <summary>
/// Claim names persisted in the Web authentication cookie. The access token is
/// read from the Blazor circuit's authentication state for each API request;
/// it is not read from ambient HttpContext during interactive rendering.
/// </summary>
public static class AuthenticationClaimTypes
{
    public const string AccessToken = "access_token";
    public const string FullName = "full_name";
}
