using Kitsuneo.Application.Auth;

namespace Kitsuneo.API.Controllers;

/// <summary>The refresh token is left out on purpose: it goes in an HttpOnly cookie.</summary>
public sealed record AuthResponse(UserResponse User, string AccessToken, DateTime AccessTokenExpiresAt)
{
    public static AuthResponse From(AuthResult result) => new(result.User, result.AccessToken, result.AccessTokenExpiresAt);
}
