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
    public ReviewSchedule ReviewSchedule { get; private set; } = null!;

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

        var content = PhraseContent.Create(language, text, spokenForm, reading, translation);

        var id = Guid.CreateVersion7();
        var createdAt = DateTime.UtcNow;

        return new Phrase
        {
            Id = id,
            UserId = userId,
            Language = content.Language,
            Text = content.Text,
            SpokenForm = content.SpokenForm,
            Reading = content.Reading,
            Translation = content.Translation,
            IsArchived = false,
            CreatedAt = createdAt,
            ReviewSchedule = ReviewSchedule.Create(id, createdAt)
        };
    }
}