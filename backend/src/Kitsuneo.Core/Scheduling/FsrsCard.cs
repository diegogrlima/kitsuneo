using Kitsuneo.Core.Enums;

namespace Kitsuneo.Core.Scheduling;

/// <summary>The memory state FSRS needs to schedule one item.</summary>
/// <param name="Step">Current learning or relearning step; null in <see cref="ReviewState.Review"/>.</param>
/// <param name="Stability">Days until recall probability drops to 90%; null before the first review.</param>
/// <param name="Difficulty">From 1 (easiest) to 10 (hardest); null before the first review.</param>
public sealed record FsrsCard(
    ReviewState State,
    int? Step,
    double? Stability,
    double? Difficulty,
    DateTime DueAt,
    DateTime? LastReviewedAt)
{
    /// <summary>A card never reviewed, due right away.</summary>
    public static FsrsCard New(DateTime dueAt) => new(ReviewState.Learning, 0, null, null, dueAt, null);
}
