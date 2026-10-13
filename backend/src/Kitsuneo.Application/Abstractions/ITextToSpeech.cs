using Kitsuneo.Application.Speech;
using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Abstractions;

public interface ITextToSpeech
{
    /// <summary>Reads <paramref name="text"/> aloud in <paramref name="language"/>, slower when <paramref name="slow"/> is set.</summary>
    /// <exception cref="TextToSpeechUnavailableException">The provider is not configured, refused the request or could not be reached.</exception>
    Task<SpeechAudio> SynthesizeAsync(string text, Language language, bool slow, CancellationToken cancellationToken = default);
}
