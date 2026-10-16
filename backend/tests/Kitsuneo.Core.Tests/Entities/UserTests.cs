using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Create_WithValidData_ReturnsUser()
    {
        var before = DateTime.UtcNow;

        var user = User.Create("ana@example.com", "hash");

        user.Id.Version.Should().Be(7);
        user.Email.Should().Be("ana@example.com");
        user.PasswordHash.Should().Be("hash");
        user.CreatedAt.Should().BeOnOrAfter(before);
        user.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Create_NormalizesEmail()
    {
        var user = User.Create("  Ana.Silva@Example.COM ", "hash");

        user.Email.Should().Be("ana.silva@example.com");
    }

    [Theory]
    [InlineData(null, "Email is required.")]
    [InlineData("   ", "Email is required.")]
    [InlineData("ana", "Email is invalid.")]
    [InlineData("ana@", "Email is invalid.")]
    [InlineData("@example.com", "Email is invalid.")]
    [InlineData("Ana <ana@example.com>", "Email is invalid.")]
    public void Create_WithInvalidEmail_ThrowsDomainException(string? email, string message)
    {
        var act = () => User.Create(email!, "hash");

        act.Should().Throw<DomainException>().WithMessage(message);
    }

    [Fact]
    public void Create_WithEmailOver254Chars_ThrowsDomainException()
    {
        var email = new string('a', User.MaxEmailLength - "@example.com".Length + 1) + "@example.com";

        var act = () => User.Create(email, "hash");

        act.Should().Throw<DomainException>().WithMessage("Email must have at most 254 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WithoutPasswordHash_ThrowsDomainException(string? hash)
    {
        var act = () => User.Create("ana@example.com", hash!);

        act.Should().Throw<DomainException>().WithMessage("PasswordHash is required.");
    }

    [Fact]
    public void ChangePasswordHash_ReplacesHash()
    {
        var user = User.Create("ana@example.com", "old");

        user.ChangePasswordHash("new");

        user.PasswordHash.Should().Be("new");
    }
}
