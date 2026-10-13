namespace Kitsuneo.Application.Speech;

/// <summary>Speech could not be generated for reasons outside the request (missing key, quota, provider down).</summary>
public sealed class TextToSpeechUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{

}
