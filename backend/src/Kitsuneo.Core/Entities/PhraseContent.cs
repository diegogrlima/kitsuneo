using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Entities;

/// <summary>The editable fields of a <see cref="Phrase"/>, already validated and normalized.</summary>
public sealed record PhraseContent
{
    public Language Language { get; }
    public string Text { get; }
    public string SpokenForm { get; }
    public string? Reading { get; }
    public string? Translation { get; }

    private PhraseContent(Language language, string text, string spokenForm, string? reading, string? translation)
    {
        Language = language;
        Text = text;
        SpokenForm = spokenForm;
        Reading = reading;
        Translation = translation;
    }

    public static PhraseContent Create(
        Language language,
        string text,
        string? spokenForm = null,
        string? reading = null,
        string? translation = null)
    {
        if (!Enum.IsDefined(language))
            throw new DomainException("Invalid language.");

        var normalizedText = EnsureMaxLength(Normalize(text), nameof(Text))
            ?? throw new DomainException("Text is required.");

        var normalizedReading = EnsureMaxLength(Normalize(reading), nameof(Reading));
        if (normalizedReading is not null && language != Language.Japanese)
            throw new DomainException("Reading is only supported for Japanese phrases.");

        return new PhraseContent(
            language,
            normalizedText,
            EnsureMaxLength(Normalize(spokenForm), nameof(SpokenForm)) ?? normalizedText,
            normalizedReading,
            EnsureMaxLength(Normalize(translation), nameof(Translation)));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? EnsureMaxLength(string? value, string field)
    {
        if (value is not null && value.Length > Phrase.MaxLength)
            throw new DomainException($"{field} must have at most {Phrase.MaxLength} characters.");

        return value;
    }
}
