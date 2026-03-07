using Microsoft.Extensions.Options;
using SkyWatch.Alpaca.Extensions;
using SkyWatch.Capture;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Core.Configuration;
using SkyWatch.Host.Workers;
using SkyWatch.Safety;
using SkyWatch.Vision;
using SkyWatch.Web.Extensions;

namespace SkyWatch.Host.Extensions;

/// <summary>
/// Registers all SkyWatch services into the dependency injection container.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Adds all SkyWatch services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSkyWatchServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core configuration
        services.Configure<SkyWatchOptions>(configuration.GetSection(SkyWatchOptions.SectionName));
        services.Configure<AlpacaSettings>(configuration.GetSection("Alpaca"));
        services.AddSingleton<ProfileManager>();

        // Load active profile settings via factory
        services.AddSingleton(sp =>
        {
            var profileManager = sp.GetRequiredService<ProfileManager>();
            return profileManager.LoadOrCreateDefaultAsync(CancellationToken.None)
                .GetAwaiter().GetResult();
        });

        // HTTP client factory (used by Alpaca camera source)
        services.AddHttpClient("AlpacaCamera");

        // Capture
        services.AddSingleton<FrameSourceManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<FrameSourceManager>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var httpClientFactory = sp.GetService<IHttpClientFactory>();
            var manager = new FrameSourceManager(logger, loggerFactory, httpClientFactory);
            var profile = sp.GetRequiredService<ProfileSettings>();
            manager.InitializeAsync(profile.Capture, CancellationToken.None).GetAwaiter().GetResult();
            return manager;
        });
        services.AddSingleton<IFrameSource>(sp => sp.GetRequiredService<FrameSourceManager>());

        // Vision
        services.AddSingleton<IFrameAnalyzer>(sp =>
        {
            var profile = sp.GetRequiredService<ProfileSettings>();
            var logger = sp.GetRequiredService<ILogger<FrameAnalyzerPipeline>>();
            return new FrameAnalyzerPipeline(profile, logger);
        });

        // Safety
        services.AddSingleton<ISafetyStateStore, SafetyStateStore>();
        services.AddSingleton<ISafetyEvaluator>(sp =>
        {
            var profile = sp.GetRequiredService<ProfileSettings>();
            var logger = sp.GetRequiredService<ILogger<SafetyEvaluatorEngine>>();
            return new SafetyEvaluatorEngine(profile, logger);
        });

        // Alpaca (controllers + discovery)
        services.AddSkyWatchAlpaca();

        // Web (controllers + static files registration)
        services.AddSkyWatchWeb();

        // Background worker
        services.AddHostedService<SkyWatchWorker>();

        return services;
    }
}
