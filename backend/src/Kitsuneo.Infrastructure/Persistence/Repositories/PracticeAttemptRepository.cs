using Kitsuneo.Application.Abstractions;
using Kitsuneo.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Persistence.Repositories;

public sealed class PracticeAttemptRepository(AppDbContext context) : IPracticeAttemptRepository
{
    public async Task AddAsync(PracticeAttempt attempt, CancellationToken cancellationToken = default) =>
        await context.PracticeAttempts.AddAsync(attempt, cancellationToken);

    public Task<PracticeAttempt?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        context.PracticeAttempts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

    // A phrase counts only if none of its attempts is older than `since`
    public Task<int> CountPhrasesFirstAttemptedSinceAsync(
        Guid userId,
        DateTime since,
        CancellationToken cancellationToken = default) =>
        context.PracticeAttempts
            .Where(a => a.UserId == userId)
            .GroupBy(a => a.PhraseId)
            .CountAsync(g => g.Min(a => a.CreatedAt) >= since, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
