using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Persistence.Repositories;

public sealed class PhraseRepository(AppDbContext context) : IPhraseRepository
{
    public Task<Phrase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Phrases.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Phrase>> ListByUserAsync(
        Guid userId,
        Language? language = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Phrases
            .AsNoTracking()
            .Where(p => p.UserId == userId);

        if (language is not null)
            query = query.Where(p => p.Language == language);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Phrase phrase, CancellationToken cancellationToken = default)
    {
        await context.Phrases.AddAsync(phrase, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
