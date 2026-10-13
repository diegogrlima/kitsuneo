using System.Net;
using System.Text.Json;
using FluentAssertions;
using Kitsuneo.Application.Speech;
using Kitsuneo.Core.Enums;
using Kitsuneo.Infrastructure.Speech;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kitsuneo.Infrastructure.Tests.Speech;

// Talks to a fake handler: no request ever reaches ElevenLabs
public sealed class ElevenLabsTextToSpeechTests : IDisposable
{
    private static readonly byte[] Mp3 = [0x49, 0x44, 0x33, 0x04];

    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), $"kitsuneo-audio-tests-{Guid.NewGuid():N}");
    private readonly FakeHandler _handler = new();

    public void Dispose()
    {
        if (Directory.Exists(_cacheDirectory))
            Directory.Delete(_cacheDirectory, recursive: true);
    }

    [Fact]
    public async Task SynthesizeAsync_SendsDocumentedRequest()
    {
        var tts = CreateTextToSpeech();

        var audio = await tts.SynthesizeAsync("Nice to meet you", Language.English, slow: false);

        audio.Should().BeEquivalentTo(new SpeechAudio(Mp3, "audio/mpeg"));
        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be("https://api.example.test/v1/text-to-speech/default-voice?output_format=mp3_44100_128");
        request.ApiKey.Should().Be("test-key");

        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetProperty("text").GetString().Should().Be("Nice to meet you");
        body.RootElement.GetProperty("model_id").GetString().Should().Be("eleven_flash_v2_5");
        body.RootElement.GetProperty("language_code").GetString().Should().Be("en");
        body.RootElement.GetProperty("voice_settings").GetProperty("speed").GetDouble().Should().Be(1.0);
    }

    [Fact]
    public async Task SynthesizeAsync_JapaneseSlow_UsesLanguageVoiceAndSlowSpeed()
    {
        var tts = CreateTextToSpeech(o => o.Voices[Language.Japanese] = "japanese-voice");

        await tts.SynthesizeAsync("ねこがすきです", Language.Japanese, slow: true);

        var request = _handler.Requests.Should().ContainSingle().Subject;
        request.Uri.Should().StartWith("https://api.example.test/v1/text-to-speech/japanese-voice?");
        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetProperty("language_code").GetString().Should().Be("ja");
        body.RootElement.GetProperty("voice_settings").GetProperty("speed").GetDouble().Should().Be(0.7);
    }

    [Fact]
    public async Task SynthesizeAsync_SameRequestTwice_CallsApiOnce()
    {
        var tts = CreateTextToSpeech();

        await tts.SynthesizeAsync("Hello", Language.English, slow: false);
        var cached = await tts.SynthesizeAsync("Hello", Language.English, slow: false);

        cached.Content.Should().Equal(Mp3);
        _handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task SynthesizeAsync_DifferentTextOrSpeed_CallsApiAgain()
    {
        var tts = CreateTextToSpeech();

        await tts.SynthesizeAsync("Hello", Language.English, slow: false);
        await tts.SynthesizeAsync("Hello", Language.English, slow: true);
        await tts.SynthesizeAsync("Hello!", Language.English, slow: false);

        _handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task SynthesizeAsync_WithoutApiKey_ThrowsWithoutCallingApi()
    {
        var tts = CreateTextToSpeech(o => o.ApiKey = null);

        var act = () => tts.SynthesizeAsync("Hello", Language.English, slow: false);

        await act.Should().ThrowAsync<TextToSpeechUnavailableException>()
            .WithMessage("Text-to-speech is not configured: set ElevenLabs:ApiKey.");
        _handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SynthesizeAsync_WhenApiRefuses_ThrowsAndCachesNothing(HttpStatusCode status)
    {
        _handler.Respond = _ => new HttpResponseMessage(status) { Content = new StringContent("""{"detail":"quota_exceeded"}""") };
        var tts = CreateTextToSpeech();

        var act = () => tts.SynthesizeAsync("Hello", Language.English, slow: false);

        await act.Should().ThrowAsync<TextToSpeechUnavailableException>()
            .WithMessage($"ElevenLabs refused the request ({(int)status}).");
        Directory.Exists(_cacheDirectory).Should().BeFalse();
    }

    [Fact]
    public async Task SynthesizeAsync_WhenApiUnreachable_Throws()
    {
        _handler.Respond = _ => throw new HttpRequestException("connection refused");
        var tts = CreateTextToSpeech();

        var act = () => tts.SynthesizeAsync("Hello", Language.English, slow: false);

        await act.Should().ThrowAsync<TextToSpeechUnavailableException>().WithMessage("Could not reach ElevenLabs.");
    }

    private ElevenLabsTextToSpeech CreateTextToSpeech(Action<ElevenLabsOptions>? configure = null)
    {
        var options = new ElevenLabsOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.example.test/",
            VoiceId = "default-voice",
            CacheDirectory = _cacheDirectory
        };
        configure?.Invoke(options);

        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri(options.BaseUrl) };
        return new ElevenLabsTextToSpeech(httpClient, Options.Create(options), NullLogger<ElevenLabsTextToSpeech>.Instance);
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string? ApiKey, string Body);

    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Mp3) };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.TryGetValues("xi-api-key", out var keys) ? keys.Single() : null,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));

            return Respond(request);
        }
    }
}
