using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Scheduling;

namespace Kitsuneo.Core.Entities;

/// <summary>When a <see cref="Phrase"/> is next reviewed, and the FSRS memory state behind it. One per phrase.</summary>
public sealed class ReviewSchedule
{
    public Guid PhraseId { get; private set; }
    public ReviewState State { get; private set; }
    public int? Step { get; private set; }
    public double? Stability { get; private set; }
    public double? Difficulty { get; private set; }
    public DateTime DueAt { get; private set; }
    public DateTime? LastReviewedAt { get; private set; }

    private ReviewSchedule() { }

    /// <summary>A schedule never reviewed, due at <paramref name="createdAt"/>.</summary>
    internal static ReviewSchedule Create(Guid phraseId, DateTime createdAt)
    {
        var schedule = new ReviewSchedule { PhraseId = phraseId };
        schedule.Apply(FsrsCard.New(createdAt));
        return schedule;
    }

    public void Review(FsrsScheduler scheduler, ReviewRating rating, DateTime reviewedAt) =>
        Apply(scheduler.Review(ToCard(), rating, reviewedAt));

    public FsrsCard ToCard() => new(State, Step, Stability, Difficulty, DueAt, LastReviewedAt);

    private void Apply(FsrsCard card)
    {
        State = card.State;
        Step = card.Step;
        Stability = card.Stability;
        Difficulty = card.Difficulty;
        DueAt = card.DueAt;
        LastReviewedAt = card.LastReviewedAt;
    }
}
