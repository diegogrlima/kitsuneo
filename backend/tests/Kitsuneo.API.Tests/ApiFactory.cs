using Kitsuneo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Kitsuneo.API.Tests;

/// <summary>
/// The whole API in memory, on a PostgreSQL container with the real migrations.
/// Runs as "Testing", so no user-secrets (database password, ElevenLabs key) are read.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DatabasePassword = "api-tests";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithPassword(DatabasePassword)
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The API adds Database:Password to the connection string itself, as it does with user-secrets
        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Password = null };

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString.ConnectionString);
        builder.UseSetting("Database:Password", DatabasePassword);
        builder.UseSetting("Jwt:SigningKey", "api-tests-signing-key-with-32-chars!");
        builder.UseSetting("Auth:RateLimitPerMinute", "1000");
    }

    /// <summary>A client over HTTPS, so the Secure refresh cookie is accepted, that leaves cookies to the test.</summary>
    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false
    });

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
