using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Core.Tests.Entities;

public class PracticeAttemptTests
{
    private static readonly Guid PhraseId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static PracticeAttempt NewAttempt(string? answer = "I like cats", int playCount = 2, int durationMs = 4200) =>
        PracticeAttempt.Create(PhraseId, UserId, answer, 66.67, playCount, usedSlowSpeed: true, durationMs);

    [Fact]
    public void Create_WithValidData_ReturnsUnratedAttempt()
    {
        var before = DateTime.UtcNow;

        var attempt = NewAttempt();

        var after = DateTime.UtcNow;
        attempt.Id.Should().NotBeEmpty();
        attempt.Id.Version.Should().Be(7);
        attempt.PhraseId.Should().Be(PhraseId);
        attempt.UserId.Should().Be(UserId);
        attempt.Answer.Should().Be("I like cats");
        attempt.Accuracy.Should().Be(66.67);
        attempt.PlayCount.Should().Be(2);
        attempt.UsedSlowSpeed.Should().BeTrue();
        attempt.DurationMs.Should().Be(4200);
        attempt.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        attempt.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        attempt.Rating.Should().BeNull();
        attempt.RatedAt.Should().BeNull();
        attempt.DueAtBefore.Should().BeNull();
        attempt.DueAtAfter.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WithEmptyAnswer_StoresEmptyString(string? answer)
    {
        var attempt = NewAttempt(answer);

        attempt.Answer.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithZeroPlayCountAndDuration_ReturnsAttempt()
    {
        var attempt = NewAttempt(playCount: 0, durationMs: 0);

        attempt.PlayCount.Should().Be(0);
        attempt.DurationMs.Should().Be(0);
    }

    [Fact]
    public void Create_WithAnswerOf500Chars_ReturnsAttempt()
    {
        var attempt = NewAttempt(new string('a', Phrase.MaxLength));

        attempt.Answer.Should().HaveLength(Phrase.MaxLength);
    }

    [Fact]
    public void Create_WithAnswerOver500Chars_ThrowsDomainException()
    {
        var act = () => NewAttempt(new string('a', Phrase.MaxLength + 1));

        act.Should().Throw<DomainException>().WithMessage("Answer must have at most 500 characters.");
    }

    [Fact]
    public void Create_WithNegativePlayCount_ThrowsDomainException()
    {
        var act = () => NewAttempt(playCount: -1);

        act.Should().Throw<DomainException>().WithMessage("PlayCount cannot be negative.");
    }

    [Fact]
    public void Create_WithNegativeDuration_ThrowsDomainException()
    {
        var act = () => NewAttempt(durationMs: -1);

        act.Should().Throw<DomainException>().WithMessage("DurationMs cannot be negative.");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(double.NaN)]
    public void Create_WithAccuracyOutOfRange_ThrowsDomainException(double accuracy)
    {
        var act = () => PracticeAttempt.Create(PhraseId, UserId, "a", accuracy, 1, false, 1);

        act.Should().Throw<DomainException>().WithMessage("Accuracy must be between 0 and 100.");
    }

    [Fact]
    public void Create_WithEmptyIds_ThrowsDomainException()
    {
        var withoutPhrase = () => PracticeAttempt.Create(Guid.Empty, UserId, "a", 0, 1, false, 1);
        var withoutUser = () => PracticeAttempt.Create(PhraseId, Guid.Empty, "a", 0, 1, false, 1);

        withoutPhrase.Should().Throw<DomainException>().WithMessage("PhraseId is required.");
        withoutUser.Should().Throw<DomainException>().WithMessage("UserId is required.");
    }

    [Fact]
    public void Rate_RecordsRatingAndScheduleChange()
    {
        var attempt = NewAttempt();

        attempt.Rate(ReviewRating.Good, Now, Now.AddDays(3), Now.AddMinutes(1));

        attempt.Rating.Should().Be(ReviewRating.Good);
        attempt.DueAtBefore.Should().Be(Now);
        attempt.DueAtAfter.Should().Be(Now.AddDays(3));
        attempt.RatedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Rate_Twice_ThrowsConflictAndKeepsFirstRating()
    {
        var attempt = NewAttempt();
        attempt.Rate(ReviewRating.Good, Now, Now.AddDays(3), Now);

        var act = () => attempt.Rate(ReviewRating.Easy, Now, Now.AddDays(8), Now);

        act.Should().Throw<DomainConflictException>().WithMessage("Attempt has already been rated.");
        attempt.Rating.Should().Be(ReviewRating.Good);
        attempt.DueAtAfter.Should().Be(Now.AddDays(3));
    }

    [Fact]
    public void Rate_WithInvalidRating_ThrowsDomainException()
    {
        var attempt = NewAttempt();

        var act = () => attempt.Rate((ReviewRating)0, Now, Now, Now);

        act.Should().Throw<DomainException>().WithMessage("Invalid rating.");
        attempt.Rating.Should().BeNull();
    }
}
