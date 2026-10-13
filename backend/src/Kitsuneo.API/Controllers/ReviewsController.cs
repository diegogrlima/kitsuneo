using System.ComponentModel.DataAnnotations;
using Kitsuneo.Application.Reviews;
using Microsoft.AspNetCore.Mvc;

namespace Kitsuneo.API.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class ReviewsController(ReviewService reviewService) : ControllerBase
{
    /// <summary>Phrases due for review now, most overdue first.</summary>
    [HttpGet("reviews/due")]
    [ProducesResponseType<IReadOnlyList<DueReviewResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<DueReviewResponse>>> ListDue(
        [FromQuery, Range(1, ReviewService.MaxLimit)] int limit = ReviewService.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var due = await reviewService.ListDueAsync(limit, cancellationToken);

        return Ok(due);
    }

    /// <summary>Records how well the phrase was remembered and returns its next schedule.</summary>
    [HttpPost("phrases/{id:guid}/reviews")]
    [ProducesResponseType<ReviewScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewScheduleResponse>> Review(
        Guid id,
        ReviewRequest request,
        CancellationToken cancellationToken)
    {
        var schedule = await reviewService.ReviewAsync(id, request, cancellationToken);

        return schedule is null ? NotFound() : Ok(schedule);
    }
}
