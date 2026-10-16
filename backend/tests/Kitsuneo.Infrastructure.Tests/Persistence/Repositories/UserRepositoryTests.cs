using FluentAssertions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Exceptions;
using Kitsuneo.Infrastructure.Persistence.Repositories;

namespace Kitsuneo.Infrastructure.Tests.Persistence.Repositories;

[Collection(PostgresCollection.Name)]
public class UserRepositoryTests(PostgresFixture fixture)
{
    // Unique per test, since the users table is shared by every test of the collection
    private readonly string _email = $"ana-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task AddAsync_ThenGetByEmailAndById_ReturnsUser()
    {
        var user = User.Create(_email, "hash");
        await SaveAsync(user);

        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);
        var byEmail = await repository.GetByEmailAsync(_email);
        var byId = await repository.GetByIdAsync(user.Id);

        byEmail!.Id.Should().Be(user.Id);
        byId!.Email.Should().Be(_email);
        byId.PasswordHash.Should().Be("hash");
    }

    [Fact]
    public async Task EmailExistsAsync_ReflectsSavedUsers()
    {
        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);
        (await repository.EmailExistsAsync(_email)).Should().BeFalse();

        await SaveAsync(User.Create(_email, "hash"));

        (await repository.EmailExistsAsync(_email)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveChangesAsync_WithDuplicateEmail_ThrowsConflict()
    {
        await SaveAsync(User.Create(_email, "hash"));

        var act = () => SaveAsync(User.Create(_email.ToUpperInvariant(), "hash"));

        await act.Should().ThrowAsync<DomainConflictException>().WithMessage("Email is already registered.");
    }

    [Fact]
    public async Task GetByEmailAsync_ThenChangeHashAndSave_PersistsNewHash()
    {
        var user = User.Create(_email, "old");
        await SaveAsync(user);

        await using (var context = fixture.CreateContext())
        {
            var repository = new UserRepository(context);
            var tracked = await repository.GetByEmailAsync(_email);
            tracked!.ChangePasswordHash("new");
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var stored = await new UserRepository(readContext).GetByIdAsync(user.Id);
        stored!.PasswordHash.Should().Be("new");
    }

    private async Task SaveAsync(User user)
    {
        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);
        await repository.AddAsync(user);
        await repository.SaveChangesAsync();
    }
}
