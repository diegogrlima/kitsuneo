using Kitsuneo.Core.Enums;

namespace Kitsuneo.Core.Scheduling;

/// <summary>
/// FSRS-6 spaced repetition scheduler, a port of py-fsrs 6
/// (https://github.com/open-spaced-repetition/py-fsrs).
/// </summary>
public sealed class FsrsScheduler
{
    private const double MinStability = 0.001;
    private const double MinDifficulty = 1.0;
    private const double MaxDifficulty = 10.0;

    private static readonly double[] LowerBounds =
    [
        MinStability, MinStability, MinStability, MinStability, 1.0, 0.001, 0.001, 0.001, 0.0, 0.0, 0.001,
        0.001, 0.001, 0.001, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.1
    ];

    private static readonly double[] UpperBounds =
    [
        100.0, 100.0, 100.0, 100.0, 10.0, 4.0, 4.0, 0.75, 4.5, 0.8, 3.5,
        5.0, 0.25, 0.9, 4.0, 1.0, 6.0, 2.0, 2.0, 0.8, 0.8
    ];

    // Interval (days) ranges and how much of each is added to the fuzz window
    private static readonly (double Start, double End, double Factor)[] FuzzRanges =
    [
        (2.5, 7.0, 0.15),
        (7.0, 20.0, 0.1),
        (20.0, double.PositiveInfinity, 0.05)
    ];

    private readonly FsrsOptions _options;
    private readonly double[] _w;
    private readonly double _decay;
    private readonly double _factor;
    private readonly Random _random;

    public FsrsScheduler(FsrsOptions? options = null, Random? random = null)
    {
        _options = options ?? new FsrsOptions();
        _w = Validate(_options);
        _decay = -_w[20];
        _factor = Math.Pow(0.9, 1 / _decay) - 1;
        _random = random ?? Random.Shared;
    }

    /// <summary>Probability of recalling the card at <paramref name="now"/>; 0 if it was never reviewed.</summary>
    public double GetRetrievability(FsrsCard card, DateTime now)
    {
        if (card.LastReviewedAt is null || card.Stability is null)
            return 0;

        var elapsedDays = Math.Max(0, WholeDaysBetween(card.LastReviewedAt.Value, now));
        return Math.Pow(1 + _factor * elapsedDays / card.Stability.Value, _decay);
    }

    /// <summary>Returns the card's state after being rated <paramref name="rating"/> at <paramref name="reviewedAt"/>.</summary>
    public FsrsCard Review(FsrsCard card, ReviewRating rating, DateTime reviewedAt) =>
        Review(card, rating, reviewedAt, _options.EnableFuzzing);

    /// <summary>
    /// The due date each rating would give the card at <paramref name="reviewedAt"/>, without fuzz,
    /// so the actual review may land a few days off.
    /// </summary>
    public ReviewPreview Preview(FsrsCard card, DateTime reviewedAt) => new(
        Review(card, ReviewRating.Again, reviewedAt, fuzz: false).DueAt,
        Review(card, ReviewRating.Hard, reviewedAt, fuzz: false).DueAt,
        Review(card, ReviewRating.Good, reviewedAt, fuzz: false).DueAt,
        Review(card, ReviewRating.Easy, reviewedAt, fuzz: false).DueAt);

