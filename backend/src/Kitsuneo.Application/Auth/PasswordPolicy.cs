using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Application.Auth;

public static class PasswordPolicy
{
    public const int MinLength = 8;

    // Bounds the hashing work a single request can cause
    public const int MaxLength = 128;

    public static void Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            throw new DomainException($"Password must have at least {MinLength} characters.");

        if (password.Length > MaxLength)
            throw new DomainException($"Password must have at most {MaxLength} characters.");

        if (string.IsNullOrWhiteSpace(password))
            throw new DomainException("Password cannot be only spaces.");
    }
}
