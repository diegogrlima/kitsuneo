using Kitsuneo.Application.Phrases;
using Kitsuneo.Application.Practice;
using Kitsuneo.Application.Reviews;
using Kitsuneo.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kitsuneo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<PhraseService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<PracticeService>();

        // Stateless apart from the shared Random used for fuzzing
        services.AddSingleton(new FsrsScheduler());
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
