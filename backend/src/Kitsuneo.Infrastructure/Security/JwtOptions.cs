using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Kitsuneo.Infrastructure.Security;

/// <summary>Settings bound from the "Jwt" section. The signing key belongs in user-secrets, never in appsettings.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinSigningKeyLength = 32;

    public string Issuer { get; set; } = "kitsuneo-api";

    public string Audience { get; set; } = "kitsuneo-app";

    public string? SigningKey { get; set; }

    /// <summary>Short, since a stolen access token can't be taken back except through the blacklist.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey!));
}
