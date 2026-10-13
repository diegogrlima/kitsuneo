using Kitsuneo.Core.Enums;

namespace Kitsuneo.Infrastructure.Speech;

/// <summary>Settings bound from the "ElevenLabs" section. The API key belongs in user-secrets, never in appsettings.</summary>
public sealed class ElevenLabsOptions
{
    public const string SectionName = "ElevenLabs";

    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.elevenlabs.io/";

    /// <summary>Flash v2.5 speaks English and Japanese and costs half the credits of the other models.</summary>
    public string ModelId { get; set; } = "eleven_flash_v2_5";

    /// <summary>A default (premade) voice, which the free plan can use through the API.</summary>
    public string VoiceId { get; set; } = "JBFqnCBsd6RMkjVDRZzb";

    /// <summary>Per-language voice, overriding <see cref="VoiceId"/>.</summary>
    public Dictionary<Language, string> Voices { get; set; } = [];

    public string OutputFormat { get; set; } = "mp3_44100_128";

    /// <summary>Speed for slow playback; ElevenLabs accepts 0.7 (slowest) to 1.2.</summary>
    public double SlowSpeed { get; set; } = 0.7;

    /// <summary>Where generated audio is kept, so each phrase spends credits only once.</summary>
    public string CacheDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "kitsuneo-audio");
}
