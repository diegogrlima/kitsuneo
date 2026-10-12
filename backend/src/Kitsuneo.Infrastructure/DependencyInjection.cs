using Kitsuneo.Application.Abstractions;
using Kitsuneo.Infrastructure.Persistence;
using Kitsuneo.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        return services;
    }
}
