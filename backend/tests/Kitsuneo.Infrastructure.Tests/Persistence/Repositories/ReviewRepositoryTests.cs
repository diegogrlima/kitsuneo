using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Scheduling;
using Kitsuneo.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Tests.Persistence.Repositories;

// Each test owns a fresh UserId, so tests never see each other's rows and need no cleanup
[Collection(PostgresCollection.Name)]
public class ReviewRepositoryTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task ListDueAsync_ReturnsOnlyDuePhrasesMostOverdueFirst()
    {
        var recent = await SeedAsync("Recent", Now.AddHours(-1));
        var old = await SeedAsync("Old", Now.AddDays(-3));
        var exactlyNow = await SeedAsync("Now", Now);
        await SeedAsync("Future", Now.AddMinutes(1));

        var due = await ListDueAsync();

        due.Select(p => p.Id).Should().Equal(old.Id, recent.Id, exactlyNow.Id);
        due.Should().OnlyContain(p => p.ReviewSchedule.PhraseId == p.Id);
    }

    [Fact]
    public async Task ListDueAsync_ExcludesArchivedAndOtherUsersPhrases()
    {
        var own = await SeedAsync("Mine", Now.AddDays(-1));
        var archived = await SeedAsync("Archived", Now.AddDays(-1));
        await ArchiveAsync(archived.Id);
        await SeedAsync("Someone else's", Now.AddDays(-1), Guid.NewGuid());

        var due = await ListDueAsync();

        due.Select(p => p.Id).Should().Equal(own.Id);
    }

    [Fact]
    public async Task ListDueAsync_RespectsLimit()
    {
        var first = await SeedAsync("First", Now.AddDays(-2));
        await SeedAsync("Second", Now.AddDays(-1));

        var due = await ListDueAsync(limit: 1);

        due.Select(p => p.Id).Should().Equal(first.Id);
    }

    [Fact]
    public async Task GetForReviewAsync_ThenSaveChanges_PersistsReview()
    {
        var phrase = await SeedAsync("Reviewed", Now);
        var scheduler = new FsrsScheduler(new FsrsOptions { EnableFuzzing = false });

        await using (var context = fixture.CreateContext())
        {
            var repository = new ReviewRepository(context);
            var tracked = await repository.GetForReviewAsync(phrase.Id, _userId);
            tracked!.ReviewSchedule.Review(scheduler, ReviewRating.Easy, Now);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var schedule = await readContext.ReviewSchedules.AsNoTracking().SingleAsync(s => s.PhraseId == phrase.Id);
        schedule.State.Should().Be(ReviewState.Review);
        schedule.DueAt.Should().Be(Now.AddDays(8));
        schedule.LastReviewedAt.Should().Be(Now);
    }

    [Fact]
    public async Task GetForReviewAsync_WithOtherUsersPhrase_ReturnsNull()
    {
        var phrase = await SeedAsync("Someone else's", Now, Guid.NewGuid());
        await using var context = fixture.CreateContext();

        var result = await new ReviewRepository(context).GetForReviewAsync(phrase.Id, _userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetForReviewAsync_WithArchivedPhrase_ReturnsNull()
    {
        var phrase = await SeedAsync("Archived", Now);
        await ArchiveAsync(phrase.Id);
        await using var context = fixture.CreateContext();

        var result = await new ReviewRepository(context).GetForReviewAsync(phrase.Id, _userId);

        result.Should().BeNull();
    }

    // Saves a new phrase and moves its schedule to dueAt
    private async Task<Phrase> SeedAsync(string text, DateTime dueAt, Guid? userId = null)
    {
        var phrase = Phrase.Create(userId ?? _userId, Language.English, text);

        await using var context = fixture.CreateContext();
        context.Phrases.Add(phrase);
        await context.SaveChangesAsync();
        await context.ReviewSchedules.Where(s => s.PhraseId == phrase.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DueAt, dueAt));

        return phrase;
    }

    private async Task ArchiveAsync(Guid id)
    {
        await using var context = fixture.CreateContext();
        await new PhraseRepository(context).DeleteAsync(id, _userId);
    }

    private async Task<IReadOnlyList<Phrase>> ListDueAsync(int limit = 1000)
    {
        await using var context = fixture.CreateContext();
        return await new ReviewRepository(context).ListDueAsync(_userId, Now, limit);
    }
}
