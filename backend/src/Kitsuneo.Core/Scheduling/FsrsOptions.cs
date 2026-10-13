namespace Kitsuneo.Core.Scheduling;

/// <summary>FSRS-6 settings; the defaults match py-fsrs 6.</summary>
public sealed record FsrsOptions
{
    public static readonly IReadOnlyList<double> DefaultParameters =
    [
        0.212, 1.2931, 2.3065, 8.2956, 6.4133, 0.8334, 3.0194, 0.001, 1.8722, 0.1666, 0.796,
        1.4835, 0.0614, 0.2629, 1.6483, 0.6014, 1.8729, 0.5425, 0.0912, 0.0658, 0.1542
    ];

    /// <summary>The 21 FSRS-6 weights (w0..w20).</summary>
    public IReadOnlyList<double> Parameters { get; init; } = DefaultParameters;

    /// <summary>Target recall probability when an item comes due.</summary>
    public double DesiredRetention { get; init; } = 0.9;

    /// <summary>Empty skips learning: the first review graduates the item.</summary>
    public IReadOnlyList<TimeSpan> LearningSteps { get; init; } = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10)];

    /// <summary>Empty keeps forgotten items in review instead of relearning.</summary>
    public IReadOnlyList<TimeSpan> RelearningSteps { get; init; } = [TimeSpan.FromMinutes(10)];

    public int MaximumIntervalDays { get; init; } = 36500;

    /// <summary>Spreads review intervals of 3+ days a little so items added together don't stay together.</summary>
    public bool EnableFuzzing { get; init; } = true;
}
