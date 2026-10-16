namespace Kitsuneo.API.Infrastructure.Auth;

/// <summary>
/// The refresh token travels only in this cookie: HttpOnly keeps it away from JavaScript (XSS),
/// SameSite=Strict away from other sites (CSRF), and the path limits it to the auth endpoints.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "kitsuneo_refresh";
    private const string Path = "/api/v1/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string token, DateTime expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(expiresAt: null));

    private static CookieOptions Options(DateTime? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt
    };
}
