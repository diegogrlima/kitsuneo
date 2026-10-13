using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Entities;

/// <summary>One dictation of a <see cref="Phrase"/>: what was typed, how it scored and, once rated, how it moved the schedule.</summary>
public sealed class PracticeAttempt
{
    public Guid Id { get; private set; }
    public Guid PhraseId { get; private set; }
    public Guid UserId { get; private set; }
    public string Answer { get; private set; } = null!;
    public double Accuracy { get; private set; }
    public int PlayCount { get; private set; }
    public bool UsedSlowSpeed { get; private set; }
    public int DurationMs { get; private set; }
    public ReviewRating? Rating { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RatedAt { get; private set; }
    public DateTime? DueAtBefore { get; private set; }
    public DateTime? DueAtAfter { get; private set; }

    private PracticeAttempt() { }

    /// <param name="answer">What the user typed; may be empty.</param>
    /// <param name="accuracy">The <c>DictationResult.Accuracy</c> of the answer, from 0 to 100.</param>
    public static PracticeAttempt Create(
        Guid phraseId,
        Guid userId,
        string? answer,
        double accuracy,
        int playCount,
        bool usedSlowSpeed,
        int durationMs)
    {
        if (phraseId == Guid.Empty)
            throw new DomainException("PhraseId is required.");

        if (userId == Guid.Empty)
            throw new DomainException("UserId is required.");

        answer ??= string.Empty;
        if (answer.Length > Phrase.MaxLength)
            throw new DomainException($"Answer must have at most {Phrase.MaxLength} characters.");

        if (accuracy is < 0 or > 100 || double.IsNaN(accuracy))
            throw new DomainException("Accuracy must be between 0 and 100.");

        if (playCount < 0)
            throw new DomainException("PlayCount cannot be negative.");

        if (durationMs < 0)
            throw new DomainException("DurationMs cannot be negative.");

        return new PracticeAttempt
        {
            Id = Guid.CreateVersion7(),
            PhraseId = phraseId,
            UserId = userId,
            Answer = answer,
            Accuracy = accuracy,
            PlayCount = playCount,
            UsedSlowSpeed = usedSlowSpeed,
            DurationMs = durationMs,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Records the rating given after the attempt and its effect on the phrase's schedule. Allowed once.</summary>
    public void Rate(ReviewRating rating, DateTime dueAtBefore, DateTime dueAtAfter, DateTime ratedAt)
    {
        if (Rating is not null)
            throw new DomainConflictException("Attempt has already been rated.");

        if (!Enum.IsDefined(rating))
            throw new DomainException("Invalid rating.");

        Rating = rating;
        RatedAt = ratedAt;
        DueAtBefore = dueAtBefore;
        DueAtAfter = dueAtAfter;
    }
}
