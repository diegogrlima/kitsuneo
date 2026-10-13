namespace Kitsuneo.Application.Abstractions;

/// <summary>Access tokens revoked before expiring, refused by their id (jti) until they expire.</summary>
public interface ITokenBlacklist
{
    Task RevokeAsync(string jti, Guid userId, DateTime expiresAt, CancellationToken cancellationToken = default);

    Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default);
}
