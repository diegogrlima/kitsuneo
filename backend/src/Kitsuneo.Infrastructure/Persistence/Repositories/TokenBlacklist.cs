using Kitsuneo.Application.Abstractions;
using Kitsuneo.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Persistence.Repositories;

/// <summary>Blacklist kept in the database, so every API instance sees a logout right away.</summary>
public sealed class TokenBlacklist(AppDbContext context, TimeProvider timeProvider) : ITokenBlacklist
{
    public async Task RevokeAsync(string jti, Guid userId, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Entries of expired tokens no longer protect anything; clearing them here keeps the table small
        await context.RevokedAccessTokens
            .Where(t => t.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);

        if (expiresAt <= now || await context.RevokedAccessTokens.AnyAsync(t => t.Jti == jti, cancellationToken))
            return;

        context.RevokedAccessTokens.Add(RevokedAccessToken.Create(jti, userId, expiresAt, now));
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default) =>
        context.RevokedAccessTokens.AnyAsync(t => t.Jti == jti, cancellationToken);
}
