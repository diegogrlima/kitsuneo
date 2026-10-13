using Kitsuneo.Application.Practice;
using Kitsuneo.Core.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Kitsuneo.API.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class PracticeController(PracticeService practiceService) : ControllerBase
{
    /// <summary>Today's practice: due reviews, then new phrases up to the daily limit. Texts are left out.</summary>
    [HttpGet("practice/today")]
    [ProducesResponseType<TodayPracticeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TodayPracticeResponse>> GetToday(
        [FromQuery] Language? language,
        CancellationToken cancellationToken)
    {
        var today = await practiceService.GetTodayAsync(language, cancellationToken);

        return Ok(today);
    }

    /// <summary>Checks a dictation answer, saves the attempt and returns the correction with the next due date per rating.</summary>
    [HttpPost("phrases/{id:guid}/attempts")]
    [ProducesResponseType<AttemptResultResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttemptResultResponse>> SubmitAttempt(
        Guid id,
        SubmitAttemptRequest request,
        CancellationToken cancellationToken)
    {
        var result = await practiceService.SubmitAttemptAsync(
            id, request.Answer, request.PlayCount, request.UsedSlowSpeed, request.DurationMs, cancellationToken);

        // There is no endpoint to read an attempt back, so the 201 carries no Location
        return result is null ? NotFound() : StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Rates an attempt once, moving the phrase's schedule accordingly.</summary>
    [HttpPost("attempts/{id:guid}/rating")]
    [ProducesResponseType<RateAttemptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RateAttemptResponse>> RateAttempt(
        Guid id,
        RateAttemptRequest request,
        CancellationToken cancellationToken)
    {
        var result = await practiceService.RateAttemptAsync(id, request.Rating, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }
}
