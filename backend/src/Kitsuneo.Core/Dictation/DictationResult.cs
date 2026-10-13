namespace Kitsuneo.Core.Dictation;

/// <param name="Tokens">The alignment, in reading order.</param>
/// <param name="Accuracy">
/// Percentage (0–100) of aligned positions that are <see cref="DictationTokenStatus.Correct"/>,
/// so extra tokens also lower it.
/// </param>
public sealed record DictationResult(IReadOnlyList<DictationToken> Tokens, double Accuracy);
