using Kitsuneo.Application.Phrases;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Abstractions;

public interface IPhraseRepository
{
    Task AddAsync(Phrase phrase, CancellationToken cancellationToken = default);

    /// <summary>Returns null when the phrase does not exist or is archived.</summary>
    Task<Phrase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns up to <paramref name="limit"/> phrases that come after <paramref name="after"/>, newest first.</summary>
    Task<IReadOnlyList<Phrase>> ListAsync(
        Guid userId,
        int limit,
        Language? language = null,
        string? search = null,
        PhraseCursor? after = null,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes straight to the database, without SaveChangesAsync. Returns false when no active phrase of <paramref name="userId"/> has <paramref name="id"/>.</summary>
    Task<bool> UpdateAsync(Guid id, Guid userId, PhraseContent content, CancellationToken cancellationToken = default);

    /// <summary>Soft delete: archives the phrase straight in the database, without SaveChangesAsync. Returns false when no active phrase of <paramref name="userId"/> has <paramref name="id"/>.</summary>
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
