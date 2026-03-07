using System.Globalization;
using Serilog;
using SkyWatch.Host.Extensions;
using SkyWatch.Web.Extensions;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting SkyWatch");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
            formatProvider: CultureInfo.InvariantCulture)
        .WriteTo.File(
            Path.Combine(
                context.Configuration.GetSection("SkyWatch").GetValue<string>("LogsDirectory") ?? "logs",
                "skywatch-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            formatProvider: CultureInfo.InvariantCulture));

    // Register all SkyWatch services
    builder.Services.AddSkyWatchServices(builder.Configuration);

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseSkyWatchWeb();
    app.MapControllers();

    app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow }));

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Entry point for the SkyWatch application. Partial declaration enables access from integration tests.
/// </summary>
public partial class Program
{
}
