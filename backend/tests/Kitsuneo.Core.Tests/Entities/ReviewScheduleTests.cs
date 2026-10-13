using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Scheduling;

namespace Kitsuneo.Core.Tests.Entities;

public class ReviewScheduleTests
{
    private static readonly FsrsScheduler Scheduler = new(new FsrsOptions { EnableFuzzing = false });

    [Fact]
    public void PhraseCreate_CreatesScheduleDueRightAway()
    {
        var phrase = Phrase.Create(Guid.NewGuid(), Language.English, "I like cats");

        var schedule = phrase.ReviewSchedule;
        schedule.PhraseId.Should().Be(phrase.Id);
        schedule.State.Should().Be(ReviewState.Learning);
        schedule.Step.Should().Be(0);
        schedule.Stability.Should().BeNull();
        schedule.Difficulty.Should().BeNull();
        schedule.DueAt.Should().Be(phrase.CreatedAt);
        schedule.LastReviewedAt.Should().BeNull();
    }

    [Fact]
    public void Review_AppliesSchedulerResult()
    {
        var schedule = Phrase.Create(Guid.NewGuid(), Language.English, "I like cats").ReviewSchedule;
        var reviewedAt = schedule.DueAt.AddMinutes(1);
        var expected = Scheduler.Review(schedule.ToCard(), ReviewRating.Easy, reviewedAt);

        schedule.Review(Scheduler, ReviewRating.Easy, reviewedAt);

        schedule.ToCard().Should().Be(expected);
        schedule.State.Should().Be(ReviewState.Review);
        schedule.DueAt.Should().Be(reviewedAt.AddDays(8));
    }
}
