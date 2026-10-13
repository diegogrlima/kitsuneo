using FluentAssertions;
using Kitsuneo.Application.Phrases;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Scheduling;
using Kitsuneo.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Tests.Persistence.Repositories;

// Each test owns a fresh UserId, so tests never see each other's rows and need no cleanup
[Collection(PostgresCollection.Name)]
public class PhraseRepositoryTests(PostgresFixture fixture)
{
    private const int NoLimit = 1000;

    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task ListAsync_WithArchivedPhrase_ExcludesIt()
    {
        var active = NewPhrase("Active");
        var archived = NewPhrase("Archived");
        await SeedAsync(active, archived);
        await DeleteAsync(archived.Id);

        var phrases = await ListAsync();

        phrases.Select(p => p.Id).Should().Equal(active.Id);
    }

    [Fact]
    public async Task ListAsync_WithPhrasesOfAnotherUser_ReturnsOnlyOwnPhrases()
    {
        var own = NewPhrase("Mine");
        var others = Phrase.Create(Guid.NewGuid(), Language.English, "Someone else's");
        await SeedAsync(own, others);

        var phrases = await ListAsync();

        phrases.Select(p => p.Id).Should().Equal(own.Id);
    }

    [Fact]
    public async Task ListAsync_WithLanguageFilter_ReturnsOnlyThatLanguage()
    {
        var english = NewPhrase("Water, please.");
        var japanese = Phrase.Create(_userId, Language.Japanese, "水をください。", reading: "みずをください。");
        await SeedAsync(english, japanese);

        var phrases = await ListAsync(language: Language.Japanese);

        phrases.Select(p => p.Id).Should().Equal(japanese.Id);
    }

    [Fact]
    public async Task ListAsync_WithSearchInDifferentCase_FindsMatchInText()
    {
        var match = NewPhrase("I'm going to Figure It Out.");
        await SeedAsync(match, NewPhrase("Something else"));

        var phrases = await ListAsync(search: "fIGURE it");

        phrases.Select(p => p.Id).Should().Equal(match.Id);
    }

    [Fact]
    public async Task ListAsync_WithSearchInDifferentCase_FindsMatchInTranslation()
    {
        var match = NewPhrase("Nice to meet you", translation: "Prazer em CONHECÊ-LO");
        await SeedAsync(match, NewPhrase("Something else", translation: "Outra coisa"));

        var phrases = await ListAsync(search: "conhecê-lo");

        phrases.Select(p => p.Id).Should().Equal(match.Id);
    }

