using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tests.Core;

public sealed class ProfileManagerTests : IDisposable
{
    private readonly string tempDir;
    private readonly ProfileManager profileManager;

    public ProfileManagerTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var options = Options.Create(new SkyWatchOptions { ProfilesDirectory = tempDir });
        var logger = NullLogger<ProfileManager>.Instance;
        profileManager = new ProfileManager(options, logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAndLoadRoundTrip()
    {
        var profile = new ProfileSettings
        {
            Name = "test-profile",
            Description = "A test profile",
            Capture = new CaptureSettings
            {
                FrameIntervalSeconds = 5,
                TargetWidth = 1920,
                TargetHeight = 1080,
                RotationDegrees = 90,
            },
            Thresholds = new ThresholdSettings
            {
                MaxAverageBrightness = 180.0,
                MinStarCount = 20,
            },
            Timing = new TimingSettings
            {
                WarmupSeconds = 60,
                UnsafeDebounceSeconds = 15,
            },
        };

        await profileManager.SaveAsync(profile, CancellationToken.None);
        var loaded = await profileManager.LoadAsync("test-profile", CancellationToken.None);

        Assert.Equal(profile.Name, loaded.Name);
        Assert.Equal(profile.Description, loaded.Description);
        Assert.Equal(profile.Capture.FrameIntervalSeconds, loaded.Capture.FrameIntervalSeconds);
        Assert.Equal(profile.Capture.TargetWidth, loaded.Capture.TargetWidth);
        Assert.Equal(profile.Capture.TargetHeight, loaded.Capture.TargetHeight);
        Assert.Equal(profile.Capture.RotationDegrees, loaded.Capture.RotationDegrees);
        Assert.Equal(profile.Thresholds.MaxAverageBrightness, loaded.Thresholds.MaxAverageBrightness);
        Assert.Equal(profile.Thresholds.MinStarCount, loaded.Thresholds.MinStarCount);
        Assert.Equal(profile.Timing.WarmupSeconds, loaded.Timing.WarmupSeconds);
        Assert.Equal(profile.Timing.UnsafeDebounceSeconds, loaded.Timing.UnsafeDebounceSeconds);
    }

    [Fact]
    public async Task LoadAsyncNonexistentProfileThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => profileManager.LoadAsync("nonexistent", CancellationToken.None));
    }

    [Fact]
    public async Task ListProfilesReturnsAllSavedProfiles()
    {
        var profiles = new[] { "alpha", "beta", "gamma" };
        foreach (var name in profiles)
        {
            var profile = new ProfileSettings { Name = name };
            await profileManager.SaveAsync(profile, CancellationToken.None);
        }

        var listed = profileManager.ListProfiles();

        Assert.Equal(3, listed.Count);
        Assert.Contains("alpha", listed);
        Assert.Contains("beta", listed);
        Assert.Contains("gamma", listed);
    }

    [Fact]
    public void ListProfilesEmptyDirectoryReturnsEmptyList()
    {
        var listed = profileManager.ListProfiles();

        Assert.Empty(listed);
    }

    [Fact]
    public async Task LoadOrCreateDefaultAsyncCreatesDefaultWhenNoneExists()
    {
        var profile = await profileManager.LoadOrCreateDefaultAsync(CancellationToken.None);

        Assert.Equal("default", profile.Name);
        Assert.True(File.Exists(Path.Combine(tempDir, "default.json")));
    }

    [Fact]
    public async Task LoadOrCreateDefaultAsyncLoadsExistingWhenExists()
    {
        var custom = new ProfileSettings
        {
            Name = "default",
            Description = "Custom default",
        };
        await profileManager.SaveAsync(custom, CancellationToken.None);

        var loaded = await profileManager.LoadOrCreateDefaultAsync(CancellationToken.None);

        Assert.Equal("Custom default", loaded.Description);
    }

    [Fact]
    public async Task SaveAsyncOverwritesExisting()
    {
        var profile = new ProfileSettings
        {
            Name = "overwrite-test",
            Description = "Original",
        };
        await profileManager.SaveAsync(profile, CancellationToken.None);

        profile.Description = "Updated";
        await profileManager.SaveAsync(profile, CancellationToken.None);

        var loaded = await profileManager.LoadAsync("overwrite-test", CancellationToken.None);
        Assert.Equal("Updated", loaded.Description);
    }
}
