using Kitsuneo.Application.Speech;
using Microsoft.AspNetCore.Mvc;

namespace Kitsuneo.API.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class SpeechController(PhraseAudioService phraseAudioService) : ControllerBase
{
    /// <summary>The phrase read aloud by an AI voice (MP3). Lets the dictation be played without revealing the text.</summary>
    [HttpGet("phrases/{id:guid}/audio")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, "audio/mpeg")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult> GetAudio(
        Guid id,
        [FromQuery] bool slow = false,
        CancellationToken cancellationToken = default)
    {
        var audio = await phraseAudioService.GetAsync(id, slow, cancellationToken);

        return audio is null ? NotFound() : File(audio.Content, audio.ContentType);
    }
}