    [Theory]
    [InlineData("%", "100% sure", "Plain text")]       // unescaped % would match anything
    [InlineData("snake_case", "snake_case", "snakeXcase")] // unescaped _ would match any character
    [InlineData(@"a\b", @"Path a\b", "Path ab")]        // unescaped \ would escape the next character
    public async Task ListAsync_WithWildcardCharacterInSearch_MatchesItLiterally(
        string search, string matchingText, string nonMatchingText)
    {
        var match = NewPhrase(matchingText);
        await SeedAsync(match, NewPhrase(nonMatchingText));

        var phrases = await ListAsync(search: search);

        phrases.Select(p => p.Id).Should().Equal(match.Id);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ListAsync_PagingWithCursor_ReturnsEveryPhraseOnceInListOrder(int pageSize)
    {
        // Three phrases share one CreatedAt and two share another, so page boundaries fall inside ties
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var phrases = Enumerable.Range(0, 7).Select(i => NewPhrase($"Phrase {i}")).ToArray();
        await SeedAsync(phrases);
        int[] secondsOffsets = [3, 3, 3, 2, 2, 1, 0];
        for (var i = 0; i < phrases.Length; i++)
            await SetCreatedAtAsync(phrases[i].Id, baseTime.AddSeconds(secondsOffsets[i]));

        var expectedOrder = (await ListAsync()).Select(p => p.Id).ToList();
        var pagedIds = new List<Guid>();
        PhraseCursor? cursor = null;
        while (true)
        {
            var page = await ListAsync(limit: pageSize, after: cursor);
            pagedIds.AddRange(page.Select(p => p.Id));

            if (page.Count < pageSize)
                break;

            cursor = new PhraseCursor(page[^1].CreatedAt, page[^1].Id);
        }

        expectedOrder.Should().BeEquivalentTo(phrases.Select(p => p.Id));
        pagedIds.Should().Equal(expectedOrder);
    }

    [Fact]
    public async Task AddAsync_SavesReviewScheduleWithPhrase()
    {
        var phrase = NewPhrase("New");
        await SeedAsync(phrase);

        var stored = await GetAsync(phrase.Id);

        stored!.ReviewSchedule.Should().BeEquivalentTo(new
        {
            PhraseId = phrase.Id,
            State = ReviewState.Learning,
            Step = 0,
            Stability = (double?)null,
            Difficulty = (double?)null,
            DueAt = stored.CreatedAt,
            LastReviewedAt = (DateTime?)null
        });
    }

    [Fact]
    public async Task ListAsync_LoadsReviewSchedules()
    {
        await SeedAsync(NewPhrase("First"), NewPhrase("Second"));

        var phrases = await ListAsync();

        phrases.Should().HaveCount(2).And.OnlyContain(p => p.ReviewSchedule != null && p.ReviewSchedule.PhraseId == p.Id);
    }

    [Fact]
    public async Task SaveChangesAsync_AfterReview_PersistsSchedule()
    {
        var phrase = NewPhrase("Reviewed");
        await SeedAsync(phrase);
        var reviewedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var scheduler = new FsrsScheduler(new FsrsOptions { EnableFuzzing = false });

        await using (var context = fixture.CreateContext())
        {
            var tracked = await context.Phrases.SingleAsync(p => p.Id == phrase.Id);
            tracked.ReviewSchedule.Review(scheduler, ReviewRating.Easy, reviewedAt);
            await context.SaveChangesAsync();
        }

        var stored = await GetAsync(phrase.Id);
        stored!.ReviewSchedule.ToCard().Should().Be(
            new FsrsCard(ReviewState.Review, null, 8.2956, 1.0, reviewedAt.AddDays(8), reviewedAt));
    }

    [Fact]
    public async Task GetByIdAsync_WithArchivedPhrase_ReturnsNull()
    {
        var phrase = NewPhrase("Archived");
        await SeedAsync(phrase);
        await DeleteAsync(phrase.Id);

        var stored = await GetAsync(phrase.Id);

        stored.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        await SeedAsync(NewPhrase("Existing"));
        await using var context = fixture.CreateContext();

        var phrase = await new PhraseRepository(context).GetByIdAsync(Guid.NewGuid());

        phrase.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithOwnPhrase_UpdatesContentAndKeepsOtherColumns()
    {
        var phrase = NewPhrase("Hello", translation: "Olá");
        await SeedAsync(phrase);
        var content = PhraseContent.Create(Language.Japanese, "猫", "ねこ", "ねこ", "Cat");

        var updated = await UpdateAsync(phrase.Id, content);

        updated.Should().BeTrue();
        var stored = await GetAsync(phrase.Id);
        stored.Should().BeEquivalentTo(new
        {
            phrase.Id,
            UserId = _userId,
            Language = Language.Japanese,
            Text = "猫",
            SpokenForm = "ねこ",
            Reading = "ねこ",
            Translation = "Cat",
            IsArchived = false
        });
        stored!.CreatedAt.Should().BeCloseTo(phrase.CreatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task UpdateAsync_WithPhraseOfAnotherUser_ReturnsFalseAndLeavesItUnchanged()
    {
        var others = Phrase.Create(Guid.NewGuid(), Language.English, "Someone else's");
        await SeedAsync(others);

        var updated = await UpdateAsync(others.Id, PhraseContent.Create(Language.English, "Hijacked"));

        updated.Should().BeFalse();
        (await GetAsync(others.Id))!.Text.Should().Be("Someone else's");
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_ReturnsFalse()
    {
        var updated = await UpdateAsync(Guid.NewGuid(), PhraseContent.Create(Language.English, "Hello"));

        updated.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WithArchivedPhrase_ReturnsFalseAndLeavesItUnchanged()
    {
        var phrase = NewPhrase("Archived");
        await SeedAsync(phrase);
        await DeleteAsync(phrase.Id);

        var updated = await UpdateAsync(phrase.Id, PhraseContent.Create(Language.English, "Changed"));

        updated.Should().BeFalse();
        (await GetRowAsync(phrase.Id)).Text.Should().Be("Archived");
    }

    [Fact]
    public async Task DeleteAsync_WithOwnPhrase_ArchivesItAndKeepsTheRow()
    {
        var deleted = NewPhrase("Delete me");
        var kept = NewPhrase("Keep me");
        await SeedAsync(deleted, kept);

        var result = await DeleteAsync(deleted.Id);

        result.Should().BeTrue();
        (await GetRowAsync(deleted.Id)).IsArchived.Should().BeTrue();
        (await GetRowAsync(kept.Id)).IsArchived.Should().BeFalse();
        (await GetAsync(deleted.Id)).Should().BeNull();
        (await ListAsync()).Select(p => p.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task DeleteAsync_WithArchivedPhrase_ReturnsFalse()
    {
        var phrase = NewPhrase("Delete me twice");
        await SeedAsync(phrase);
        await DeleteAsync(phrase.Id);

        var result = await DeleteAsync(phrase.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithPhraseOfAnotherUser_ReturnsFalseAndKeepsIt()
    {
        var others = Phrase.Create(Guid.NewGuid(), Language.English, "Someone else's");
        await SeedAsync(others);

        var result = await DeleteAsync(others.Id);

        result.Should().BeFalse();
        (await GetRowAsync(others.Id)).IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsFalse()
    {
        var result = await DeleteAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    private Phrase NewPhrase(string text, string? translation = null) =>
        Phrase.Create(_userId, Language.English, text, translation: translation);

    private async Task SeedAsync(params Phrase[] phrases)
    {
        await using var context = fixture.CreateContext();
        var repository = new PhraseRepository(context);

        foreach (var phrase in phrases)
            await repository.AddAsync(phrase);

        await repository.SaveChangesAsync();
    }

    // The domain has no way to backdate a phrase, so the test writes the column directly
    private async Task SetCreatedAtAsync(Guid id, DateTime createdAt)
    {
        await using var context = fixture.CreateContext();
        await context.Phrases.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, createdAt));
    }

    private async Task<bool> UpdateAsync(Guid id, PhraseContent content)
    {
        await using var context = fixture.CreateContext();
        return await new PhraseRepository(context).UpdateAsync(id, _userId, content);
    }

    private async Task<bool> DeleteAsync(Guid id)
    {
        await using var context = fixture.CreateContext();
        return await new PhraseRepository(context).DeleteAsync(id, _userId);
    }

    private async Task<Phrase?> GetAsync(Guid id)
    {
        await using var context = fixture.CreateContext();
        return await new PhraseRepository(context).GetByIdAsync(id);
    }

    // Reads the row directly, archived or not
    private async Task<Phrase> GetRowAsync(Guid id)
    {
        await using var context = fixture.CreateContext();
        return await context.Phrases.AsNoTracking().SingleAsync(p => p.Id == id);
    }

    // A fresh context per call, so results come from the database and not from the change tracker
    private async Task<IReadOnlyList<Phrase>> ListAsync(
        int limit = NoLimit,
        Language? language = null,
        string? search = null,
        PhraseCursor? after = null)
    {
        await using var context = fixture.CreateContext();
        return await new PhraseRepository(context).ListAsync(_userId, limit, language, search, after);
    }
}
