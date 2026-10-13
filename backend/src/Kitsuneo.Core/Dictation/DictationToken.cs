namespace Kitsuneo.Core.Dictation;

/// <summary>One aligned position between the expected text and the answer, already normalized.</summary>
/// <param name="Expected">Null when <see cref="Status"/> is <see cref="DictationTokenStatus.Extra"/>.</param>
/// <param name="Actual">Null when <see cref="Status"/> is <see cref="DictationTokenStatus.Missing"/>.</param>
public sealed record DictationToken(DictationTokenStatus Status, string? Expected, string? Actual);
