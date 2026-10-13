using FluentAssertions;
using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Practice;
using Kitsuneo.Application.Users;
using Kitsuneo.Core.Dictation;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;
using Kitsuneo.Core.Scheduling;
using NSubstitute;

namespace Kitsuneo.Application.Tests.Practice;

public class PracticeServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 15, 30, 0, DateTimeKind.Utc);

    private readonly IPhraseRepository _phrases = Substitute.For<IPhraseRepository>();
    private readonly IReviewRepository _reviews = Substitute.For<IReviewRepository>();
    private readonly IPracticeAttemptRepository _attempts = Substitute.For<IPracticeAttemptRepository>();
    private readonly FsrsScheduler _scheduler = new(new FsrsOptions { EnableFuzzing = false });
    private readonly PracticeService _service;

    public PracticeServiceTests()
    {
        _service = new PracticeService(_phrases, _reviews, _attempts, _scheduler, new FixedTimeProvider(Now));
    }

    // GetTodayAsync

    [Fact]
    public async Task GetTodayAsync_ListsReviewsThenNewPhrasesWithoutText()
    {
        var review = Phrase.Create(DefaultUser.Id, Language.English, "Reviewed");
        var fresh = Phrase.Create(DefaultUser.Id, Language.Japanese, "猫", reading: "ねこ");
        _reviews.ListDueReviewsAsync(DefaultUser.Id, Now, null, Arg.Any<CancellationToken>()).Returns([review]);
        _reviews.ListNewAsync(DefaultUser.Id, DefaultUser.NewPhrasesPerDay, null, Arg.Any<CancellationToken>()).Returns([fresh]);

        var today = await _service.GetTodayAsync();

        today.Items.Should().Equal(
            new PracticeItemResponse(review.Id, Language.English, IsNew: false),
            new PracticeItemResponse(fresh.Id, Language.Japanese, IsNew: true));
        today.ReviewCount.Should().Be(1);
        today.NewCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTodayAsync_SubtractsPhrasesStartedTodayFromNewLimit()
    {
        _attempts.CountPhrasesFirstAttemptedSinceAsync(DefaultUser.Id, Now.Date, Arg.Any<CancellationToken>()).Returns(7);

        await _service.GetTodayAsync(Language.Japanese);

        await _reviews.Received(1).ListNewAsync(DefaultUser.Id, 3, Language.Japanese, Arg.Any<CancellationToken>());
        await _reviews.Received(1).ListDueReviewsAsync(DefaultUser.Id, Now, Language.Japanese, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(10)]
    [InlineData(12)]
    public async Task GetTodayAsync_WhenDailyNewLimitReached_ReturnsNoNewPhrases(int startedToday)
    {
        _attempts.CountPhrasesFirstAttemptedSinceAsync(DefaultUser.Id, Now.Date, Arg.Any<CancellationToken>()).Returns(startedToday);

        var today = await _service.GetTodayAsync();

        today.NewCount.Should().Be(0);
        await _reviews.DidNotReceiveWithAnyArgs().ListNewAsync(default, default, default, default);
    }

    // SubmitAttemptAsync

    [Fact]
    public async Task SubmitAttemptAsync_JapaneseWithReading_ComparesAgainstReading()
    {
        var phrase = Phrase.Create(DefaultUser.Id, Language.Japanese, "猫が好きです", reading: "ねこがすきです", translation: "I like cats");
        _phrases.GetByIdAsync(phrase.Id, Arg.Any<CancellationToken>()).Returns(phrase);

        var result = await _service.SubmitAttemptAsync(phrase.Id, "ネコがすきです", playCount: 2, usedSlowSpeed: true, durationMs: 5000);

        result!.Accuracy.Should().Be(100);
        result.Tokens.Should().HaveCount(7).And.OnlyContain(t => t.Status == DictationTokenStatus.Correct);
        result.Text.Should().Be("猫が好きです");
        result.SpokenForm.Should().Be("猫が好きです");
        result.Reading.Should().Be("ねこがすきです");
        result.Translation.Should().Be("I like cats");
    }

    [Fact]
    public async Task SubmitAttemptAsync_JapaneseWithoutReading_ComparesAgainstText()
    {
        var phrase = Phrase.Create(DefaultUser.Id, Language.Japanese, "ねこ");
        _phrases.GetByIdAsync(phrase.Id, Arg.Any<CancellationToken>()).Returns(phrase);

        var result = await _service.SubmitAttemptAsync(phrase.Id, "ねこ", 1, false, 1000);

        result!.Accuracy.Should().Be(100);
    }

    [Fact]
    public async Task SubmitAttemptAsync_SavesAttemptAndReturnsPreview()
    {
        var phrase = Phrase.Create(DefaultUser.Id, Language.English, "I like cats");
        _phrases.GetByIdAsync(phrase.Id, Arg.Any<CancellationToken>()).Returns(phrase);
        PracticeAttempt? saved = null;
        await _attempts.AddAsync(Arg.Do<PracticeAttempt>(a => saved = a), Arg.Any<CancellationToken>());

        var result = await _service.SubmitAttemptAsync(phrase.Id, "I like cat", 3, true, 7000);

        result!.Accuracy.Should().Be(66.67);
        result.Tokens.Should().Equal(
            new DictationToken(DictationTokenStatus.Correct, "i", "i"),
            new DictationToken(DictationTokenStatus.Correct, "like", "like"),
            new DictationToken(DictationTokenStatus.Wrong, "cats", "cat"));
        result.NextDueAt.Should().Be(_scheduler.Preview(phrase.ReviewSchedule.ToCard(), Now));
        result.NextDueAt.Easy.Should().Be(Now.AddDays(8));

        saved.Should().NotBeNull();
        result.AttemptId.Should().Be(saved!.Id);
        saved.Should().BeEquivalentTo(new
        {
            PhraseId = phrase.Id,
            UserId = DefaultUser.Id,
            Answer = "I like cat",
            Accuracy = 66.67,
            PlayCount = 3,
            UsedSlowSpeed = true,
            DurationMs = 7000,
            Rating = (ReviewRating?)null
        });
        await _attempts.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAttemptAsync_WithArchivedOrUnknownPhrase_ReturnsNullAndSavesNothing()
    {
        // The repository hides archived phrases, so they look the same as unknown ones
        _phrases.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Phrase?)null);

        var result = await _service.SubmitAttemptAsync(Guid.NewGuid(), "anything", 1, false, 1000);

        result.Should().BeNull();
        await _attempts.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _attempts.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task SubmitAttemptAsync_WithOtherUsersPhrase_ReturnsNull()
    {
        var phrase = Phrase.Create(Guid.NewGuid(), Language.English, "Not mine");
        _phrases.GetByIdAsync(phrase.Id, Arg.Any<CancellationToken>()).Returns(phrase);

        var result = await _service.SubmitAttemptAsync(phrase.Id, "Not mine", 1, false, 1000);

        result.Should().BeNull();
        await _attempts.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task SubmitAttemptAsync_WithNegativePlayCount_ThrowsDomainException()
    {
        var phrase = Phrase.Create(DefaultUser.Id, Language.English, "Hello");
        _phrases.GetByIdAsync(phrase.Id, Arg.Any<CancellationToken>()).Returns(phrase);

        var act = () => _service.SubmitAttemptAsync(phrase.Id, "Hello", -1, false, 1000);

        await act.Should().ThrowAsync<DomainException>().WithMessage("PlayCount cannot be negative.");
        await _attempts.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    // RateAttemptAsync

    [Fact]
    public async Task RateAttemptAsync_UpdatesScheduleAndRecordsItOnAttempt()
    {
        var (phrase, attempt) = ArrangeAttempt();
        var dueBefore = phrase.ReviewSchedule.DueAt;

        var result = await _service.RateAttemptAsync(attempt.Id, ReviewRating.Easy);

        result.Should().Be(new RateAttemptResponse(Now.AddDays(8), ReviewState.Review));
        phrase.ReviewSchedule.DueAt.Should().Be(Now.AddDays(8));
        phrase.ReviewSchedule.LastReviewedAt.Should().Be(Now);
        attempt.Rating.Should().Be(ReviewRating.Easy);
        attempt.RatedAt.Should().Be(Now);
        attempt.DueAtBefore.Should().Be(dueBefore);
        attempt.DueAtAfter.Should().Be(Now.AddDays(8));
        await _attempts.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _reviews.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RateAttemptAsync_AlreadyRated_ThrowsConflictAndDoesNotSave()
    {
        var (_, attempt) = ArrangeAttempt();
        attempt.Rate(ReviewRating.Good, Now, Now.AddMinutes(10), Now);

        var act = () => _service.RateAttemptAsync(attempt.Id, ReviewRating.Easy);

        await act.Should().ThrowAsync<DomainConflictException>().WithMessage("Attempt has already been rated.");
        await _attempts.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RateAttemptAsync_WithUnknownAttempt_ReturnsNull()
    {
        var result = await _service.RateAttemptAsync(Guid.NewGuid(), ReviewRating.Good);

        result.Should().BeNull();
        await _attempts.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RateAttemptAsync_WhenPhraseWasArchived_ReturnsNull()
    {
        var attempt = PracticeAttempt.Create(Guid.NewGuid(), DefaultUser.Id, "a", 0, 1, false, 1);
        _attempts.GetByIdAsync(attempt.Id, DefaultUser.Id, Arg.Any<CancellationToken>()).Returns(attempt);

        var result = await _service.RateAttemptAsync(attempt.Id, ReviewRating.Good);

        result.Should().BeNull();
        attempt.Rating.Should().BeNull();
        await _attempts.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RateAttemptAsync_WithInvalidRating_ThrowsDomainException()
    {
        var act = () => _service.RateAttemptAsync(Guid.NewGuid(), (ReviewRating)99);

        await act.Should().ThrowAsync<DomainException>().WithMessage("Invalid rating.");
        await _attempts.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default, default);
    }

    private (Phrase Phrase, PracticeAttempt Attempt) ArrangeAttempt()
    {
        var phrase = Phrase.Create(DefaultUser.Id, Language.English, "I like cats");
        var attempt = PracticeAttempt.Create(phrase.Id, DefaultUser.Id, "I like cats", 100, 1, false, 3000);
        _attempts.GetByIdAsync(attempt.Id, DefaultUser.Id, Arg.Any<CancellationToken>()).Returns(attempt);
        _reviews.GetForReviewAsync(phrase.Id, DefaultUser.Id, Arg.Any<CancellationToken>()).Returns(phrase);
        return (phrase, attempt);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
