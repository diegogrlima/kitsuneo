using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Tests.Persistence.Repositories;

// Each test owns a fresh UserId, so tests never see each other's rows and need no cleanup
[Collection(PostgresCollection.Name)]
public class PracticeAttemptRepositoryTests(PostgresFixture fixture)
{
    private static readonly DateTime Today = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsSavedAttempt()
    {
        var phrase = await SeedPhraseAsync();
        var attempt = PracticeAttempt.Create(phrase.Id, _userId, "I like cat", 66.67, 3, true, 7000);
        await SaveAsync(attempt);

        await using var context = fixture.CreateContext();
        var stored = await new PracticeAttemptRepository(context).GetByIdAsync(attempt.Id, _userId);

        stored.Should().BeEquivalentTo(new
        {
            attempt.Id,
            PhraseId = phrase.Id,
            UserId = _userId,
            Answer = "I like cat",
            Accuracy = 66.67,
            PlayCount = 3,
            UsedSlowSpeed = true,
            DurationMs = 7000,
            Rating = (ReviewRating?)null,
            RatedAt = (DateTime?)null,
            DueAtBefore = (DateTime?)null,
            DueAtAfter = (DateTime?)null
        });
        stored!.CreatedAt.Should().BeCloseTo(attempt.CreatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetByIdAsync_WithOtherUsersAttempt_ReturnsNull()
    {
        var phrase = await SeedPhraseAsync();
        var attempt = PracticeAttempt.Create(phrase.Id, Guid.NewGuid(), "a", 0, 1, false, 1);
        await SaveAsync(attempt);

        await using var context = fixture.CreateContext();
        var stored = await new PracticeAttemptRepository(context).GetByIdAsync(attempt.Id, _userId);

        stored.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ThenRateAndSave_PersistsRating()
    {
        var phrase = await SeedPhraseAsync();
        var attempt = PracticeAttempt.Create(phrase.Id, _userId, "a", 100, 1, false, 1);
        await SaveAsync(attempt);

        await using (var context = fixture.CreateContext())
        {
            var repository = new PracticeAttemptRepository(context);
            var tracked = await repository.GetByIdAsync(attempt.Id, _userId);
            tracked!.Rate(ReviewRating.Good, Today, Today.AddDays(2), Today.AddHours(1));
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var stored = await readContext.PracticeAttempts.AsNoTracking().SingleAsync(a => a.Id == attempt.Id);
        stored.Rating.Should().Be(ReviewRating.Good);
        stored.DueAtBefore.Should().Be(Today);
        stored.DueAtAfter.Should().Be(Today.AddDays(2));
        stored.RatedAt.Should().Be(Today.AddHours(1));
    }

    [Fact]
    public async Task CountPhrasesFirstAttemptedSinceAsync_CountsOnlyPhrasesWhoseFirstAttemptIsSince()
    {
        var startedToday = await SeedPhraseAsync();
        var startedYesterday = await SeedPhraseAsync();
        var onlyYesterday = await SeedPhraseAsync();
        var othersPhrase = await SeedPhraseAsync();

        await SeedAttemptAsync(startedToday.Id, Today.AddHours(1));
        await SeedAttemptAsync(startedToday.Id, Today.AddHours(2));
        await SeedAttemptAsync(startedYesterday.Id, Today.AddHours(-1));
        await SeedAttemptAsync(startedYesterday.Id, Today.AddHours(3));
        await SeedAttemptAsync(onlyYesterday.Id, Today.AddDays(-1));
        await SeedAttemptAsync(othersPhrase.Id, Today.AddHours(1), Guid.NewGuid());

        await using var context = fixture.CreateContext();
        var count = await new PracticeAttemptRepository(context).CountPhrasesFirstAttemptedSinceAsync(_userId, Today);

        count.Should().Be(1);
    }

    [Fact]
    public async Task SaveChangesAsync_WithUnknownPhrase_FailsForeignKey()
    {
        var attempt = PracticeAttempt.Create(Guid.NewGuid(), _userId, "a", 0, 1, false, 1);

        var act = () => SaveAsync(attempt);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private async Task<Phrase> SeedPhraseAsync()
    {
        var phrase = Phrase.Create(_userId, Language.English, "I like cats");

        await using var context = fixture.CreateContext();
        context.Phrases.Add(phrase);
        await context.SaveChangesAsync();

        return phrase;
    }

    // Saves an attempt and moves its creation date to createdAt
    private async Task SeedAttemptAsync(Guid phraseId, DateTime createdAt, Guid? userId = null)
    {
        var attempt = PracticeAttempt.Create(phraseId, userId ?? _userId, "a", 0, 1, false, 1);
        await SaveAsync(attempt);

        await using var context = fixture.CreateContext();
        await context.PracticeAttempts.Where(a => a.Id == attempt.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedAt, createdAt));
    }

    private async Task SaveAsync(PracticeAttempt attempt)
    {
        await using var context = fixture.CreateContext();
        var repository = new PracticeAttemptRepository(context);
        await repository.AddAsync(attempt);
        await repository.SaveChangesAsync();
    }
}
