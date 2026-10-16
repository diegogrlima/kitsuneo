using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Entities;

/// <summary>
/// Blacklist entry for an access token revoked before it expired (on logout). A JWT stays valid until
/// it expires, so it is refused by its id (jti) until then; after <see cref="ExpiresAt"/> the entry is useless.
/// </summary>
public sealed class RevokedAccessToken
{
    public const int MaxJtiLength = 64;

    public string Jti { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public DateTime RevokedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private RevokedAccessToken() { }

    public static RevokedAccessToken Create(string jti, Guid userId, DateTime expiresAt, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(jti))
            throw new DomainException("Jti is required.");

        if (jti.Length > MaxJtiLength)
            throw new DomainException($"Jti must have at most {MaxJtiLength} characters.");

        return new RevokedAccessToken
        {
            Jti = jti,
            UserId = userId,
            RevokedAt = now,
            ExpiresAt = expiresAt
        };
    }
}
