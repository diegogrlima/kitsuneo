using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;

namespace Kitsuneo.Core.Repositories;

public interface IPhraseRepository
{
    Task<Phrase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Phrase>> ListByUserAsync(
        Guid userId,
        Language? language = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Phrase phrase, CancellationToken cancellationToken = default);
}
