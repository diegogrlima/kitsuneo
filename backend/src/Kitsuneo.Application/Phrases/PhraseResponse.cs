using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Phrases;

public sealed record PhraseResponse(
    Guid Id,
    Language Language,
    string Text,
    string SpokenForm,
    string? Reading,
    string? Translation,
    bool IsArchived,
    DateTime CreatedAt)
{
    public static PhraseResponse FromEntity(Phrase phrase) => new(
        phrase.Id,
        phrase.Language,
        phrase.Text,
        phrase.SpokenForm,
        phrase.Reading,
        phrase.Translation,
        phrase.IsArchived,
        phrase.CreatedAt);
}
