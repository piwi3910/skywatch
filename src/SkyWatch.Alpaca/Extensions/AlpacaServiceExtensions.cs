using Microsoft.Extensions.DependencyInjection;
using SkyWatch.Alpaca.Controllers;
using SkyWatch.Alpaca.Discovery;

namespace SkyWatch.Alpaca.Extensions;

public static class AlpacaServiceExtensions
{
    public static IServiceCollection AddSkyWatchAlpaca(this IServiceCollection services)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(SafetyMonitorController).Assembly);
        services.AddHostedService<AlpacaDiscoveryService>();
        return services;
    }
}
