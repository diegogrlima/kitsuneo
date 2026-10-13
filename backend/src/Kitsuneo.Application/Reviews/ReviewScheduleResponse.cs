using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;

namespace Kitsuneo.Application.Reviews;

public sealed record ReviewScheduleResponse(
    ReviewState State,
    double? Stability,
    double? Difficulty,
    DateTime DueAt,
    DateTime? LastReviewedAt)
{
    public static ReviewScheduleResponse FromEntity(ReviewSchedule schedule) => new(
        schedule.State,
        schedule.Stability,
        schedule.Difficulty,
        schedule.DueAt,
        schedule.LastReviewedAt);
}
