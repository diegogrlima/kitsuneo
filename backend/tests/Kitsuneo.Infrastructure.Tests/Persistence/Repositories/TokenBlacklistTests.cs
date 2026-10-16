using FluentAssertions;
using Kitsuneo.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Tests.Persistence.Repositories;

[Collection(PostgresCollection.Name)]
public class TokenBlacklistTests(PostgresFixture fixture) : IAsyncLifetime
{
    private Guid _userId;

    public async Task InitializeAsync() => _userId = await fixture.CreateUserAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RevokeAsync_ThenIsRevokedAsync_ReturnsTrueOnlyForThatJti()
    {
        var jti = NewJti();

        await RevokeAsync(jti, DateTime.UtcNow.AddMinutes(10));

        (await IsRevokedAsync(jti)).Should().BeTrue();
        (await IsRevokedAsync(NewJti())).Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_SameJtiTwice_KeepsOneEntry()
    {
        var jti = NewJti();

        await RevokeAsync(jti, DateTime.UtcNow.AddMinutes(10));
        await RevokeAsync(jti, DateTime.UtcNow.AddMinutes(10));

        await using var context = fixture.CreateContext();
        (await context.RevokedAccessTokens.CountAsync(t => t.Jti == jti)).Should().Be(1);
    }

    [Fact]
    public async Task RevokeAsync_WithAlreadyExpiredToken_StoresNothing()
    {
        var jti = NewJti();

        await RevokeAsync(jti, DateTime.UtcNow.AddMinutes(-1));

        (await IsRevokedAsync(jti)).Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_PurgesEntriesOfExpiredTokens()
    {
        var expiring = NewJti();
        await RevokeAsync(expiring, DateTime.UtcNow.AddSeconds(1));
        await using (var context = fixture.CreateContext())
        {
            // Move the entry into the past instead of waiting for it to expire
            await context.RevokedAccessTokens.Where(t => t.Jti == expiring)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        await RevokeAsync(NewJti(), DateTime.UtcNow.AddMinutes(10));

        (await IsRevokedAsync(expiring)).Should().BeFalse();
    }

    private static string NewJti() => Guid.NewGuid().ToString("N");

    private async Task RevokeAsync(string jti, DateTime expiresAt)
    {
        await using var context = fixture.CreateContext();
        await new TokenBlacklist(context, TimeProvider.System).RevokeAsync(jti, _userId, expiresAt);
    }

    private async Task<bool> IsRevokedAsync(string jti)
    {
        await using var context = fixture.CreateContext();
        return await new TokenBlacklist(context, TimeProvider.System).IsRevokedAsync(jti);
    }
}
