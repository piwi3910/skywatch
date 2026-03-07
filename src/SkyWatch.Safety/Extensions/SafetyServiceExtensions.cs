using Microsoft.Extensions.DependencyInjection;
using SkyWatch.Contracts.Interfaces;

namespace SkyWatch.Safety.Extensions;

public static class SafetyServiceExtensions
{
    public static IServiceCollection AddSkyWatchSafety(this IServiceCollection services)
    {
        services.AddSingleton<ISafetyStateStore, SafetyStateStore>();

        // ISafetyEvaluator registration needs ProfileSettings, done in Host
        return services;
    }
}
