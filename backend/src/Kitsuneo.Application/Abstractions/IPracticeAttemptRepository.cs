using Kitsuneo.Core.Entities;

namespace Kitsuneo.Application.Abstractions;

public interface IPracticeAttemptRepository
{
    Task AddAsync(PracticeAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>The attempt tracked for changes. Returns null when no attempt of <paramref name="userId"/> has <paramref name="id"/>.</summary>
    Task<PracticeAttempt?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>How many phrases of <paramref name="userId"/> had their first attempt at or after <paramref name="since"/>.</summary>
    Task<int> CountPhrasesFirstAttemptedSinceAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
