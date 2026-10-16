using Kitsuneo.Application.Abstractions;

namespace Kitsuneo.Application.Speech;

public sealed class PhraseAudioService(
    IPhraseRepository phraseRepository,
    ITextToSpeech textToSpeech,
    ICurrentUser currentUser)
{
    /// <summary>
    /// The phrase's <c>SpokenForm</c> read aloud. Returns null when the phrase does not exist,
    /// is archived or belongs to another user.
    /// </summary>
    public async Task<SpeechAudio?> GetAsync(Guid phraseId, bool slow, CancellationToken cancellationToken = default)
    {
        var phrase = await phraseRepository.GetByIdAsync(phraseId, cancellationToken);
        if (phrase is null || phrase.UserId != currentUser.Id)
            return null;

        return await textToSpeech.SynthesizeAsync(phrase.SpokenForm, phrase.Language, slow, cancellationToken);
    }
}
