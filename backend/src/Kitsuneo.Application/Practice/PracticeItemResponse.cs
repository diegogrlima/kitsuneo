using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Practice;

/// <summary>A phrase to practice. Leaves out the text, which would give the dictation away.</summary>
public sealed record PracticeItemResponse(Guid PhraseId, Language Language, bool IsNew);
