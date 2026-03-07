using System.Globalization;
using Serilog;
using SkyWatch.Core.Configuration;

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

    builder.Services.Configure<SkyWatchOptions>(
        builder.Configuration.GetSection(SkyWatchOptions.SectionName));

    builder.Services.AddSingleton<ProfileManager>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

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
