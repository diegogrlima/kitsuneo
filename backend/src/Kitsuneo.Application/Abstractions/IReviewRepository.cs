using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Abstractions;

public interface IReviewRepository
{
    /// <summary>Active phrases of <paramref name="userId"/> due at <paramref name="now"/>, most overdue first.</summary>
    Task<IReadOnlyList<Phrase>> ListDueAsync(Guid userId, DateTime now, int limit, CancellationToken cancellationToken = default);

    /// <summary>Active phrases of <paramref name="userId"/> already reviewed at least once and due at <paramref name="now"/>, most overdue first.</summary>
    Task<IReadOnlyList<Phrase>> ListDueReviewsAsync(
        Guid userId,
        DateTime now,
        Language? language = null,
        CancellationToken cancellationToken = default);

    /// <summary>Up to <paramref name="limit"/> active phrases of <paramref name="userId"/> never reviewed, oldest first.</summary>
    Task<IReadOnlyList<Phrase>> ListNewAsync(
        Guid userId,
        int limit,
        Language? language = null,
        CancellationToken cancellationToken = default);

    /// <summary>The phrase tracked for changes, with its schedule. Returns null when no active phrase of <paramref name="userId"/> has <paramref name="id"/>.</summary>
    Task<Phrase?> GetForReviewAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
