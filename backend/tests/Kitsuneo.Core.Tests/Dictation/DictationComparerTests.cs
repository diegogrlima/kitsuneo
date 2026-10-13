using FluentAssertions;
using Kitsuneo.Core.Dictation;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Tests.Dictation;

public class DictationComparerTests
{
    private static DictationToken Correct(string token) => new(DictationTokenStatus.Correct, token, token);
    private static DictationToken Wrong(string expected, string actual) => new(DictationTokenStatus.Wrong, expected, actual);
    private static DictationToken Missing(string expected) => new(DictationTokenStatus.Missing, expected, null);
    private static DictationToken Extra(string actual) => new(DictationTokenStatus.Extra, null, actual);

    [Fact]
    public void Compare_English_WithExactAnswer_ReturnsAllCorrect()
    {
        var result = DictationComparer.Compare(Language.English, "I like cats", "I like cats");

        result.Tokens.Should().Equal(Correct("i"), Correct("like"), Correct("cats"));
        result.Accuracy.Should().Be(100);
    }

    [Fact]
    public void Compare_English_IgnoresCasePunctuationAndExtraSpaces()
    {
        var result = DictationComparer.Compare(Language.English, "Hello, world!", "  hello   WORLD ");

        result.Tokens.Should().Equal(Correct("hello"), Correct("world"));
        result.Accuracy.Should().Be(100);
    }

    [Theory]
    [InlineData("don't")]
    [InlineData("dont")]
    [InlineData("don’t")]
    public void Compare_English_IgnoresApostrophes(string actual)
    {
        var result = DictationComparer.Compare(Language.English, "I don't know", $"I {actual} know");

        result.Accuracy.Should().Be(100);
    }

    [Fact]
    public void Compare_English_TreatsHyphenAsWordSeparator()
    {
        var result = DictationComparer.Compare(Language.English, "a well-known song", "a well known song");

        result.Tokens.Should().Equal(Correct("a"), Correct("well"), Correct("known"), Correct("song"));
    }

    [Fact]
    public void Compare_English_WithWrongWord_MarksWrong()
    {
        var result = DictationComparer.Compare(Language.English, "I like cats", "I like cat");

        result.Tokens.Should().Equal(Correct("i"), Correct("like"), Wrong("cats", "cat"));
        result.Accuracy.Should().Be(66.67);
    }

    [Fact]
    public void Compare_English_WithSkippedWord_MarksMissing()
    {
        var result = DictationComparer.Compare(Language.English, "I really like cats", "I like cats");

        result.Tokens.Should().Equal(Correct("i"), Missing("really"), Correct("like"), Correct("cats"));
        result.Accuracy.Should().Be(75);
    }

    [Fact]
    public void Compare_English_WithAddedWord_MarksExtraAndLowersAccuracy()
    {
        var result = DictationComparer.Compare(Language.English, "I like cats", "I like the cats");

        result.Tokens.Should().Equal(Correct("i"), Correct("like"), Extra("the"), Correct("cats"));
        result.Accuracy.Should().Be(75);
    }

    [Fact]
    public void Compare_English_WithMixedErrors_AlignsEachWord()
    {
        var result = DictationComparer.Compare(Language.English, "the quick brown fox jumps", "a quick fox jumps high");

        result.Tokens.Should().Equal(
            Wrong("the", "a"),
            Correct("quick"),
            Missing("brown"),
            Correct("fox"),
            Correct("jumps"),
            Extra("high"));
        result.Accuracy.Should().Be(50);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?!")]
    public void Compare_WithEmptyAnswer_MarksEverythingMissing(string actual)
    {
        var result = DictationComparer.Compare(Language.English, "I like cats", actual);

        result.Tokens.Should().Equal(Missing("i"), Missing("like"), Missing("cats"));
        result.Accuracy.Should().Be(0);
    }

    [Fact]
    public void Compare_WithNullAnswer_MarksEverythingMissing()
    {
        var result = DictationComparer.Compare(Language.English, "cats", null!);

        result.Tokens.Should().Equal(Missing("cats"));
        result.Accuracy.Should().Be(0);
    }

    [Fact]
    public void Compare_Japanese_WithExactReading_ComparesCharacterByCharacter()
    {
        var result = DictationComparer.Compare(Language.Japanese, "ねこがすき", "ねこがすき");

        result.Tokens.Should().Equal(Correct("ね"), Correct("こ"), Correct("が"), Correct("す"), Correct("き"));
        result.Accuracy.Should().Be(100);
    }

    [Theory]
    [InlineData("ラーメン", "らーめん")]
    [InlineData("らーめん", "ラーメン")]
    [InlineData("ラーメン", "ﾗｰﾒﾝ")]
    [InlineData("ヴァイオリン", "ゔぁいおりん")]
    public void Compare_Japanese_TreatsHiraganaAndKatakanaAsEqual(string expected, string actual)
    {
        var result = DictationComparer.Compare(Language.Japanese, expected, actual);

        result.Tokens.Should().OnlyContain(t => t.Status == DictationTokenStatus.Correct);
        result.Accuracy.Should().Be(100);
    }

    [Fact]
    public void Compare_Japanese_ComposesDecomposedDakuten()
    {
        var result = DictationComparer.Compare(Language.Japanese, "が", "が");

        result.Tokens.Should().Equal(Correct("が"));
    }

    [Fact]
    public void Compare_Japanese_IgnoresSpacesAndPunctuation()
    {
        var result = DictationComparer.Compare(Language.Japanese, "はい、そうです。", "はい そうです！");

        result.Tokens.Should().HaveCount(6).And.OnlyContain(t => t.Status == DictationTokenStatus.Correct);
    }

    [Fact]
    public void Compare_Japanese_WithWrongCharacter_MarksWrong()
    {
        var result = DictationComparer.Compare(Language.Japanese, "ねこがすき", "ねこがすぎ");

        result.Tokens.Should().Equal(Correct("ね"), Correct("こ"), Correct("が"), Correct("す"), Wrong("き", "ぎ"));
        result.Accuracy.Should().Be(80);
    }

    [Fact]
    public void Compare_Japanese_WithMissingAndExtraCharacters_AlignsEachCharacter()
    {
        var result = DictationComparer.Compare(Language.Japanese, "とうきょう", "ときょうう");

        result.Tokens.Should().Equal(
            Correct("と"), Missing("う"), Correct("き"), Correct("ょ"), Correct("う"), Extra("う"));
        result.Accuracy.Should().Be(66.67);
    }

    [Fact]
    public void Compare_Japanese_WithKanjiInsteadOfKana_MarksWrong()
    {
        var result = DictationComparer.Compare(Language.Japanese, "ねこ", "猫");

        result.Tokens.Should().Equal(Wrong("ね", "猫"), Missing("こ"));
        result.Accuracy.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("。")]
    public void Compare_WithEmptyExpected_ThrowsDomainException(string expected)
    {
        var act = () => DictationComparer.Compare(Language.Japanese, expected, "ねこ");

        act.Should().Throw<DomainException>().WithMessage("Expected text is required.");
    }

    [Fact]
    public void Compare_WithInvalidLanguage_ThrowsDomainException()
    {
        var act = () => DictationComparer.Compare((Language)99, "cats", "cats");

        act.Should().Throw<DomainException>().WithMessage("Invalid language.");
    }
}
