using Kitsuneo.Application.Abstractions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Persistence.Repositories;

public sealed class PhraseRepository(AppDbContext context) : IPhraseRepository
{
    public async Task AddAsync(Phrase phrase, CancellationToken cancellationToken = default) =>
        await context.Phrases.AddAsync(phrase, cancellationToken);

    public Task<Phrase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Phrases.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Phrase>> ListAsync(
        Guid userId,
        Language? language = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Phrases
            .AsNoTracking()
            .Where(p => p.UserId == userId);

        if (language is not null)
            query = query.Where(p => p.Language == language);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Text, pattern, LikeEscape) ||
                EF.Functions.ILike(p.SpokenForm, pattern, LikeEscape) ||
                (p.Reading != null && EF.Functions.ILike(p.Reading, pattern, LikeEscape)) ||
                (p.Translation != null && EF.Functions.ILike(p.Translation, pattern, LikeEscape)));
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
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
