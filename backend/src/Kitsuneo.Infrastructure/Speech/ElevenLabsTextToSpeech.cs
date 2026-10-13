using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Speech;
using Kitsuneo.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kitsuneo.Infrastructure.Speech;

/// <summary>
/// Text-to-speech through the ElevenLabs API (https://elevenlabs.io/docs/api-reference/text-to-speech/convert).
/// Audio is cached on disk by everything that shapes it, so editing a phrase or changing a setting generates it again.
/// </summary>
public sealed class ElevenLabsTextToSpeech(
    HttpClient httpClient,
    IOptions<ElevenLabsOptions> options,
    ILogger<ElevenLabsTextToSpeech> logger) : ITextToSpeech
{
    private const string ContentType = "audio/mpeg";
    private const double NormalSpeed = 1.0;

    private readonly ElevenLabsOptions _options = options.Value;

    public async Task<SpeechAudio> SynthesizeAsync(
        string text,
        Language language,
        bool slow,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new TextToSpeechUnavailableException("Text-to-speech is not configured: set ElevenLabs:ApiKey.");

        var voiceId = _options.Voices.GetValueOrDefault(language, _options.VoiceId);
        var speed = slow ? _options.SlowSpeed : NormalSpeed;

        var cachePath = CachePath(text, language, voiceId, speed);
        if (File.Exists(cachePath))
            return new SpeechAudio(await File.ReadAllBytesAsync(cachePath, cancellationToken), ContentType);

        var content = await RequestAsync(text, language, voiceId, speed, cancellationToken);
        await WriteCacheAsync(cachePath, content, cancellationToken);

        return new SpeechAudio(content, ContentType);
    }

    private async Task<byte[]> RequestAsync(
        string text,
        Language language,
        string voiceId,
        double speed,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1/text-to-speech/{Uri.EscapeDataString(voiceId)}?output_format={Uri.EscapeDataString(_options.OutputFormat)}");
        request.Headers.Add("xi-api-key", _options.ApiKey);
        request.Content = JsonContent.Create(new SpeechRequest(
            text, _options.ModelId, LanguageCode(language), new VoiceSettings(speed)));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new TextToSpeechUnavailableException("Could not reach ElevenLabs.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // 401 covers both a bad key and an exhausted quota; the body says which
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("ElevenLabs returned {StatusCode}: {Body}", (int)response.StatusCode, body);

                throw new TextToSpeechUnavailableException($"ElevenLabs refused the request ({(int)response.StatusCode}).");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
    }

    private string CachePath(string text, Language language, string voiceId, double speed)
    {
        var key = string.Join('\n',
            _options.ModelId, voiceId, _options.OutputFormat, language, speed.ToString(CultureInfo.InvariantCulture), text);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

        return Path.Combine(_options.CacheDirectory, $"{hash}.mp3");
    }

    // Writes to a temporary file first, so a concurrent read never sees half a file
    private async Task WriteCacheAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_options.CacheDirectory);
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (IOException ex)
        {
            // The audio is still returned; it is just generated again next time
            logger.LogWarning(ex, "Could not cache speech audio at {Path}", path);
        }
    }

    private static string LanguageCode(Language language) => language switch
    {
        Language.Japanese => "ja",
        _ => "en"
    };

    private sealed record SpeechRequest(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("model_id")] string ModelId,
        [property: JsonPropertyName("language_code")] string LanguageCode,
        [property: JsonPropertyName("voice_settings")] VoiceSettings VoiceSettings);

    private sealed record VoiceSettings([property: JsonPropertyName("speed")] double Speed);
}
