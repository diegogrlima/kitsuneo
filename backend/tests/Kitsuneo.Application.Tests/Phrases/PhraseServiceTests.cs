using FluentAssertions;
using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Phrases;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;
using NSubstitute;

namespace Kitsuneo.Application.Tests.Phrases;

public class PhraseServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IPhraseRepository _repository = Substitute.For<IPhraseRepository>();
    private readonly PhraseService _service;

    public PhraseServiceTests()
    {
        _service = new PhraseService(_repository, new FakeCurrentUser(UserId));
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsPhraseOwnedByCurrentUser()
    {
        var request = new CreatePhraseRequest(Language.Japanese, " 猫が好きです ", "ねこがすきです", "ねこがすきです", "I like cats");

        var response = await _service.CreateAsync(request);

        response.Id.Should().NotBeEmpty();
        response.Language.Should().Be(Language.Japanese);
        response.Text.Should().Be("猫が好きです");
        response.SpokenForm.Should().Be("ねこがすきです");
        response.Reading.Should().Be("ねこがすきです");
        response.Translation.Should().Be("I like cats");
        response.IsArchived.Should().BeFalse();

        await _repository.Received(1).AddAsync(
            Arg.Is<Phrase>(p => p.Id == response.Id && p.UserId == UserId && p.Text == "猫が好きです"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_AddsBeforeSavingChanges()
    {
        var request = new CreatePhraseRequest(Language.English, "Hello");
        using var cts = new CancellationTokenSource();

        await _service.CreateAsync(request, cts.Token);

        Received.InOrder(async () =>
        {
            await _repository.AddAsync(Arg.Any<Phrase>(), cts.Token);
            await _repository.SaveChangesAsync(cts.Token);
        });
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ThrowsDomainExceptionAndDoesNotPersist()
    {
        var request = new CreatePhraseRequest(Language.English, "Hello", Reading: "heh-loh");

        var act = () => _service.CreateAsync(request);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Reading is only supported for Japanese phrases.");
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_WhenSaveFails_PropagatesException()
    {
        _repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("database down")));

        var act = () => _service.CreateAsync(new CreatePhraseRequest(Language.English, "Hello"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database down");
    }

    [Fact]
    public async Task GetAsync_WithPhraseOfCurrentUser_ReturnsPhrase()
    {
        var phrase = Phrase.Create(UserId, Language.English, "Hello", translation: "Olá");
        using var cts = new CancellationTokenSource();
        _repository.GetByIdAsync(phrase.Id, cts.Token).Returns(phrase);

        var response = await _service.GetAsync(phrase.Id, cts.Token);

        response.Should().BeEquivalentTo(PhraseResponse.FromEntity(phrase));
    }

    [Fact]
    public async Task GetAsync_WhenPhraseDoesNotExist_ReturnsNull()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Phrase?)null);

        var response = await _service.GetAsync(Guid.NewGuid());

        response.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WithPhraseOfAnotherUser_ReturnsNull()
    {
        var phrase = Phrase.Create(Guid.NewGuid(), Language.English, "Hello");
        _repository.GetByIdAsync(phrase.Id, Arg.Any<CancellationToken>()).Returns(phrase);

        var response = await _service.GetAsync(phrase.Id);

        response.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_WithFilters_QueriesCurrentUserAndPassesFilters()
    {
        using var cts = new CancellationTokenSource();
        var after = new PhraseCursor(DateTime.UtcNow, Guid.NewGuid());
        ArrangeList(Array.Empty<Phrase>());

        await _service.ListAsync(Language.Japanese, "ねこ", after, 10, cts.Token);

        await _repository.Received(1).ListAsync(UserId, 11, Language.Japanese, "ねこ", after, cts.Token);
    }

    [Fact]
    public async Task ListAsync_WithoutArguments_UsesDefaultPageSizeAndNullFilters()
    {
        ArrangeList(Array.Empty<Phrase>());

        await _service.ListAsync();

        await _repository.Received(1).ListAsync(
            UserId, PhraseService.DefaultPageSize + 1, null, null, null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1000, PhraseService.MaxPageSize)]
    public async Task ListAsync_WithPageSizeOutOfRange_ClampsIt(int pageSize, int expected)
    {
        ArrangeList(Array.Empty<Phrase>());

        await _service.ListAsync(pageSize: pageSize);

        await _repository.Received(1).ListAsync(
            UserId, expected + 1, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_WhenRepositoryReturnsAtMostPageSize_ReturnsLastPage()
    {
        var newest = Phrase.Create(UserId, Language.Japanese, "日本語", reading: "にほんご");
        var oldest = Phrase.Create(UserId, Language.English, "Hello");
        ArrangeList(newest, oldest);

        var page = await _service.ListAsync(pageSize: 2);

        page.Items.Should().Equal(PhraseResponse.FromEntity(newest), PhraseResponse.FromEntity(oldest));
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_WhenRepositoryReturnsExtraRow_DropsItAndPointsCursorAtLastItem()
    {
        var phrases = Enumerable.Range(0, 3)
            .Select(i => Phrase.Create(UserId, Language.English, $"Phrase {i}"))
            .ToArray();
        ArrangeList(phrases);

        var page = await _service.ListAsync(pageSize: 2);

        page.Items.Select(p => p.Id).Should().Equal(phrases[0].Id, phrases[1].Id);
        PhraseCursor.TryDecode(page.NextCursor!, out var cursor).Should().BeTrue();
        cursor.Should().Be(new PhraseCursor(phrases[1].CreatedAt, phrases[1].Id));
    }

    [Fact]
    public async Task ListAsync_WhenRepositoryReturnsNothing_ReturnsEmptyLastPage()
    {
        ArrangeList(Array.Empty<Phrase>());

        var page = await _service.ListAsync();

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesNormalizedContentOfCurrentUser()
    {
        var id = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _repository.UpdateAsync(id, UserId, Arg.Any<PhraseContent>(), cts.Token).Returns(true);

        var updated = await _service.UpdateAsync(id, new UpdatePhraseRequest(Language.English, " Hello ", Translation: "Olá"), cts.Token);

        updated.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(
            id,
            UserId,
            PhraseContent.Create(Language.English, "Hello", translation: "Olá"),
            cts.Token);
    }

    [Fact]
    public async Task UpdateAsync_WhenRepositoryFindsNoPhrase_ReturnsFalse()
    {
        _repository.UpdateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<PhraseContent>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var updated = await _service.UpdateAsync(Guid.NewGuid(), new UpdatePhraseRequest(Language.English, "Hello"));

        updated.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidRequest_ThrowsDomainExceptionAndDoesNotPersist()
    {
        var request = new UpdatePhraseRequest(Language.English, "Hello", Reading: "heh-loh");

        var act = () => _service.UpdateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Reading is only supported for Japanese phrases.");
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default, default, default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteAsync_DeletesPhraseOfCurrentUserAndReturnsRepositoryResult(bool deleted)
    {
        var id = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _repository.DeleteAsync(id, UserId, cts.Token).Returns(deleted);

        var result = await _service.DeleteAsync(id, cts.Token);

        result.Should().Be(deleted);
    }

    private void ArrangeList(params Phrase[] phrases) =>
        _repository.ListAsync(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<Language?>(), Arg.Any<string?>(),
                Arg.Any<PhraseCursor?>(), Arg.Any<CancellationToken>())
            .Returns(phrases);
}
