using System.ComponentModel.DataAnnotations;
using Kitsuneo.Application.Common;
using Kitsuneo.Application.Phrases;
using Kitsuneo.Core.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Kitsuneo.API.Controllers;

[ApiController]
[Route("api/v1/phrases")]
[Produces("application/json")]
public sealed class PhrasesController(PhraseService phraseService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PhraseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PhraseResponse>> Create(
        CreatePhraseRequest request,
        CancellationToken cancellationToken)
    {
        var phrase = await phraseService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = phrase.Id }, phrase);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PhraseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PhraseResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var phrase = await phraseService.GetAsync(id, cancellationToken);

        return phrase is null ? NotFound() : Ok(phrase);
    }

    /// <summary>Lists phrases newest first. Pass the returned nextCursor as cursor to get the next page.</summary>
    [HttpGet]
    [ProducesResponseType<CursorPage<PhraseResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CursorPage<PhraseResponse>>> List(
        [FromQuery] Language? language,
        [FromQuery] string? search,
        [FromQuery] string? cursor,
        [FromQuery, Range(1, PhraseService.MaxPageSize)] int pageSize = PhraseService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        PhraseCursor? after = null;
        if (cursor is not null && !PhraseCursor.TryDecode(cursor, out after))
        {
            ModelState.AddModelError(nameof(cursor), "The cursor is invalid.");
            return ValidationProblem(ModelState);
        }

        var phrases = await phraseService.ListAsync(language, search, after, pageSize, cancellationToken);

        return Ok(phrases);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(
        Guid id,
        UpdatePhraseRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await phraseService.UpdateAsync(id, request, cancellationToken);

        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await phraseService.DeleteAsync(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
