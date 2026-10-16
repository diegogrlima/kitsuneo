using FluentAssertions;
using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Speech;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using NSubstitute;

namespace Kitsuneo.Application.Tests.Speech;

public class PhraseAudioServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly SpeechAudio Audio = new([1, 2, 3], "audio/mpeg");

    private readonly IPhraseRepository _phrases = Substitute.For<IPhraseRepository>();
    private readonly ITextToSpeech _textToSpeech = Substitute.For<ITextToSpeech>();
    private readonly PhraseAudioService _service;

    public PhraseAudioServiceTests()
    {
        _service = new PhraseAudioService(_phrases, _textToSpeech, new FakeCurrentUser(UserId));
        _textToSpeech.SynthesizeAsync(default!, default, default, default).ReturnsForAnyArgs(Audio);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAsync_ReadsSpokenFormInPhraseLanguage(bool slow)
    {
        var phrase = Phrase.Create(UserId, Language.Japanese, "猫が好きです", spokenForm: "ねこがすきです");
        _phrases.GetByIdAsync(phrase.Id, Arg.Any<CancellationToken>()).Returns(phrase);

        var audio = await _service.GetAsync(phrase.Id, slow);

        audio.Should().BeSameAs(Audio);
        await _textToSpeech.Received(1).SynthesizeAsync("ねこがすきです", Language.Japanese, slow, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_WithArchivedOrUnknownPhrase_ReturnsNullWithoutSpending()
    {
        // The repository hides archived phrases, so they look the same as unknown ones
        var audio = await _service.GetAsync(Guid.NewGuid(), slow: false);

        audio.Should().BeNull();
        await _textToSpeech.DidNotReceiveWithAnyArgs().SynthesizeAsync(default!, default, default, default);
    }

    [Fact]
    public async Task GetAsync_WithOtherUsersPhrase_ReturnsNullWithoutSpending()
    {
        var phrase = Phrase.Create(Guid.NewGuid(), Language.English, "Not mine");
        _phrases.GetByIdAsync(phrase.Id, Arg.Any<CancellationToken>()).Returns(phrase);

        var audio = await _service.GetAsync(phrase.Id, slow: false);

        audio.Should().BeNull();
        await _textToSpeech.DidNotReceiveWithAnyArgs().SynthesizeAsync(default!, default, default, default);
    }
}
