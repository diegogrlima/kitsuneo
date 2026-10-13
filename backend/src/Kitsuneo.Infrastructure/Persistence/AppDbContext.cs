using Kitsuneo.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kitsuneo.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RevokedAccessToken> RevokedAccessTokens => Set<RevokedAccessToken>();
    public DbSet<Phrase> Phrases => Set<Phrase>();
    public DbSet<ReviewSchedule> ReviewSchedules => Set<ReviewSchedule>();
    public DbSet<PracticeAttempt> PracticeAttempts => Set<PracticeAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Stores every enum by name, so the database stays readable and reordering values is safe
        configurationBuilder.Properties<Enum>()
            .HaveConversion<string>()
            .HaveMaxLength(50);
    }
}
