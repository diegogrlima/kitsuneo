using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Common;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Phrases;

public sealed class PhraseService(IPhraseRepository repository, ICurrentUser currentUser)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public async Task<PhraseResponse> CreateAsync(CreatePhraseRequest request, CancellationToken cancellationToken = default)
    {
        var phrase = Phrase.Create(
            currentUser.Id,
            request.Language,
            request.Text,
            request.SpokenForm,
            request.Reading,
            request.Translation);

        await repository.AddAsync(phrase, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return PhraseResponse.FromEntity(phrase);
    }

    /// <summary>Returns null when the phrase does not exist, is archived or belongs to another user.</summary>
    public async Task<PhraseResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var phrase = await repository.GetByIdAsync(id, cancellationToken);

        return phrase is null || phrase.UserId != currentUser.Id
            ? null
            : PhraseResponse.FromEntity(phrase);
    }

    /// <summary>Returns false when the phrase does not exist, is archived or belongs to another user.</summary>
    public async Task<bool> UpdateAsync(Guid id, UpdatePhraseRequest request, CancellationToken cancellationToken = default)
    {
        var content = PhraseContent.Create(
            request.Language,
            request.Text,
            request.SpokenForm,
            request.Reading,
            request.Translation);

        return await repository.UpdateAsync(id, currentUser.Id, content, cancellationToken);
    }

    /// <summary>Returns false when the phrase does not exist, is archived or belongs to another user.</summary>
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(id, currentUser.Id, cancellationToken);

    public async Task<CursorPage<PhraseResponse>> ListAsync(
        Language? language = null,
        string? search = null,
        PhraseCursor? after = null,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // One extra row tells whether there is a next page without a COUNT query
        var phrases = await repository.ListAsync(
            currentUser.Id, pageSize + 1, language, search, after, cancellationToken);

        var page = phrases.Take(pageSize).ToList();
        var nextCursor = phrases.Count > pageSize
            ? new PhraseCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        return new CursorPage<PhraseResponse>(page.Select(PhraseResponse.FromEntity).ToList(), nextCursor);
    }
}
