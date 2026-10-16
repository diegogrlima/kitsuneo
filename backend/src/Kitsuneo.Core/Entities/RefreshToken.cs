using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Entities;

/// <summary>
/// A long-lived token traded for new access tokens. Only its hash is stored. Each refresh replaces it
/// with a new token of the same <see cref="FamilyId"/> (one login session), so reusing a replaced token
/// reveals it was stolen and the whole family can be revoked.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedById { get; private set; }

    private RefreshToken() { }

    /// <param name="familyId">The session it continues; null starts a new session.</param>
    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime now, TimeSpan lifetime, Guid? familyId = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId is required.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("TokenHash is required.");

        if (lifetime <= TimeSpan.Zero)
            throw new DomainException("Lifetime must be positive.");

        return new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FamilyId = familyId ?? Guid.CreateVersion7(),
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };
    }

    public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;
}
