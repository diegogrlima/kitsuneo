using System.Security.Cryptography;
using System.Text;

namespace Kitsuneo.Application.Auth;

/// <summary>Raw refresh tokens and the hash that is stored in their place.</summary>
public static class RefreshTokenSecret
{
    private const int SizeInBytes = 32;

    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(SizeInBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // A fast hash is enough: the token is 256 random bits, not a guessable password
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
