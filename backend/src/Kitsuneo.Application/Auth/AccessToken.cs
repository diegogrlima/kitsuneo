namespace Kitsuneo.Application.Auth;

public sealed record AccessToken(string Token, string Jti, DateTime ExpiresAt);
