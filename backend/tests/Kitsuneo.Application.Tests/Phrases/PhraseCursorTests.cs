using FluentAssertions;
using Kitsuneo.Application.Phrases;

namespace Kitsuneo.Application.Tests.Phrases;

public class PhraseCursorTests
{
    [Fact]
    public void Encode_ThenTryDecode_ReturnsSameCursor()
    {
        var cursor = new PhraseCursor(new DateTime(2026, 10, 12, 20, 26, 20, DateTimeKind.Utc).AddTicks(1234), Guid.CreateVersion7());

        PhraseCursor.TryDecode(cursor.Encode(), out var decoded).Should().BeTrue();

        decoded.Should().Be(cursor);
        decoded!.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Encode_ProducesUrlSafeValue()
    {
        var encoded = new PhraseCursor(DateTime.UtcNow, Guid.NewGuid()).Encode();

        encoded.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor!")]
    [InlineData("MTIz")]                                              // "123": no separator
    [InlineData("YWJjXzAxOTlkOGE0N2MzZTcwMDA4MDAwMDAwMDAwMDAwMDAx")] // "abc_<guid>": ticks not a number
    [InlineData("MTIzX25vdC1hLWd1aWQ")]                               // "123_not-a-guid"
    [InlineData("NDAwMDAwMDAwMDAwMDAwMDAwMF8wMTk5ZDhhNDdjM2U3MDAwODAwMDAwMDAwMDAwMDAwMQ")] // ticks beyond DateTime.MaxValue
    public void TryDecode_WithInvalidValue_ReturnsFalse(string value)
    {
        PhraseCursor.TryDecode(value, out var cursor).Should().BeFalse();

        cursor.Should().BeNull();
    }
}
