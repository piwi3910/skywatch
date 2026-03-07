using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Serilog;
using SkyWatch.Alpaca.Discovery;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Host.Workers;

namespace SkyWatch.IntegrationTests;

/// <summary>
/// Custom web application factory for integration tests that replaces infrastructure
/// dependencies with test doubles to avoid filesystem and native library dependencies.
/// </summary>
public sealed class SkyWatchWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string tempDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkyWatchWebApplicationFactory"/> class.
    /// </summary>
    public SkyWatchWebApplicationFactory()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), $"skywatch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        // Reset the global Serilog logger so each test factory gets a fresh instance
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .CreateLogger();
    }

    /// <summary>
    /// Gets or sets the server name used in Alpaca settings.
    /// </summary>
    public string ServerName { get; set; } = "SkyWatch";

    /// <summary>
    /// Gets or sets the device name used in Alpaca settings.
    /// </summary>
    public string DeviceName { get; set; } = "SkyWatch SafetyMonitor";

    /// <inheritdoc/>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var profilesDir = Path.Combine(tempDirectory, "profiles");
        var logsDir = Path.Combine(tempDirectory, "logs");

        builder.ConfigureServices(services =>
        {
            // Remove only the SkyWatch-specific hosted services, not the GenericWebHostService
            RemoveServicesByImplementation<SkyWatchWorker>(services);
            RemoveServicesByImplementation<AlpacaDiscoveryService>(services);

            // Remove factory-based registrations that depend on native libraries or filesystem
            RemoveAllServices<ProfileManager>(services);
            RemoveAllServices<ProfileSettings>(services);
            RemoveAllServices<IFrameSource>(services);
            RemoveAllServices<IFrameAnalyzer>(services);
            RemoveAllServices<ISafetyEvaluator>(services);

            // Remove Serilog-registered ILoggerFactory to avoid "logger already frozen" errors
            RemoveAllServices<ILoggerFactory>(services);
            services.AddLogging(logging => logging.ClearProviders());

            // Replace with test-safe implementations
            services.AddSingleton(new ProfileSettings());

            var mockFrameSource = new Mock<IFrameSource>();
            mockFrameSource.Setup(f => f.GetStatus()).Returns(new CaptureStatus
            {
                IsConnected = true,
                SourceType = FrameSourceType.Replay,
                SourceDescription = "Test",
            });
            services.AddSingleton(mockFrameSource.Object);
            services.AddSingleton(new Mock<IFrameAnalyzer>().Object);
            services.AddSingleton(new Mock<ISafetyEvaluator>().Object);

            // Configure temp directories and Alpaca settings
            services.Configure<SkyWatchOptions>(options =>
            {
                options.ProfilesDirectory = profilesDir;
                options.LogsDirectory = logsDir;
            });

            services.Configure<AlpacaSettings>(options =>
            {
                options.EnableDiscovery = false;
                options.ServerName = ServerName;
                options.DeviceName = DeviceName;
            });
        });

        return base.CreateHost(builder);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(tempDirectory))
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort cleanup
            }
        }
    }

    private static void RemoveAllServices<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }

    private static void RemoveServicesByImplementation<T>(IServiceCollection services)
    {
        var descriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(T))
            .ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }
}
