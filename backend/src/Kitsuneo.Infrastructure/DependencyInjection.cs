using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Auth;
using Kitsuneo.Infrastructure.Persistence;
using Kitsuneo.Infrastructure.Persistence.Repositories;
using Kitsuneo.Infrastructure.Security;
using Kitsuneo.Infrastructure.Speech;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kitsuneo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Password = configuration["Database:Password"]
        };

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.ConnectionString));

        services.AddScoped<IPhraseRepository, PhraseRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IPracticeAttemptRepository, PracticeAttemptRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITokenBlacklist, TokenBlacklist>();

        // Fails at startup, not at the first login, when the signing key is missing or too short
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                o => o.SigningKey is { Length: >= JwtOptions.MinSigningKeyLength },
                $"Jwt:SigningKey must be set (user-secrets) with at least {JwtOptions.MinSigningKeyLength} characters.")
            .ValidateOnStart();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton(provider =>
            new RefreshTokenSettings(provider.GetRequiredService<IOptions<JwtOptions>>().Value.RefreshTokenLifetime));

        services.Configure<ElevenLabsOptions>(configuration.GetSection(ElevenLabsOptions.SectionName));
        services.AddHttpClient<ITextToSpeech, ElevenLabsTextToSpeech>((provider, client) =>
        {
            client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<ElevenLabsOptions>>().Value.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
