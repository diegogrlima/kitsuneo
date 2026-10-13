using Kitsuneo.Core.Entities;

namespace Kitsuneo.Application.Abstractions;

public interface IReviewRepository
{
    /// <summary>Active phrases of <paramref name="userId"/> due at <paramref name="now"/>, most overdue first.</summary>
    Task<IReadOnlyList<Phrase>> ListDueAsync(Guid userId, DateTime now, int limit, CancellationToken cancellationToken = default);

    /// <summary>The phrase tracked for changes, with its schedule. Returns null when no active phrase of <paramref name="userId"/> has <paramref name="id"/>.</summary>
    Task<Phrase?> GetForReviewAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
