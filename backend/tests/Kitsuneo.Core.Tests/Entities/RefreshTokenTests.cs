using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Tests.Entities;

public class RefreshTokenTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    [Fact]
    public void Issue_WithoutFamily_StartsNewSession()
    {
        var token = RefreshToken.Issue(UserId, "hash", Now, Lifetime);

        token.Id.Version.Should().Be(7);
        token.UserId.Should().Be(UserId);
        token.FamilyId.Should().NotBeEmpty().And.NotBe(token.Id);
        token.TokenHash.Should().Be("hash");
        token.CreatedAt.Should().Be(Now);
        token.ExpiresAt.Should().Be(Now + Lifetime);
        token.RevokedAt.Should().BeNull();
        token.ReplacedById.Should().BeNull();
    }

    [Fact]
    public void Issue_WithFamily_ContinuesSession()
    {
        var first = RefreshToken.Issue(UserId, "first", Now, Lifetime);

        var next = RefreshToken.Issue(UserId, "next", Now, Lifetime, first.FamilyId);

        next.FamilyId.Should().Be(first.FamilyId);
    }

    [Fact]
    public void IsActive_BeforeExpiry_IsTrueAndAtExpiry_IsFalse()
    {
        var token = RefreshToken.Issue(UserId, "hash", Now, Lifetime);

        token.IsActive(Now + Lifetime - TimeSpan.FromSeconds(1)).Should().BeTrue();
        token.IsActive(Now + Lifetime).Should().BeFalse();
    }

    [Fact]
    public void Issue_WithInvalidData_ThrowsDomainException()
    {
        var withoutUser = () => RefreshToken.Issue(Guid.Empty, "hash", Now, Lifetime);
        var withoutHash = () => RefreshToken.Issue(UserId, " ", Now, Lifetime);
        var withoutLifetime = () => RefreshToken.Issue(UserId, "hash", Now, TimeSpan.Zero);

        withoutUser.Should().Throw<DomainException>().WithMessage("UserId is required.");
        withoutHash.Should().Throw<DomainException>().WithMessage("TokenHash is required.");
        withoutLifetime.Should().Throw<DomainException>().WithMessage("Lifetime must be positive.");
    }
}
