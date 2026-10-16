using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Infrastructure.Persistence.Repositories;

namespace Kitsuneo.Infrastructure.Tests.Persistence.Repositories;

[Collection(PostgresCollection.Name)]
public class RefreshTokenRepositoryTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    private Guid _userId;

    public async Task InitializeAsync() => _userId = await fixture.CreateUserAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddAsync_ThenGetByHashAsync_ReturnsToken()
    {
        var token = await SeedAsync();

        var stored = await GetAsync(token.TokenHash);

        stored.Should().BeEquivalentTo(token);
    }

    [Fact]
    public async Task GetByHashAsync_WithUnknownHash_ReturnsNull()
    {
        (await GetAsync(Guid.NewGuid().ToString("N"))).Should().BeNull();
    }

    [Fact]
    public async Task TryRotateAsync_SucceedsOnlyOnce()
    {
        var token = await SeedAsync();
        var firstReplacement = Guid.NewGuid();

        await using var context = fixture.CreateContext();
        var repository = new RefreshTokenRepository(context);
        var first = await repository.TryRotateAsync(token.Id, firstReplacement, Now);
        var second = await repository.TryRotateAsync(token.Id, Guid.NewGuid(), Now);

        first.Should().BeTrue();
        second.Should().BeFalse();
        var stored = await GetAsync(token.TokenHash);
        stored!.RevokedAt.Should().Be(Now);
        stored.ReplacedById.Should().Be(firstReplacement);
    }

    [Fact]
    public async Task RevokeFamilyAsync_RevokesOnlyThatSession()
    {
        var first = await SeedAsync();
        var second = await SeedAsync(first.FamilyId);
        var otherSession = await SeedAsync();

        await using (var context = fixture.CreateContext())
            await new RefreshTokenRepository(context).RevokeFamilyAsync(first.FamilyId, Now);

        (await GetAsync(first.TokenHash))!.RevokedAt.Should().Be(Now);
        (await GetAsync(second.TokenHash))!.RevokedAt.Should().Be(Now);
        (await GetAsync(otherSession.TokenHash))!.RevokedAt.Should().BeNull();
    }

    private async Task<RefreshToken> SeedAsync(Guid? familyId = null)
    {
        var token = RefreshToken.Issue(_userId, Guid.NewGuid().ToString("N"), Now, Lifetime, familyId);

        await using var context = fixture.CreateContext();
        var repository = new RefreshTokenRepository(context);
        await repository.AddAsync(token);
        await repository.SaveChangesAsync();

        return token;
    }

    private async Task<RefreshToken?> GetAsync(string hash)
    {
        await using var context = fixture.CreateContext();
        return await new RefreshTokenRepository(context).GetByHashAsync(hash);
    }
}
