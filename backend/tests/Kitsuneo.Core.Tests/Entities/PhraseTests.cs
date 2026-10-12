using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Tests.Entities;

public class PhraseTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ReturnsPhrase()
    {
        var before = DateTime.UtcNow;

        var phrase = Phrase.Create(UserId, Language.Japanese, "猫が好きです", "ねこがすきです", "ねこがすきです", "I like cats");

        var after = DateTime.UtcNow;
        phrase.Id.Should().NotBeEmpty();
        phrase.Id.Version.Should().Be(7);
        phrase.UserId.Should().Be(UserId);
        phrase.Language.Should().Be(Language.Japanese);
        phrase.Text.Should().Be("猫が好きです");
        phrase.SpokenForm.Should().Be("ねこがすきです");
        phrase.Reading.Should().Be("ねこがすきです");
        phrase.Translation.Should().Be("I like cats");
        phrase.IsArchived.Should().BeFalse();
        phrase.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        phrase.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Create_TrimsText()
    {
        var phrase = Phrase.Create(UserId, Language.English, "  Hello world  ", "  hello  ", translation: "  Olá mundo  ");

        phrase.Text.Should().Be("Hello world");
        phrase.SpokenForm.Should().Be("hello");
        phrase.Translation.Should().Be("Olá mundo");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyText_ThrowsDomainException(string? text)
    {
        var act = () => Phrase.Create(UserId, Language.English, text!);

        act.Should().Throw<DomainException>().WithMessage("Text is required.");
    }

    [Fact]
    public void Create_WithTextOver500Chars_ThrowsDomainException()
    {
        var text = new string('a', Phrase.MaxLength + 1);

        var act = () => Phrase.Create(UserId, Language.English, text);

        act.Should().Throw<DomainException>().WithMessage("Text must have at most 500 characters.");
    }

    [Fact]
    public void Create_WithTextOf500Chars_ReturnsPhrase()
    {
        var text = new string('a', Phrase.MaxLength);

        var phrase = Phrase.Create(UserId, Language.English, text);

        phrase.Text.Should().HaveLength(Phrase.MaxLength);
    }

    [Fact]
    public void Create_WithTextOver500CharsOnlyBecauseOfWhitespace_ReturnsPhrase()
    {
        var text = $"  {new string('a', Phrase.MaxLength)}  ";

        var phrase = Phrase.Create(UserId, Language.English, text);

        phrase.Text.Should().HaveLength(Phrase.MaxLength);
    }

    [Theory]
    [InlineData(nameof(Phrase.SpokenForm))]
    [InlineData(nameof(Phrase.Reading))]
    [InlineData(nameof(Phrase.Translation))]
    public void Create_WithOptionalFieldOver500Chars_ThrowsDomainException(string field)
    {
        var tooLong = new string('a', Phrase.MaxLength + 1);

        var act = () => Phrase.Create(
            UserId,
            Language.Japanese,
            "テキスト",
            spokenForm: field == nameof(Phrase.SpokenForm) ? tooLong : null,
            reading: field == nameof(Phrase.Reading) ? tooLong : null,
            translation: field == nameof(Phrase.Translation) ? tooLong : null);

        act.Should().Throw<DomainException>().WithMessage($"{field} must have at most 500 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutSpokenForm_UsesText(string? spokenForm)
    {
        var phrase = Phrase.Create(UserId, Language.English, " Good morning ", spokenForm);

        phrase.SpokenForm.Should().Be("Good morning");
    }

    [Fact]
    public void Create_WithBlankOptionalFields_SetsThemToNull()
    {
        var phrase = Phrase.Create(UserId, Language.Japanese, "こんにちは", reading: "   ", translation: "");

        phrase.Reading.Should().BeNull();
        phrase.Translation.Should().BeNull();
    }

    [Fact]
    public void Create_WithReadingForJapanese_SetsReading()
    {
        var phrase = Phrase.Create(UserId, Language.Japanese, "日本語", reading: " にほんご ");

        phrase.Reading.Should().Be("にほんご");
    }

    [Fact]
    public void Create_WithReadingForEnglish_ThrowsDomainException()
    {
        var act = () => Phrase.Create(UserId, Language.English, "Hello", reading: "heh-loh");

        act.Should().Throw<DomainException>().WithMessage("Reading is only supported for Japanese phrases.");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsDomainException()
    {
        var act = () => Phrase.Create(Guid.Empty, Language.English, "Hello");

        act.Should().Throw<DomainException>().WithMessage("UserId is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Create_WithUndefinedLanguage_ThrowsDomainException(int language)
    {
        var act = () => Phrase.Create(UserId, (Language)language, "Hello");

        act.Should().Throw<DomainException>().WithMessage("Invalid language.");
    }
}
