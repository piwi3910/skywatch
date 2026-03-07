using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SkyWatch.Web.Controllers;
using SkyWatch.Web.WebSocket;

namespace SkyWatch.Web.Extensions;

/// <summary>
/// Extension methods for registering SkyWatch Web services and middleware.
/// </summary>
public static class WebServiceExtensions
{
    /// <summary>
    /// Adds SkyWatch Web controllers to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSkyWatchWeb(this IServiceCollection services)
    {
        services.AddSingleton<WebSocketConnectionManager>();
        services.AddSingleton<SkyWatchWebSocketHandler>();
        services.AddControllers()
            .AddApplicationPart(typeof(DashboardApiController).Assembly);
        return services;
    }

    /// <summary>
    /// Configures the application to serve SkyWatch Web static files and default pages.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseSkyWatchWeb(this IApplicationBuilder app)
    {
        app.UseWebSockets();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws/live")
            {
                var handler = context.RequestServices.GetRequiredService<SkyWatchWebSocketHandler>();
                await handler.HandleAsync(context);
            }
            else
            {
                await next(context);
            }
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();
        return app;
    }
}
