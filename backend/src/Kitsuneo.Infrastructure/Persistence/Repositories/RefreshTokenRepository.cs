using Kitsuneo.Application.Abstractions;
using Kitsuneo.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await context.RefreshTokens.AddAsync(token, cancellationToken);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    // A single conditional UPDATE: of two concurrent refreshes, only one sees RevokedAt still null
    public async Task<bool> TryRotateAsync(
        Guid id,
        Guid replacedById,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var affected = await context.RefreshTokens
            .Where(t => t.Id == id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedById, replacedById),
                cancellationToken);

        return affected > 0;
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken = default) =>
        context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
