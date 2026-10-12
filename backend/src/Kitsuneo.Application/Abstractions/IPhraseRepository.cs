using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Abstractions;

public interface IPhraseRepository
{
    Task AddAsync(Phrase phrase, CancellationToken cancellationToken = default);

    Task<Phrase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Phrase>> ListAsync(
        Guid userId,
        Language? language = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
