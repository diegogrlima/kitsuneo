using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Tests.Entities;

public class RevokedAccessTokenTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_KeepsJtiUntilTokenExpires()
    {
        var userId = Guid.NewGuid();

        var revoked = RevokedAccessToken.Create("abc123", userId, Now.AddMinutes(10), Now);

        revoked.Jti.Should().Be("abc123");
        revoked.UserId.Should().Be(userId);
        revoked.RevokedAt.Should().Be(Now);
        revoked.ExpiresAt.Should().Be(Now.AddMinutes(10));
    }

    [Fact]
    public void Create_WithInvalidJti_ThrowsDomainException()
    {
        var empty = () => RevokedAccessToken.Create("", Guid.NewGuid(), Now, Now);
        var tooLong = () => RevokedAccessToken.Create(new string('a', 65), Guid.NewGuid(), Now, Now);

        empty.Should().Throw<DomainException>().WithMessage("Jti is required.");
        tooLong.Should().Throw<DomainException>().WithMessage("Jti must have at most 64 characters.");
    }
}
