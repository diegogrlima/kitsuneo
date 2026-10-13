using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Phrases;

public sealed record UpdatePhraseRequest(
    Language Language,
    string Text,
    string? SpokenForm = null,
    string? Reading = null,
    string? Translation = null);
