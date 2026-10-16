using System.Net.Mail;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Entities;

public sealed class User
{
    public const int MaxEmailLength = 254;

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private User() { }

    /// <param name="passwordHash">Already hashed; the plain password never reaches the domain.</param>
    public static User Create(string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("PasswordHash is required.");

        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Replaces the hash, e.g. when the hashing algorithm got stronger since it was stored.</summary>
    public void ChangePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("PasswordHash is required.");

        PasswordHash = passwordHash;
    }

    /// <summary>Trimmed and lowercased, so "Ana@Mail.com " and "ana@mail.com" are the same account.</summary>
    public static string NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized))
            throw new DomainException("Email is required.");

        if (normalized.Length > MaxEmailLength)
            throw new DomainException($"Email must have at most {MaxEmailLength} characters.");

        // MailAddress also accepts "Name <a@b.c>"; only a bare address is valid here
        if (!MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)
            throw new DomainException("Email is invalid.");

        return normalized;
    }
}
