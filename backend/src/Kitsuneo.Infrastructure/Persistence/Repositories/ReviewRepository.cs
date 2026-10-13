using Kitsuneo.Application.Abstractions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository(AppDbContext context) : IReviewRepository
{
    public async Task<IReadOnlyList<Phrase>> ListDueAsync(
        Guid userId,
        DateTime now,
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.Phrases
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsArchived && p.ReviewSchedule.DueAt <= now)
            .OrderBy(p => p.ReviewSchedule.DueAt)
            .ThenBy(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Phrase>> ListDueReviewsAsync(
        Guid userId,
        DateTime now,
        Language? language = null,
        CancellationToken cancellationToken = default) =>
        await ActivePhrases(userId, language)
            .Where(p => p.ReviewSchedule.LastReviewedAt != null && p.ReviewSchedule.DueAt <= now)
            .OrderBy(p => p.ReviewSchedule.DueAt)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Phrase>> ListNewAsync(
        Guid userId,
        int limit,
        Language? language = null,
        CancellationToken cancellationToken = default) =>
        await ActivePhrases(userId, language)
            .Where(p => p.ReviewSchedule.LastReviewedAt == null)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<Phrase?> GetForReviewAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        context.Phrases
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId && !p.IsArchived, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    private IQueryable<Phrase> ActivePhrases(Guid userId, Language? language)
    {
        var query = context.Phrases
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsArchived);

        return language is null ? query : query.Where(p => p.Language == language);
    }
}
