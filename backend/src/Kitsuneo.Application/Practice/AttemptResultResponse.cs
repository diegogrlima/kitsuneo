using Kitsuneo.Core.Dictation;
using Kitsuneo.Core.Scheduling;

namespace Kitsuneo.Application.Practice;

/// <param name="NextDueAt">When the phrase would be due again for each rating, to show on the rating buttons.</param>
public sealed record AttemptResultResponse(
    Guid AttemptId,
    IReadOnlyList<DictationToken> Tokens,
    double Accuracy,
    string Text,
    string SpokenForm,
    string? Reading,
    string? Translation,
    ReviewPreview NextDueAt);
