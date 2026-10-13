using FluentAssertions;
using Kitsuneo.Application.Abstractions;
using Kitsuneo.Infrastructure.Security;

namespace Kitsuneo.Infrastructure.Tests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_IsSaltedAndNeverThePassword()
    {
        var first = _hasher.Hash("correct horse");
        var second = _hasher.Hash("correct horse");

        first.Should().NotContain("correct horse");
        first.Should().NotBe(second, "each hash gets its own random salt");
    }

    [Fact]
    public void Verify_WithRightPassword_Succeeds()
    {
        var hash = _hasher.Hash("correct horse");

        _hasher.Verify(hash, "correct horse").Should().Be(PasswordCheck.Success);
    }

    [Theory]
    [InlineData("Correct horse")]
    [InlineData("correct horse ")]
    [InlineData("")]
    public void Verify_WithWrongPassword_Fails(string password)
    {
        var hash = _hasher.Hash("correct horse");

        _hasher.Verify(hash, password).Should().Be(PasswordCheck.Failed);
    }

    [Fact]
    public void Verify_WithGarbageHash_Fails()
    {
        _hasher.Verify("not-a-hash", "correct horse").Should().Be(PasswordCheck.Failed);
    }
}
