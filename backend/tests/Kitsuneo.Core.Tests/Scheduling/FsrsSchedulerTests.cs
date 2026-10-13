using FluentAssertions;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Scheduling;
using static Kitsuneo.Core.Enums.ReviewRating;
using static Kitsuneo.Core.Enums.ReviewState;

namespace Kitsuneo.Core.Tests.Scheduling;

// Expected values were generated with py-fsrs 6.3.2 (enable_fuzzing=False), the reference FSRS-6 implementation
public class FsrsSchedulerTests
{
    private const double Precision = 1e-9;

    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly FsrsScheduler Scheduler = new(new FsrsOptions { EnableFuzzing = false });

    /// <summary>One review and the card expected after it; null <paramref name="At"/> means "when due".</summary>
    private sealed record Expected(
        ReviewRating Rating,
        ReviewState State,
        int? Step,
        double Stability,
        double Difficulty,
        TimeSpan Due,
        TimeSpan? At = null);

    private static TimeSpan Days(double days) => TimeSpan.FromDays(days);
    private static TimeSpan Minutes(double minutes) => TimeSpan.FromMinutes(minutes);

    private static void AssertReviews(FsrsScheduler scheduler, params Expected[] reviews)
    {
        var card = FsrsCard.New(T0);

        foreach (var expected in reviews)
        {
            var reviewedAt = expected.At is { } at ? T0 + at : card.DueAt;

            card = scheduler.Review(card, expected.Rating, reviewedAt);

            card.State.Should().Be(expected.State);
            card.Step.Should().Be(expected.Step);
            card.Stability.Should().BeApproximately(expected.Stability, Precision);
            card.Difficulty.Should().BeApproximately(expected.Difficulty, Precision);
            card.DueAt.Should().Be(T0 + expected.Due);
            card.LastReviewedAt.Should().Be(reviewedAt);
        }
    }

    [Fact]
    public void New_IsLearningAndDueRightAway()
    {
        var card = FsrsCard.New(T0);

        card.Should().Be(new FsrsCard(Learning, 0, null, null, T0, null));
    }

    [Fact]
    public void Review_NewCardRatedAgain_RestartsLearningInOneMinute() =>
        AssertReviews(Scheduler, new Expected(Again, Learning, 0, 0.212, 6.4133, Minutes(1)));

    [Fact]
    public void Review_NewCardRatedHard_StaysOnFirstStepBetweenTheTwoSteps() =>
        AssertReviews(Scheduler, new Expected(Hard, Learning, 0, 1.2931, 5.112170705601056, Minutes(5.5)));

    [Fact]
    public void Review_NewCardRatedGood_MovesToNextLearningStep() =>
        AssertReviews(Scheduler, new Expected(Good, Learning, 1, 2.3065, 2.118103970459016, Minutes(10)));

    [Fact]
    public void Review_NewCardRatedEasy_GraduatesToReview() =>
        AssertReviews(Scheduler, new Expected(Easy, Review, null, 8.2956, 1.0, Days(8)));

    [Fact]
    public void Review_LongSequenceAtDue_MatchesReference()
    {
        var offset = Minutes(10);
        var lapse = Days(720) + Minutes(10);

        AssertReviews(Scheduler,
            new Expected(Good, Learning, 1, 2.3065, 2.118103970459016, Minutes(10)),
            new Expected(Good, Review, null, 2.3065, 2.111214235785395, Days(2) + offset),
            new Expected(Good, Review, null, 10.971048263078135, 2.1043313908464483, Days(13) + offset),
            new Expected(Good, Review, null, 46.316858440073425, 2.0974554287524403, Days(59) + offset),
            new Expected(Good, Review, null, 162.99981577472244, 2.0905863426205262, Days(222) + offset),
            new Expected(Good, Review, null, 497.8765551245907, 2.083724125574744, Days(720) + offset),
            new Expected(Again, Relearning, 0, 6.890412507565338, 7.383202320049203, lapse + Minutes(10)),
            new Expected(Again, Relearning, 0, 2.154598374301973, 9.125104766121234, lapse + Minutes(20)),
            new Expected(Good, Review, null, 2.154598374301973, 9.11120803065195, Days(722) + Minutes(30)),
            new Expected(Good, Review, null, 3.9831233795187773, 9.097325191918136, Days(726) + Minutes(30)),
            new Expected(Good, Review, null, 7.236254476883319, 9.083456236023055, Days(733) + Minutes(30)),
            new Expected(Good, Review, null, 12.483043578674472, 9.06960114908387, Days(745) + Minutes(30)),
            new Expected(Good, Review, null, 20.77035728499242, 9.055759917231622, Days(766) + Minutes(30)));
    }