    private FsrsCard Review(FsrsCard card, ReviewRating rating, DateTime reviewedAt, bool fuzz)
    {
        if (reviewedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Review time must be UTC.", nameof(reviewedAt));
        if (!Enum.IsDefined(rating))
            throw new ArgumentOutOfRangeException(nameof(rating), rating, "Invalid rating.");

        var (stability, difficulty) = NextMemoryState(card, rating, reviewedAt);

        var (state, step, interval) = card.State switch
        {
            ReviewState.Learning => NextStep(card.Step ?? 0, rating, stability, _options.LearningSteps, ReviewState.Learning),
            ReviewState.Review => NextReviewStep(rating, stability),
            ReviewState.Relearning => NextStep(card.Step ?? 0, rating, stability, _options.RelearningSteps, ReviewState.Relearning),
            _ => throw new ArgumentOutOfRangeException(nameof(card), card.State, "Invalid review state.")
        };

        if (fuzz && state == ReviewState.Review)
            interval = Fuzz(interval);

        return new FsrsCard(state, step, stability, difficulty, reviewedAt + interval, reviewedAt);
    }

    private (double Stability, double Difficulty) NextMemoryState(FsrsCard card, ReviewRating rating, DateTime reviewedAt)
    {
        if (card.Stability is not { } stability || card.Difficulty is not { } difficulty)
            return (InitialStability(rating), ClampDifficulty(InitialDifficulty(rating)));

        var isSameDay = card.LastReviewedAt is { } last && WholeDaysBetween(last, reviewedAt) < 1;

        var nextStability = isSameDay
            ? ShortTermStability(stability, rating)
            : NextStability(difficulty, stability, GetRetrievability(card, reviewedAt), rating);

        return (nextStability, NextDifficulty(difficulty, rating));
    }

    private (ReviewState, int?, TimeSpan) NextStep(
        int step, ReviewRating rating, double stability, IReadOnlyList<TimeSpan> steps, ReviewState current)
    {
        var graduate = (ReviewState.Review, (int?)null, TimeSpan.FromDays(NextIntervalDays(stability)));

        // Steps may have been shortened since the card entered them
        if (steps.Count == 0 || (step >= steps.Count && rating != ReviewRating.Again))
            return graduate;

        return rating switch
        {
            ReviewRating.Again => (current, 0, steps[0]),
            ReviewRating.Hard when step == 0 && steps.Count == 1 => (current, step, steps[0] * 1.5),
            ReviewRating.Hard when step == 0 => (current, step, (steps[0] + steps[1]) / 2.0),
            ReviewRating.Hard => (current, step, steps[step]),
            ReviewRating.Good when step + 1 == steps.Count => graduate,
            ReviewRating.Good => (current, step + 1, steps[step + 1]),
            _ => graduate
        };
    }

    private (ReviewState, int?, TimeSpan) NextReviewStep(ReviewRating rating, double stability) =>
        rating == ReviewRating.Again && _options.RelearningSteps.Count > 0
            ? (ReviewState.Relearning, 0, _options.RelearningSteps[0])
            : (ReviewState.Review, null, TimeSpan.FromDays(NextIntervalDays(stability)));

    private double InitialStability(ReviewRating rating) =>
        ClampStability(_w[(int)rating - 1]);

    private double InitialDifficulty(ReviewRating rating) =>
        _w[4] - Math.Exp(_w[5] * ((int)rating - 1)) + 1;

    private int NextIntervalDays(double stability)
    {
        var days = stability / _factor * (Math.Pow(_options.DesiredRetention, 1 / _decay) - 1);

        // Math.Round rounds half to even, like Python's round
        return Math.Min(Math.Max((int)Math.Round(days), 1), _options.MaximumIntervalDays);
    }

    private double ShortTermStability(double stability, ReviewRating rating)
    {
        var increase = Math.Exp(_w[17] * ((int)rating - 3 + _w[18])) * Math.Pow(stability, -_w[19]);

        if (rating != ReviewRating.Again)
            increase = Math.Max(increase, 1.0);

        return ClampStability(stability * increase);
    }

    private double NextDifficulty(double difficulty, ReviewRating rating)
    {
        var delta = -(_w[6] * ((int)rating - 3));
        var damped = difficulty + (10.0 - difficulty) * delta / 9.0;

        // Mean reversion towards the initial difficulty of an Easy rating
        var reverted = _w[7] * InitialDifficulty(ReviewRating.Easy) + (1 - _w[7]) * damped;

        return ClampDifficulty(reverted);
    }

    private double NextStability(double difficulty, double stability, double retrievability, ReviewRating rating) =>
        ClampStability(rating == ReviewRating.Again
            ? NextForgetStability(difficulty, stability, retrievability)
            : NextRecallStability(difficulty, stability, retrievability, rating));

    private double NextForgetStability(double difficulty, double stability, double retrievability)
    {
        var longTerm = _w[11]
            * Math.Pow(difficulty, -_w[12])
            * (Math.Pow(stability + 1, _w[13]) - 1)
            * Math.Exp((1 - retrievability) * _w[14]);

        var shortTerm = stability / Math.Exp(_w[17] * _w[18]);

        return Math.Min(longTerm, shortTerm);
    }

    private double NextRecallStability(double difficulty, double stability, double retrievability, ReviewRating rating)
    {
        var hardPenalty = rating == ReviewRating.Hard ? _w[15] : 1;
        var easyBonus = rating == ReviewRating.Easy ? _w[16] : 1;

        return stability * (1
            + Math.Exp(_w[8])
            * (11 - difficulty)
            * Math.Pow(stability, -_w[9])
            * (Math.Exp((1 - retrievability) * _w[10]) - 1)
            * hardPenalty
            * easyBonus);
    }

    private TimeSpan Fuzz(TimeSpan interval)
    {
        var days = interval.Days;
        if (days < 2.5)
            return interval;

        var delta = 1.0;
        foreach (var (start, end, factor) in FuzzRanges)
            delta += factor * Math.Max(Math.Min(days, end) - start, 0.0);

        var max = Math.Min((int)Math.Round(days + delta), _options.MaximumIntervalDays);
        var min = Math.Min(Math.Max(2, (int)Math.Round(days - delta)), max);

        var fuzzed = (int)Math.Round(_random.NextDouble() * (max - min + 1) + min);
        return TimeSpan.FromDays(Math.Min(fuzzed, _options.MaximumIntervalDays));
    }

    private static double ClampStability(double stability) => Math.Max(stability, MinStability);

    private static double ClampDifficulty(double difficulty) => Math.Clamp(difficulty, MinDifficulty, MaxDifficulty);

    // Like Python's timedelta.days: whole days, rounded down
    private static int WholeDaysBetween(DateTime from, DateTime to) => (int)Math.Floor((to - from).TotalDays);

    private static double[] Validate(FsrsOptions options)
    {
        var w = options.Parameters.ToArray();

        if (w.Length != LowerBounds.Length)
            throw new ArgumentException($"Expected {LowerBounds.Length} FSRS parameters, got {w.Length}.", nameof(options));

        for (var i = 0; i < w.Length; i++)
        {
            if (w[i] < LowerBounds[i] || w[i] > UpperBounds[i])
                throw new ArgumentException(
                    $"FSRS parameter w[{i}] = {w[i]} is out of bounds [{LowerBounds[i]}, {UpperBounds[i]}].", nameof(options));
        }

        if (options.DesiredRetention is <= 0 or >= 1)
            throw new ArgumentException("Desired retention must be between 0 and 1.", nameof(options));

        if (options.MaximumIntervalDays < 1)
            throw new ArgumentException("Maximum interval must be at least 1 day.", nameof(options));

        return w;
    }
}
