using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
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

        // Serve static files from SkyWatch.Web's output directory
        var webAssemblyPath = Path.GetDirectoryName(typeof(WebServiceExtensions).Assembly.Location)!;
        var webRoot = Path.Combine(webAssemblyPath, "wwwroot");
        if (Directory.Exists(webRoot))
        {
            var fileProvider = new PhysicalFileProvider(webRoot);

            // SPA fallback: rewrite to index.html for client-side routes
            app.Use(async (context, next) =>
            {
                // Skip API, WebSocket, health, and file extension routes
                if (!context.Request.Path.StartsWithSegments("/api")
                    && !context.Request.Path.StartsWithSegments("/ws")
                    && !context.Request.Path.StartsWithSegments("/health")
                    && !Path.HasExtension(context.Request.Path.Value))
                {
                    // Check if the file exists; if not, rewrite to index.html
                    var filePath = Path.Combine(webRoot, context.Request.Path.Value?.TrimStart('/') ?? string.Empty);
                    if (!File.Exists(filePath))
                    {
                        context.Request.Path = "/index.html";
                    }
                }

                await next(context);
            });

            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
        }

        return app;
    }
}
