using System.Text.Json;
using System.Text.Json.Serialization;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tools.Replay;

/// <summary>
/// Loads profile settings from a JSON file.
/// </summary>
internal static class ProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Loads a profile from the specified file path.
    /// </summary>
    /// <param name="profilePath">The path to the profile JSON file.</param>
    /// <returns>The loaded profile settings, or null if deserialization fails.</returns>
    public static async Task<ProfileSettings?> LoadFromFileAsync(string profilePath)
    {
        var json = await File.ReadAllTextAsync(profilePath);
        return JsonSerializer.Deserialize<ProfileSettings>(json, JsonOptions);
    }
}
