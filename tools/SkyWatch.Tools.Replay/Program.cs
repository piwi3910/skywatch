using Microsoft.Extensions.Logging;
using SkyWatch.Core.Configuration;
using SkyWatch.Tools.Replay;

const string usage = """
    Usage: SkyWatch.Tools.Replay --source <dir> [--profile <name>] [--output <report.json>]

    Options:
      --source    Directory containing images to replay (required)
      --profile   Profile name to use (default: uses default settings)
      --output    Path for JSON report output (default: stdout summary only)
    """;

string? sourceDirectory = null;
string? profileName = null;
string? outputPath = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--source" when i + 1 < args.Length:
            sourceDirectory = args[++i];
            break;
        case "--profile" when i + 1 < args.Length:
            profileName = args[++i];
            break;
        case "--output" when i + 1 < args.Length:
            outputPath = args[++i];
            break;
        case "--help" or "-h":
            Console.WriteLine(usage);
            return 0;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
            Console.Error.WriteLine(usage);
            return 1;
    }
}

if (string.IsNullOrEmpty(sourceDirectory))
{
    Console.Error.WriteLine("Error: --source is required.");
    Console.Error.WriteLine(usage);
    return 1;
}

var fullSourcePath = Path.GetFullPath(sourceDirectory);

if (!Directory.Exists(fullSourcePath))
{
    Console.Error.WriteLine($"Error: Source directory does not exist: {fullSourcePath}");
    return 1;
}

var profileSettings = new ProfileSettings();

if (!string.IsNullOrEmpty(profileName))
{
    var profilesDir = Path.Combine(AppContext.BaseDirectory, "profiles");
    var profilePath = Path.Combine(profilesDir, $"{profileName}.json");

    if (File.Exists(profilePath))
    {
        var loaded = await ProfileLoader.LoadFromFileAsync(profilePath);

        if (loaded is not null)
        {
            profileSettings = loaded;
        }
    }
    else
    {
        Console.Error.WriteLine($"Warning: Profile '{profileName}' not found at {profilePath}, using default settings.");
    }

    profileSettings.Name = profileName;
}

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Information);
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
});

var logger = loggerFactory.CreateLogger<ReplayRunner>();
var runner = new ReplayRunner(fullSourcePath, profileSettings, logger);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    var report = await runner.RunAsync(cts.Token);

    if (!string.IsNullOrEmpty(outputPath))
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        await ReplayRunner.WriteJsonReportAsync(report, fullOutputPath, CancellationToken.None);
        Console.WriteLine($"Report written to: {fullOutputPath}");
    }

    ReplayRunner.PrintSummary(report);
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Replay cancelled.");
    return 130;
}
catch (DirectoryNotFoundException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
