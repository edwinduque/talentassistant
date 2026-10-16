using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using TalentAssistant.Api.Authentication;

namespace TalentAssistant.IntegrationTests.Authentication;

public sealed class AccessTokenServiceTests
{
    private const string SigningKey = "integration-test-signing-key-with-32-characters!";
    private const string Issuer = "TalentAssistant.Test";
    private const string Audience = "TalentAssistant.Local.Test";

    private static AccessTokenService CreateService(int lifetimeMinutes = 60) =>
        new(Issuer, Audience, SigningKey, lifetimeMinutes);

    private static TokenValidationParameters CreateValidationParameters(string? key = null) =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(key ?? SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

    [Fact]
    public void Create_ProducesSignedTokenForTheUser()
    {
        var service = CreateService();

        var token = service.Create("user-123", "demo@talentassistant.local");

        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        Assert.Equal("demo@talentassistant.local", token.UserName);
        Assert.True(token.ExpiresAtUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Token_ValidatesWithTheSameKeyIssuerAndAudience()
    {
        var service = CreateService();

        var token = service.Create("user-123", "demo@talentassistant.local");

        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token.Token,
            CreateValidationParameters(),
            out var validatedToken);

        Assert.NotNull(validatedToken);
        Assert.Equal("demo@talentassistant.local", principal.Identity?.Name);
        Assert.True(principal.Identity?.IsAuthenticated);
    }

    [Fact]
    public void ExpiredToken_IsRejectedByValidation()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var expiredToken = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: credentials);

        var serialized = new JwtSecurityTokenHandler().WriteToken(expiredToken);

        Assert.Throws<SecurityTokenExpiredException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                serialized,
                CreateValidationParameters(),
                out _));
    }

    [Fact]
    public void TokenSignedWithAnotherKey_IsRejected()
    {
        var service = CreateService();

        var token = service.Create("user-123", "demo@talentassistant.local");

        var parameters = CreateValidationParameters(
            key: "another-signing-key-with-32-characters!!");

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                token.Token,
                parameters,
                out _));
    }

    [Fact]
    public void Create_WithShortSigningKey_Fails()
    {
        var service = new AccessTokenService(Issuer, Audience, "short", 60);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.Create("user-123", "demo@talentassistant.local"));
    }
}
