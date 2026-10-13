namespace Kitsuneo.Application.Auth;

/// <param name="RefreshToken">The raw token; only its hash is stored, so this is the only chance to hand it out.</param>
public sealed record AuthResult(
    UserResponse User,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
