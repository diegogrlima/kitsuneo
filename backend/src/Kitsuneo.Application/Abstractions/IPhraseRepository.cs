using Kitsuneo.Application.Phrases;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Abstractions;

public interface IPhraseRepository
{
    Task AddAsync(Phrase phrase, CancellationToken cancellationToken = default);

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
}
