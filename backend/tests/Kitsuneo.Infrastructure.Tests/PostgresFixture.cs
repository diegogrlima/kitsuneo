using Kitsuneo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Kitsuneo.Infrastructure.Tests;

/// <summary>
/// One PostgreSQL container shared by every test in <see cref="PostgresCollection"/>.
/// Uses the same image as docker-compose and applies the real migrations, so they are tested too.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <param name="database">Another database in the same container, created by migrating it; defaults to the shared one.</param>
    public AppDbContext CreateContext(string? database = null)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString());
        if (database is not null)
            connectionString.Database = database;

        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString.ConnectionString)
                .Options);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
