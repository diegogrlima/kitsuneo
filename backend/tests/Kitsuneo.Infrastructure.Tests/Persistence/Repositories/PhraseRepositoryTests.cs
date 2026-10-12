using FluentAssertions;
using Kitsuneo.Application.Phrases;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
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
        await ArchiveAsync(archived.Id);

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
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        await SeedAsync(NewPhrase("Existing"));
        await using var context = fixture.CreateContext();

        var phrase = await new PhraseRepository(context).GetByIdAsync(Guid.NewGuid());

        phrase.Should().BeNull();
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

    // The domain has no way to archive or backdate a phrase yet, so the tests write the columns directly
    private async Task ArchiveAsync(Guid id)
    {
        await using var context = fixture.CreateContext();
        await context.Phrases.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsArchived, true));
    }

    private async Task SetCreatedAtAsync(Guid id, DateTime createdAt)
    {
        await using var context = fixture.CreateContext();
        await context.Phrases.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, createdAt));
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
