using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Entities;

public sealed class Phrase
{
    public const int MaxLength = 500;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Language Language { get; private set; }
    public string Text { get; private set; } = null!;
    public string SpokenForm { get; private set; } = null!;
    public string? Reading { get; private set; }
    public string? Translation { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Phrase() { }

    public static Phrase Create(
        Guid userId,
        Language language,
        string text,
        string? spokenForm = null,
        string? reading = null,
        string? translation = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId is required.");

        if (!Enum.IsDefined(language))
            throw new DomainException("Invalid language.");

        var normalizedText = EnsureMaxLength(Normalize(text), nameof(Text))
            ?? throw new DomainException("Text is required.");

        var normalizedReading = EnsureMaxLength(Normalize(reading), nameof(Reading));
        if (normalizedReading is not null && language != Language.Japanese)
            throw new DomainException("Reading is only supported for Japanese phrases.");

        return new Phrase
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Language = language,
            Text = normalizedText,
            SpokenForm = EnsureMaxLength(Normalize(spokenForm), nameof(SpokenForm)) ?? normalizedText,
            Reading = normalizedReading,
            Translation = EnsureMaxLength(Normalize(translation), nameof(Translation)),
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? EnsureMaxLength(string? value, string field)
    {
        if (value is not null && value.Length > MaxLength)
            throw new DomainException($"{field} must have at most {MaxLength} characters.");

        return value;
    }
}