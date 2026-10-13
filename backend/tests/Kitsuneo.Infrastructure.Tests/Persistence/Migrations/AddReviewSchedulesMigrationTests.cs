using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Kitsuneo.Infrastructure.Tests.Persistence.Migrations;

[Collection(PostgresCollection.Name)]
public class AddReviewSchedulesMigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Up_WithExistingPhrases_CreatesNewScheduleForEach()
    {
        // A database of its own, stopped at the migration before this one
        await using var context = fixture.CreateContext($"migration_{Guid.NewGuid():N}");
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("AddPhraseListIndexes");

        var phraseId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO phrases ("Id", "UserId", "Language", "Text", "SpokenForm", "IsArchived", "CreatedAt")
            VALUES ({phraseId}, {Guid.NewGuid()}, 'English', 'Old phrase', 'Old phrase', false, {createdAt})
            """);

        // Stops at this migration: AddUsersAndAuth, later on, drops phrases owned by no account
        await migrator.MigrateAsync("AddReviewSchedules");

        var schedule = await context.ReviewSchedules.AsNoTracking().SingleAsync();
        schedule.Should().BeEquivalentTo(new
        {
            PhraseId = phraseId,
            State = Core.Enums.ReviewState.Learning,
            Step = 0,
            Stability = (double?)null,
            Difficulty = (double?)null,
            DueAt = createdAt,
            LastReviewedAt = (DateTime?)null
        });

        await context.Database.EnsureDeletedAsync();
    }
}
