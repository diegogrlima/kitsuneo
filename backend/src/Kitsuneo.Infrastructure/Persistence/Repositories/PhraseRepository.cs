using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Phrases;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Persistence.Repositories;

public sealed class PhraseRepository(AppDbContext context) : IPhraseRepository
{
    public async Task AddAsync(Phrase phrase, CancellationToken cancellationToken = default) =>
        await context.Phrases.AddAsync(phrase, cancellationToken);

    public Task<Phrase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Phrases
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsArchived, cancellationToken);

    public async Task<IReadOnlyList<Phrase>> ListAsync(
        Guid userId,
        int limit,
        Language? language = null,
        string? search = null,
        PhraseCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Phrases
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsArchived);

        if (language is not null)
            query = query.Where(p => p.Language == language);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Text, pattern, LikeEscape) ||
                (p.Translation != null && EF.Functions.ILike(p.Translation, pattern, LikeEscape)));
        }

        // Keyset pagination: (CreatedAt, Id) < (cursor) reads the index from the cursor on, unlike OFFSET
        if (after is not null)
            query = query.Where(p => EF.Functions.LessThan(
                ValueTuple.Create(p.CreatedAt, p.Id),
                ValueTuple.Create(after.CreatedAt, after.Id)));

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    // ExecuteUpdate runs a single UPDATE with no prior SELECT; the UserId filter enforces ownership
    public async Task<bool> UpdateAsync(
        Guid id,
        Guid userId,
        PhraseContent content,
        CancellationToken cancellationToken = default)
    {
        var affected = await context.Phrases
            .Where(p => p.Id == id && p.UserId == userId && !p.IsArchived)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Language, content.Language)
                .SetProperty(p => p.Text, content.Text)
                .SetProperty(p => p.SpokenForm, content.SpokenForm)
                .SetProperty(p => p.Reading, content.Reading)
                .SetProperty(p => p.Translation, content.Translation),
                cancellationToken);

        return affected > 0;
    }

    // Soft delete: the row stays, archived phrases are hidden from every query
    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var affected = await context.Phrases
            .Where(p => p.Id == id && p.UserId == userId && !p.IsArchived)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsArchived, true), cancellationToken);

        return affected > 0;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    private const string LikeEscape = "\\";

    // Treats %, _ and \ typed by the user as literal characters
    private static string EscapeLikePattern(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");
}
