using Kitsuneo.Application.Abstractions;
using Kitsuneo.Core.Entities;
using IdentityPasswordHasher = Microsoft.AspNetCore.Identity.PasswordHasher<Kitsuneo.Core.Entities.User>;
using PasswordVerificationResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace Kitsuneo.Infrastructure.Security;

/// <summary>
/// ASP.NET Core Identity's hasher (PBKDF2 with HMAC-SHA512, random salt, 100,000 iterations), used on its own.
/// The hash records its format, so stronger defaults in future versions are picked up through rehashing.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly IdentityPasswordHasher _hasher = new();

    // The Identity hasher ignores the user argument
    private static readonly User NoUser = null!;

    public string Hash(string password) => _hasher.HashPassword(NoUser, password);

    public PasswordCheck Verify(string passwordHash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(NoUser, passwordHash, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
                _ => PasswordCheck.Failed
            };
        }
        catch (FormatException)
        {
            // A corrupted stored hash matches no password; failing the login beats a 500
            return PasswordCheck.Failed;
        }
    }
}
