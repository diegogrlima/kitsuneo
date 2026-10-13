using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Kitsuneo.Infrastructure.Tests.Persistence.Migrations;

[Collection(PostgresCollection.Name)]
public class AddUsersAndAuthMigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Up_WithPhrasesOfPlaceholderUser_DropsThemWithSchedulesAndAttempts()
    {
        // A database of its own, stopped at the migration before this one
        await using var context = fixture.CreateContext($"migration_{Guid.NewGuid():N}");
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("AddPracticeAttempts");

        var phraseId = Guid.NewGuid();
        var placeholderUser = Guid.NewGuid();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO phrases ("Id", "UserId", "Language", "Text", "SpokenForm", "IsArchived", "CreatedAt")
            VALUES ({phraseId}, {placeholderUser}, 'English', 'Old phrase', 'Old phrase', false, {now});
            INSERT INTO review_schedules ("PhraseId", "State", "Step", "DueAt")
            VALUES ({phraseId}, 'Learning', 0, {now});
            INSERT INTO practice_attempts ("Id", "PhraseId", "UserId", "Answer", "Accuracy", "PlayCount", "UsedSlowSpeed", "DurationMs", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {phraseId}, {placeholderUser}, 'Old', 100, 1, false, 1000, {now});
            """);

        await migrator.MigrateAsync();

        (await context.Phrases.IgnoreAutoIncludes().CountAsync()).Should().Be(0);
        (await context.ReviewSchedules.CountAsync()).Should().Be(0);
        (await context.PracticeAttempts.CountAsync()).Should().Be(0);

        await context.Database.EnsureDeletedAsync();
    }
}