    [Fact]
    public void Review_HardRatings_RaiseDifficultyAndShortenIntervals() =>
        AssertReviews(Scheduler,
            new Expected(Easy, Review, null, 8.2956, 1.0, Days(8)),
            new Expected(Hard, Review, null, 26.704183359371363, 4.010608969296839, Days(35)),
            new Expected(Hard, Review, null, 61.967885620777885, 6.00919149749405, Days(97)),
            new Expected(Good, Review, null, 145.78297141586833, 5.998410675293394, Days(243)));

    [Fact]
    public void Review_SameDayInReview_UsesShortTermStability() =>
        AssertReviews(Scheduler,
            new Expected(Easy, Review, null, 8.2956, 1.0, Days(8), At: TimeSpan.Zero),
            new Expected(Good, Review, null, 8.2956, 1.0, Days(8) + TimeSpan.FromHours(6), At: TimeSpan.FromHours(6)));

    [Fact]
    public void Review_LateRecall_GrowsStabilityMore() =>
        AssertReviews(Scheduler,
            new Expected(Easy, Review, null, 8.2956, 1.0, Days(8), At: TimeSpan.Zero),
            new Expected(Good, Review, null, 101.30395589443371, 1.0, Days(161), At: Days(60)));

    [Fact]
    public void Review_LateLapse_RelearnsThenGraduates() =>
        AssertReviews(Scheduler,
            new Expected(Easy, Review, null, 8.2956, 1.0, Days(8), At: TimeSpan.Zero),
            new Expected(Again, Relearning, 0, 1.8622913082133028, 7.0269895692968385, Days(60) + Minutes(10), At: Days(60)),
            new Expected(Good, Review, null, 1.87830293420938, 7.0151909490243805, Days(62) + Minutes(10), At: Days(60) + Minutes(10)));

    [Fact]
    public void Review_WithoutSteps_StaysInReview()
    {
        var scheduler = new FsrsScheduler(new FsrsOptions { EnableFuzzing = false, LearningSteps = [], RelearningSteps = [] });

        AssertReviews(scheduler,
            new Expected(Good, Review, null, 2.3065, 2.118103970459016, Days(2)),
            new Expected(Again, Review, null, 0.6075801062519337, 7.394502741279718, Days(3)),
            new Expected(Good, Review, null, 2.4004040500172708, 7.382336607835277, Days(5)));
    }

    [Fact]
    public void Review_WithMaximumInterval_CapsInterval()
    {
        var scheduler = new FsrsScheduler(new FsrsOptions { EnableFuzzing = false, MaximumIntervalDays = 10 });

        AssertReviews(scheduler,
            new Expected(Easy, Review, null, 8.2956, 1.0, Days(8)),
            new Expected(Easy, Review, null, 65.62422616189994, 1.0, Days(18)),
            new Expected(Easy, Review, null, 133.49671119573603, 1.0, Days(28)));
    }

