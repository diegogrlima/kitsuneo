using FluentAssertions;
using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Phrases;
using Kitsuneo.Application.Users;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Enums;
using Kitsuneo.Core.Exceptions;
using NSubstitute;

namespace Kitsuneo.Application.Tests.Phrases;

public class PhraseServiceTests
{
    private readonly IPhraseRepository _repository = Substitute.For<IPhraseRepository>();
    private readonly PhraseService _service;

    public PhraseServiceTests()
    {
        _service = new PhraseService(_repository);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsPhraseOwnedByDefaultUser()
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
            Arg.Is<Phrase>(p => p.Id == response.Id && p.UserId == DefaultUser.Id && p.Text == "猫が好きです"),
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
    public async Task GetAsync_WithPhraseOfDefaultUser_ReturnsPhrase()
    {
        var phrase = Phrase.Create(DefaultUser.Id, Language.English, "Hello", translation: "Olá");
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
    public async Task ListAsync_WithFilters_QueriesDefaultUserAndPassesFilters()
    {
        using var cts = new CancellationTokenSource();
        _repository.ListAsync(Arg.Any<Guid>(), Arg.Any<Language?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Phrase>());

        await _service.ListAsync(Language.Japanese, "ねこ", cts.Token);

        await _repository.Received(1).ListAsync(DefaultUser.Id, Language.Japanese, "ねこ", cts.Token);
    }

    [Fact]
    public async Task ListAsync_WithoutFilters_PassesNullFilters()
    {
        _repository.ListAsync(Arg.Any<Guid>(), Arg.Any<Language?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Phrase>());

        await _service.ListAsync();

        await _repository.Received(1).ListAsync(DefaultUser.Id, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_ReturnsMappedPhrasesInRepositoryOrder()
    {
        var newest = Phrase.Create(DefaultUser.Id, Language.Japanese, "日本語", reading: "にほんご");
        var oldest = Phrase.Create(DefaultUser.Id, Language.English, "Hello");
        _repository.ListAsync(DefaultUser.Id, null, null, Arg.Any<CancellationToken>())
            .Returns(new[] { newest, oldest });

        var response = await _service.ListAsync();

        response.Should().Equal(PhraseResponse.FromEntity(newest), PhraseResponse.FromEntity(oldest));
    }

    [Fact]
    public async Task ListAsync_WhenRepositoryReturnsNothing_ReturnsEmptyList()
    {
        _repository.ListAsync(Arg.Any<Guid>(), Arg.Any<Language?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Phrase>());

        var response = await _service.ListAsync();

        response.Should().BeEmpty();
    }
}
