using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kitsuneo.Infrastructure.Tests.Security;

public class JwtAccessTokenIssuerTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = new string('k', 32),
        AccessTokenLifetime = TimeSpan.FromMinutes(15)
    };

    private readonly JwtAccessTokenIssuer _issuer = new(Microsoft.Extensions.Options.Options.Create(Options));

    [Fact]
    public async Task Issue_CreatesSignedTokenWithUserClaims()
    {
        var user = User.Create("ana@example.com", "hash");
        var now = DateTime.UtcNow;

        var token = _issuer.Issue(user, now);

        token.ExpiresAt.Should().Be(now.AddMinutes(15));
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, ValidationParameters(Options.CreateSigningKey()));
        result.IsValid.Should().BeTrue();
        result.Claims[JwtRegisteredClaimNames.Sub].Should().Be(user.Id.ToString());
        result.Claims[JwtRegisteredClaimNames.Email].Should().Be("ana@example.com");
        result.Claims[JwtRegisteredClaimNames.Jti].Should().Be(token.Jti);
        ((JsonWebToken)result.SecurityToken).ValidTo.Should().BeCloseTo(token.ExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Issue_GivesEachTokenItsOwnJti()
    {
        var user = User.Create("ana@example.com", "hash");

        var first = _issuer.Issue(user, DateTime.UtcNow);
        var second = _issuer.Issue(user, DateTime.UtcNow);

        second.Jti.Should().NotBe(first.Jti);
    }

    [Fact]
    public async Task Issue_TokenFailsValidationWithAnotherKey()
    {
        var token = _issuer.Issue(User.Create("ana@example.com", "hash"), DateTime.UtcNow);
        var otherKey = new SymmetricSecurityKey(new byte[32]);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, ValidationParameters(otherKey));

        result.IsValid.Should().BeFalse();
    }

    private static TokenValidationParameters ValidationParameters(SecurityKey key) => new()
    {
        ValidIssuer = Options.Issuer,
        ValidAudience = Options.Audience,
        IssuerSigningKey = key
    };
}
