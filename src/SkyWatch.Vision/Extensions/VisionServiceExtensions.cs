using Microsoft.Extensions.DependencyInjection;

namespace SkyWatch.Vision.Extensions;

/// <summary>
/// Extension methods for registering Vision module services.
/// </summary>
public static class VisionServiceExtensions
{
    /// <summary>
    /// Adds SkyWatch Vision services to the service collection.
    /// IFrameAnalyzer registration requires ProfileSettings and is done in the Host project.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSkyWatchVision(this IServiceCollection services)
    {
        return services;
    }
}
