namespace Kitsuneo.Application.Practice;

public sealed record SubmitAttemptRequest(
    string? Answer,
    int PlayCount,
    bool UsedSlowSpeed,
    int DurationMs);
