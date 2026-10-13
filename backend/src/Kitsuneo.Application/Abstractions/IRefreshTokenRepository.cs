using Kitsuneo.Core.Entities;

namespace Kitsuneo.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes straight to the database: revokes <paramref name="id"/> as replaced by <paramref name="replacedById"/>
    /// only if it is still unrevoked. Returns false when another request already used it.
    /// </summary>
    Task<bool> TryRotateAsync(Guid id, Guid replacedById, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>Writes straight to the database: revokes every unrevoked token of the session.</summary>
    Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
