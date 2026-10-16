using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Scheduling;
using Kitsuneo.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Tests.Persistence.Repositories;

// Each test owns a fresh user, so tests never see each other's rows and need no cleanup
[Collection(PostgresCollection.Name)]
public class ReviewRepositoryTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private Guid _userId;

    public async Task InitializeAsync() => _userId = await fixture.CreateUserAsync();

    public Task DisposeAsync() => Task.CompletedTask;

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
        await SeedAsync("Someone else's", Now.AddDays(-1), await fixture.CreateUserAsync());

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
    public async Task ListDueReviewsAsync_ReturnsOnlyReviewedDuePhrasesMostOverdueFirst()
    {
        var recent = await SeedAsync("Recent", Now.AddHours(-1), lastReviewedAt: Now.AddDays(-2));
        var old = await SeedAsync("Old", Now.AddDays(-3), lastReviewedAt: Now.AddDays(-9));
        await SeedAsync("Not due yet", Now.AddDays(1), lastReviewedAt: Now.AddDays(-1));
        await SeedAsync("Never reviewed", Now.AddDays(-5));

        var due = await ListDueReviewsAsync();

        due.Select(p => p.Id).Should().Equal(old.Id, recent.Id);
    }

    [Fact]
    public async Task ListDueReviewsAsync_FiltersLanguageAndExcludesArchivedAndOtherUsers()
    {
        var japanese = await SeedAsync("日本語", Now.AddDays(-1), lastReviewedAt: Now.AddDays(-2), language: Language.Japanese);
        await SeedAsync("English", Now.AddDays(-1), lastReviewedAt: Now.AddDays(-2));
        var archived = await SeedAsync("アーカイブ", Now.AddDays(-1), lastReviewedAt: Now.AddDays(-2), language: Language.Japanese);
        await ArchiveAsync(archived.Id);
        await SeedAsync("他人", Now.AddDays(-1), await fixture.CreateUserAsync(), Now.AddDays(-2), Language.Japanese);

        var due = await ListDueReviewsAsync(Language.Japanese);

        due.Select(p => p.Id).Should().Equal(japanese.Id);
    }

    [Fact]
    public async Task ListNewAsync_ReturnsNeverReviewedPhrasesOldestFirstUpToLimit()
    {
        var first = await SeedAsync("First", Now, createdAt: Now.AddDays(-3));
        var second = await SeedAsync("Second", Now, createdAt: Now.AddDays(-2));
        await SeedAsync("Third", Now, createdAt: Now.AddDays(-1));
        await SeedAsync("Reviewed", Now, lastReviewedAt: Now.AddDays(-1), createdAt: Now.AddDays(-4));

        var fresh = await ListNewAsync(limit: 2);

        fresh.Select(p => p.Id).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task ListNewAsync_FiltersLanguageAndExcludesArchived()
    {
        var japanese = await SeedAsync("日本語", Now, language: Language.Japanese);
        await SeedAsync("English", Now);
        var archived = await SeedAsync("アーカイブ", Now, language: Language.Japanese);
        await ArchiveAsync(archived.Id);

        var fresh = await ListNewAsync(language: Language.Japanese);

        fresh.Select(p => p.Id).Should().Equal(japanese.Id);
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
        var phrase = await SeedAsync("Someone else's", Now, await fixture.CreateUserAsync());
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

    // Saves a new phrase, then moves its schedule (and optionally its creation date) to the given values
    private async Task<Phrase> SeedAsync(
        string text,
        DateTime dueAt,
        Guid? userId = null,
        DateTime? lastReviewedAt = null,
        Language language = Language.English,
        DateTime? createdAt = null)
    {
        var phrase = Phrase.Create(userId ?? _userId, language, text);

        await using var context = fixture.CreateContext();
        context.Phrases.Add(phrase);
        await context.SaveChangesAsync();
        await context.ReviewSchedules.Where(s => s.PhraseId == phrase.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.DueAt, dueAt)
                .SetProperty(x => x.LastReviewedAt, lastReviewedAt));

        if (createdAt is not null)
            await context.Phrases.Where(p => p.Id == phrase.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, createdAt.Value));

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

    private async Task<IReadOnlyList<Phrase>> ListDueReviewsAsync(Language? language = null)
    {
        await using var context = fixture.CreateContext();
        return await new ReviewRepository(context).ListDueReviewsAsync(_userId, Now, language);
    }

    private async Task<IReadOnlyList<Phrase>> ListNewAsync(int limit = 1000, Language? language = null)
    {
        await using var context = fixture.CreateContext();
        return await new ReviewRepository(context).ListNewAsync(_userId, limit, language);
    }
}
