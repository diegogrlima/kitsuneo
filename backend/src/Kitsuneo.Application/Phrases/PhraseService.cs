using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Users;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Phrases;

public sealed class PhraseService(IPhraseRepository repository)
{
    public async Task<PhraseResponse> CreateAsync(CreatePhraseRequest request, CancellationToken cancellationToken = default)
    {
        var phrase = Phrase.Create(
            DefaultUser.Id,
            request.Language,
            request.Text,
            request.SpokenForm,
            request.Reading,
            request.Translation);

        await repository.AddAsync(phrase, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return PhraseResponse.FromEntity(phrase);
    }

    /// <summary>Returns null when the phrase does not exist or belongs to another user.</summary>
    public async Task<PhraseResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var phrase = await repository.GetByIdAsync(id, cancellationToken);

        return phrase is null || phrase.UserId != DefaultUser.Id
            ? null
            : PhraseResponse.FromEntity(phrase);
    }

    public async Task<IReadOnlyList<PhraseResponse>> ListAsync(
        Language? language = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var phrases = await repository.ListAsync(DefaultUser.Id, language, search, cancellationToken);

        return phrases.Select(PhraseResponse.FromEntity).ToList();
    }
}
