using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SkyWatch.Core.Configuration;

/// <summary>
/// Manages loading, saving, and listing of profile settings as JSON files.
/// </summary>
public sealed partial class ProfileManager
{
    private readonly string profilesDirectory;
    private readonly ILogger<ProfileManager> logger;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public ProfileManager(IOptions<SkyWatchOptions> options, ILogger<ProfileManager> logger)
    {
        profilesDirectory = options.Value.ProfilesDirectory;
        this.logger = logger;
        Directory.CreateDirectory(profilesDirectory);
    }

    public async Task<ProfileSettings> LoadAsync(string profileName, CancellationToken cancellationToken)
    {
        var path = GetProfilePath(profileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Profile '{profileName}' not found at {path}");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var profile = JsonSerializer.Deserialize<ProfileSettings>(json, jsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize profile '{profileName}'");

        LogProfileLoaded(profileName, path);
        return profile;
    }

    public async Task SaveAsync(ProfileSettings profile, CancellationToken cancellationToken)
    {
        var path = GetProfilePath(profile.Name);
        var json = JsonSerializer.Serialize(profile, jsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        LogProfileSaved(profile.Name, path);
    }

    public IReadOnlyList<string> ListProfiles()
    {
        if (!Directory.Exists(profilesDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(profilesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    public async Task<ProfileSettings> LoadOrCreateDefaultAsync(CancellationToken cancellationToken)
    {
        var defaultPath = GetProfilePath("default");
        if (File.Exists(defaultPath))
        {
            return await LoadAsync("default", cancellationToken);
        }

        var defaultProfile = new ProfileSettings();
        await SaveAsync(defaultProfile, cancellationToken);
        LogDefaultProfileCreated(defaultPath);
        return defaultProfile;
    }

    private string GetProfilePath(string profileName)
    {
        return Path.Combine(profilesDirectory, $"{profileName}.json");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded profile '{ProfileName}' from {Path}")]
    private partial void LogProfileLoaded(string profileName, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved profile '{ProfileName}' to {Path}")]
    private partial void LogProfileSaved(string profileName, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created default profile at {Path}")]
    private partial void LogDefaultProfileCreated(string path);
}
