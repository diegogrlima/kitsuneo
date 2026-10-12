using Kitsuneo.Application.Phrases;
using Microsoft.Extensions.DependencyInjection;

namespace Kitsuneo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<PhraseService>();

        return services;
    }
}
