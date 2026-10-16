using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TalentAssistant.Api.Authentication;

public sealed record AccessToken(
    string Token,
    DateTimeOffset ExpiresAtUtc,
    string UserName);

public interface IAccessTokenService
{
    AccessToken Create(string userId, string userName);
}

/// <summary>
/// Issues short-lived symmetric JWTs for the local profile. The signing key comes
/// exclusively from external configuration; it is never stored in the repository.
/// </summary>
public sealed class AccessTokenService(
    string issuer,
    string audience,
    string signingKey,
    int lifetimeMinutes) : IAccessTokenService
{
    private const int MinimumSigningKeyLength = 32;

    public AccessToken Create(string userId, string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < MinimumSigningKeyLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(signingKey),
                $"The signing key must contain at least {MinimumSigningKeyLength} characters.");
        }

        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddMinutes(lifetimeMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.UniqueName, userName)
            ],
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            userName);
    }
}
