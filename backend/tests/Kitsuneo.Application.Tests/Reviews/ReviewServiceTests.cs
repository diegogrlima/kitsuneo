using FluentAssertions;
using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Reviews;
using Kitsuneo.Application.Users;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;
using Kitsuneo.Core.Scheduling;
using NSubstitute;

namespace Kitsuneo.Application.Tests.Reviews;

public class ReviewServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IReviewRepository _repository = Substitute.For<IReviewRepository>();
    private readonly ReviewService _service;

    public ReviewServiceTests()
    {
        _service = new ReviewService(
            _repository,
            new FsrsScheduler(new FsrsOptions { EnableFuzzing = false }),
            new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task ListDueAsync_QueriesDefaultUserAtCurrentTime()
    {
        var phrase = Phrase.Create(DefaultUser.Id, Language.English, "Hello");
        _repository.ListDueAsync(DefaultUser.Id, Now, 20, Arg.Any<CancellationToken>()).Returns([phrase]);

        var due = await _service.ListDueAsync();

        due.Should().ContainSingle();
        due[0].Phrase.Id.Should().Be(phrase.Id);
        due[0].Phrase.Text.Should().Be("Hello");
        due[0].Schedule.State.Should().Be(ReviewState.Learning);
        due[0].Schedule.DueAt.Should().Be(phrase.CreatedAt);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, ReviewService.MaxLimit)]
    public async Task ListDueAsync_ClampsLimit(int limit, int expected)
    {
        await _service.ListDueAsync(limit);

        await _repository.Received(1).ListDueAsync(DefaultUser.Id, Now, expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewAsync_WithOwnPhrase_AppliesRatingAndSaves()
    {
        var phrase = Phrase.Create(DefaultUser.Id, Language.English, "Hello");
        _repository.GetForReviewAsync(phrase.Id, DefaultUser.Id, Arg.Any<CancellationToken>()).Returns(phrase);

        var schedule = await _service.ReviewAsync(phrase.Id, new ReviewRequest(ReviewRating.Easy));

        schedule.Should().Be(new ReviewScheduleResponse(ReviewState.Review, 8.2956, 1.0, Now.AddDays(8), Now));
        phrase.ReviewSchedule.DueAt.Should().Be(Now.AddDays(8));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReviewAsync_WithUnknownPhrase_ReturnsNullAndDoesNotSave()
    {
        var schedule = await _service.ReviewAsync(Guid.NewGuid(), new ReviewRequest(ReviewRating.Good));

        schedule.Should().BeNull();
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ReviewAsync_WithInvalidRating_ThrowsDomainException()
    {
        var act = () => _service.ReviewAsync(Guid.NewGuid(), new ReviewRequest((ReviewRating)99));

        await act.Should().ThrowAsync<DomainException>().WithMessage("Invalid rating.");
        await _repository.DidNotReceiveWithAnyArgs().GetForReviewAsync(default, default, default);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