    [Fact]
    public void Review_WithLowerDesiredRetention_SchedulesFurtherApart()
    {
        var scheduler = new FsrsScheduler(new FsrsOptions { EnableFuzzing = false, DesiredRetention = 0.8 });

        AssertReviews(scheduler,
            new Expected(Good, Learning, 1, 2.3065, 2.118103970459016, Minutes(10)),
            new Expected(Good, Review, null, 2.3065, 2.111214235785395, Days(8) + Minutes(10)),
            new Expected(Good, Review, null, 22.784052475942136, 2.1043313908464483, Days(84) + Minutes(10)),
            new Expected(Good, Review, null, 158.3022479501867, 2.0974554287524403, Days(609) + Minutes(10)));
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 0.9829234388308534)]
    [InlineData(8, 0.9024733005284542)]
    [InlineData(30, 0.7917787593364196)]
    [InlineData(365, 0.5576673468369081)]
    public void GetRetrievability_DecaysWithElapsedWholeDays(int days, double expected)
    {
        var card = Scheduler.Review(FsrsCard.New(T0), Easy, T0);

        Scheduler.GetRetrievability(card, T0 + Days(days) + TimeSpan.FromHours(1))
            .Should().BeApproximately(expected, Precision);
    }

    [Fact]
    public void GetRetrievability_NeverReviewed_ReturnsZero() =>
        Scheduler.GetRetrievability(FsrsCard.New(T0), T0).Should().Be(0);

    [Fact]
    public void Review_WithFuzzing_SpreadsReviewIntervalLikeReference()
    {
        // Unfuzzed, Easy then Good when due gives 39 days. py-fsrs fuzzes 39 into 35..44:
        // delta = 1 + 0.15 * 4.5 + 0.1 * 13 + 0.05 * 19 = 3.925, and its rounding can reach max + 1
        var reviewedAt = T0 + Days(8);

        var intervals = Enumerable.Range(0, 1000).Select(seed =>
        {
            var scheduler = new FsrsScheduler(random: new Random(seed));
            var first = scheduler.Review(FsrsCard.New(T0), Easy, T0);
            var second = scheduler.Review(first, Good, reviewedAt);
            return (second.DueAt - reviewedAt).TotalDays;
        });

        intervals.Distinct().Order().Should().Equal(Enumerable.Range(35, 10).Select(d => (double)d));
    }

    [Fact]
    public void Review_WithFuzzing_LeavesLearningStepsUntouched()
    {
        var scheduler = new FsrsScheduler(random: new Random(1));

        scheduler.Review(FsrsCard.New(T0), Good, T0).DueAt.Should().Be(T0 + Minutes(10));
    }

    [Fact]
    public void Preview_NewCard_MatchesReferenceAndIncreasesFromAgainToEasy()
    {
        var preview = Scheduler.Preview(FsrsCard.New(T0), T0);

        preview.Should().Be(new ReviewPreview(T0 + Minutes(1), T0 + Minutes(5.5), T0 + Minutes(10), T0 + Days(8)));
    }

    [Fact]
    public void Preview_ReviewCard_IncreasesFromAgainToEasy()
    {
        var card = Scheduler.Review(FsrsCard.New(T0), Easy, T0);
        var reviewedAt = card.DueAt;

        var preview = Scheduler.Preview(card, reviewedAt);

        preview.Again.Should().BeAfter(reviewedAt);
        preview.Hard.Should().BeAfter(preview.Again);
        preview.Good.Should().BeAfter(preview.Hard);
        preview.Easy.Should().BeAfter(preview.Good);
    }

    [Fact]
    public void Preview_DoesNotChangeCardAndIgnoresFuzzing()
    {
        // Fuzzing on: Preview must still give the unfuzzed dates, every time
        var scheduler = new FsrsScheduler(random: new Random(1));
        var card = Scheduler.Review(FsrsCard.New(T0), Easy, T0);
        var copy = card with { };
        var reviewedAt = card.DueAt;

        var first = scheduler.Preview(card, reviewedAt);
        var second = scheduler.Preview(card, reviewedAt);

        card.Should().Be(copy);
        second.Should().Be(first);
        first.Should().Be(new ReviewPreview(
            Scheduler.Review(card, Again, reviewedAt).DueAt,
            Scheduler.Review(card, Hard, reviewedAt).DueAt,
            Scheduler.Review(card, Good, reviewedAt).DueAt,
            Scheduler.Review(card, Easy, reviewedAt).DueAt));
    }

    [Fact]
    public void Review_WithNonUtcTime_Throws()
    {
        var act = () => Scheduler.Review(FsrsCard.New(T0), Good, DateTime.SpecifyKind(T0, DateTimeKind.Local));

        act.Should().Throw<ArgumentException>().WithMessage("Review time must be UTC.*");
    }

    [Fact]
    public void Review_WithInvalidRating_Throws()
    {
        var act = () => Scheduler.Review(FsrsCard.New(T0), (ReviewRating)0, T0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithWrongParameterCount_Throws()
    {
        var act = () => new FsrsScheduler(new FsrsOptions { Parameters = [.. FsrsOptions.DefaultParameters.Take(19)] });

        act.Should().Throw<ArgumentException>().WithMessage("Expected 21 FSRS parameters, got 19.*");
    }

    [Fact]
    public void Constructor_WithParameterOutOfBounds_Throws()
    {
        var parameters = FsrsOptions.DefaultParameters.ToArray();
        parameters[4] = 11;

        var act = () => new FsrsScheduler(new FsrsOptions { Parameters = parameters });

        act.Should().Throw<ArgumentException>().WithMessage("FSRS parameter w[4] = 11 is out of bounds [1, 10].*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Constructor_WithDesiredRetentionOutOfRange_Throws(double retention)
    {
        var act = () => new FsrsScheduler(new FsrsOptions { DesiredRetention = retention });

        act.Should().Throw<ArgumentException>().WithMessage("Desired retention must be between 0 and 1.*");
    }
}
