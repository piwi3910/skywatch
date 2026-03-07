using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyWatch.Capture;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Core.Validation;
using SkyWatch.Web.Models;

namespace SkyWatch.Web.Controllers;

/// <summary>
/// Provides configuration management endpoints for profiles, masks, capture, thresholds, timing, and overrides.
/// </summary>
[ApiController]
[Route("api/config")]
public sealed partial class ConfigurationApiController : ControllerBase
{
    private readonly ProfileManager profileManager;
    private readonly FrameSourceManager frameSourceManager;
    private readonly IOptionsMonitor<SkyWatchOptions> options;
    private readonly ILogger<ConfigurationApiController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationApiController"/> class.
    /// </summary>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="frameSourceManager">The frame source manager.</param>
    /// <param name="options">The SkyWatch options.</param>
    /// <param name="logger">The logger.</param>
    public ConfigurationApiController(
        ProfileManager profileManager,
        FrameSourceManager frameSourceManager,
        IOptionsMonitor<SkyWatchOptions> options,
        ILogger<ConfigurationApiController> logger)
    {
        this.profileManager = profileManager;
        this.frameSourceManager = frameSourceManager;
        this.options = options;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the current active profile settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current profile settings as JSON.</returns>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        LogProfileRequested();

        var activeProfile = options.CurrentValue.ActiveProfile;
        var profile = await profileManager.LoadOrCreateDefaultAsync(cancellationToken);

        if (!string.IsNullOrEmpty(activeProfile))
        {
            try
            {
                profile = await profileManager.LoadAsync(activeProfile, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                LogProfileNotFound(activeProfile);
            }
        }

        return Ok(profile);
    }

    /// <summary>
    /// Updates and saves the current profile settings after validation.
    /// </summary>
    /// <param name="profile">The profile settings to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved profile or validation errors.</returns>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileSettings profile, CancellationToken cancellationToken)
    {
        LogProfileUpdateRequested(profile.Name);

        var errors = SettingsValidator.Validate(profile);
        if (errors.Count > 0)
        {
            LogProfileValidationFailed(profile.Name, errors.Count);
            return BadRequest(new { errors });
        }

        await profileManager.SaveAsync(profile, cancellationToken);
        LogProfileSaved(profile.Name);
        return Ok(profile);
    }

    /// <summary>
    /// Lists all available profile names.
    /// </summary>
    /// <returns>A list of profile names.</returns>
    [HttpGet("profiles")]
    public IActionResult ListProfiles()
    {
        LogProfilesListRequested();

        var profiles = profileManager.ListProfiles();
        return Ok(profiles);
    }

    /// <summary>
    /// Activates a profile by name.
    /// </summary>
    /// <param name="name">The profile name to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activated profile.</returns>
    [HttpPost("profiles/{name}/activate")]
    public async Task<IActionResult> ActivateProfile(string name, CancellationToken cancellationToken)
    {
        LogProfileActivationRequested(name);

        try
        {
            var profile = await profileManager.LoadAsync(name, cancellationToken);
            LogProfileActivated(name);
            return Ok(profile);
        }
        catch (FileNotFoundException)
        {
            LogProfileNotFound(name);
            return NotFound(new { error = $"Profile '{name}' not found" });
        }
    }

    /// <summary>
    /// Gets the current masks list from the active profile.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of mask definitions.</returns>
    [HttpGet("masks")]
    public async Task<IActionResult> GetMasks(CancellationToken cancellationToken)
    {
        LogMasksRequested();

        var profile = await LoadActiveProfileAsync(cancellationToken);
        return Ok(profile.Masks);
    }

    /// <summary>
    /// Updates the masks in the active profile.
    /// </summary>
    /// <param name="masks">The updated mask definitions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated masks list.</returns>
    [HttpPut("masks")]
    public async Task<IActionResult> UpdateMasks([FromBody] List<MaskDefinition> masks, CancellationToken cancellationToken)
    {
        LogMasksUpdateRequested(masks.Count);

        var profile = await LoadActiveProfileAsync(cancellationToken);

        var updatedProfile = new ProfileSettings
        {
            Name = profile.Name,
            Description = profile.Description,
            Capture = profile.Capture,
            Thresholds = profile.Thresholds,
            Timing = profile.Timing,
            Masks = masks,
            ManualOverride = profile.ManualOverride,
        };

        var errors = SettingsValidator.Validate(updatedProfile);
        if (errors.Count > 0)
        {
            LogMasksValidationFailed(errors.Count);
            return BadRequest(new { errors });
        }

        await profileManager.SaveAsync(updatedProfile, cancellationToken);
        LogMasksSaved(masks.Count);
        return Ok(masks);
    }

    /// <summary>
    /// Gets the current capture settings from the active profile.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capture settings.</returns>
    [HttpGet("capture")]
    public async Task<IActionResult> GetCapture(CancellationToken cancellationToken)
    {
        LogCaptureRequested();

        var profile = await LoadActiveProfileAsync(cancellationToken);
        return Ok(profile.Capture);
    }

    /// <summary>
    /// Updates the capture settings in the active profile and switches the frame source.
    /// </summary>
    /// <param name="capture">The updated capture settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated capture settings or validation errors.</returns>
    [HttpPost("capture")]
    public async Task<IActionResult> UpdateCapture([FromBody] CaptureSettings capture, CancellationToken cancellationToken)
    {
        LogCaptureUpdateRequested();

        var profile = await LoadActiveProfileAsync(cancellationToken);

        var updatedProfile = new ProfileSettings
        {
            Name = profile.Name,
            Description = profile.Description,
            Capture = capture,
            Thresholds = profile.Thresholds,
            Timing = profile.Timing,
            Masks = profile.Masks,
            ManualOverride = profile.ManualOverride,
        };

        var errors = SettingsValidator.Validate(updatedProfile);
        if (errors.Count > 0)
        {
            LogCaptureValidationFailed(errors.Count);
            return BadRequest(new { errors });
        }

        await frameSourceManager.SwitchSourceAsync(capture, cancellationToken);
        await profileManager.SaveAsync(updatedProfile, cancellationToken);
        LogCaptureSaved();
        return Ok(capture);
    }

    /// <summary>
    /// Gets the current threshold settings from the active profile.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The threshold settings.</returns>
    [HttpGet("thresholds")]
    public async Task<IActionResult> GetThresholds(CancellationToken cancellationToken)
    {
        LogThresholdsRequested();

        var profile = await LoadActiveProfileAsync(cancellationToken);
        return Ok(profile.Thresholds);
    }

    /// <summary>
    /// Updates the threshold settings in the active profile.
    /// </summary>
    /// <param name="thresholds">The updated threshold settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated threshold settings or validation errors.</returns>
    [HttpPut("thresholds")]
    public async Task<IActionResult> UpdateThresholds([FromBody] ThresholdSettings thresholds, CancellationToken cancellationToken)
    {
        LogThresholdsUpdateRequested();

        var profile = await LoadActiveProfileAsync(cancellationToken);

        var updatedProfile = new ProfileSettings
        {
            Name = profile.Name,
            Description = profile.Description,
            Capture = profile.Capture,
            Thresholds = thresholds,
            Timing = profile.Timing,
            Masks = profile.Masks,
            ManualOverride = profile.ManualOverride,
        };

        var errors = SettingsValidator.Validate(updatedProfile);
        if (errors.Count > 0)
        {
            LogThresholdsValidationFailed(errors.Count);
            return BadRequest(new { errors });
        }

        await profileManager.SaveAsync(updatedProfile, cancellationToken);
        LogThresholdsSaved();
        return Ok(thresholds);
    }

    /// <summary>
    /// Gets the current timing settings from the active profile.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The timing settings.</returns>
    [HttpGet("timing")]
    public async Task<IActionResult> GetTiming(CancellationToken cancellationToken)
    {
        LogTimingRequested();

        var profile = await LoadActiveProfileAsync(cancellationToken);
        return Ok(profile.Timing);
    }

    /// <summary>
    /// Updates the timing settings in the active profile.
    /// </summary>
    /// <param name="timing">The updated timing settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated timing settings or validation errors.</returns>
    [HttpPut("timing")]
    public async Task<IActionResult> UpdateTiming([FromBody] TimingSettings timing, CancellationToken cancellationToken)
    {
        LogTimingUpdateRequested();

        var profile = await LoadActiveProfileAsync(cancellationToken);

        var updatedProfile = new ProfileSettings
        {
            Name = profile.Name,
            Description = profile.Description,
            Capture = profile.Capture,
            Thresholds = profile.Thresholds,
            Timing = timing,
            Masks = profile.Masks,
            ManualOverride = profile.ManualOverride,
        };

        var errors = SettingsValidator.Validate(updatedProfile);
        if (errors.Count > 0)
        {
            LogTimingValidationFailed(errors.Count);
            return BadRequest(new { errors });
        }

        await profileManager.SaveAsync(updatedProfile, cancellationToken);
        LogTimingSaved();
        return Ok(timing);
    }

    /// <summary>
    /// Gets the current manual override mode from the active profile.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current override mode.</returns>
    [HttpGet("override")]
    public async Task<IActionResult> GetOverride(CancellationToken cancellationToken)
    {
        LogOverrideRequested();

        var profile = await LoadActiveProfileAsync(cancellationToken);
        return Ok(new { mode = profile.ManualOverride.ToString() });
    }

    /// <summary>
    /// Updates the manual override mode in the active profile.
    /// </summary>
    /// <param name="request">The override request containing the new mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated override mode or validation errors.</returns>
    [HttpPut("override")]
    public async Task<IActionResult> UpdateOverride([FromBody] ManualOverrideRequest request, CancellationToken cancellationToken)
    {
        LogOverrideUpdateRequested(request.Mode.ToString());

        var profile = await LoadActiveProfileAsync(cancellationToken);

        var updatedProfile = new ProfileSettings
        {
            Name = profile.Name,
            Description = profile.Description,
            Capture = profile.Capture,
            Thresholds = profile.Thresholds,
            Timing = profile.Timing,
            Masks = profile.Masks,
            ManualOverride = request.Mode,
        };

        var errors = SettingsValidator.Validate(updatedProfile);
        if (errors.Count > 0)
        {
            LogOverrideValidationFailed(errors.Count);
            return BadRequest(new { errors });
        }

        await profileManager.SaveAsync(updatedProfile, cancellationToken);
        LogOverrideSaved(request.Mode.ToString());
        return Ok(new { mode = request.Mode.ToString() });
    }

    private async Task<ProfileSettings> LoadActiveProfileAsync(CancellationToken cancellationToken)
    {
        var activeProfile = options.CurrentValue.ActiveProfile;

        if (!string.IsNullOrEmpty(activeProfile))
        {
            try
            {
                return await profileManager.LoadAsync(activeProfile, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                LogProfileNotFound(activeProfile);
            }
        }

        return await profileManager.LoadOrCreateDefaultAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Profile settings requested")]
    private partial void LogProfileRequested();

    [LoggerMessage(Level = LogLevel.Information, Message = "Profile update requested for '{ProfileName}'")]
    private partial void LogProfileUpdateRequested(string profileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Profile validation failed for '{ProfileName}' with {ErrorCount} errors")]
    private partial void LogProfileValidationFailed(string profileName, int errorCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Profile '{ProfileName}' saved successfully")]
    private partial void LogProfileSaved(string profileName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Profile list requested")]
    private partial void LogProfilesListRequested();

    [LoggerMessage(Level = LogLevel.Information, Message = "Profile activation requested for '{ProfileName}'")]
    private partial void LogProfileActivationRequested(string profileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Profile '{ProfileName}' activated")]
    private partial void LogProfileActivated(string profileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Profile '{ProfileName}' not found")]
    private partial void LogProfileNotFound(string profileName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Masks requested")]
    private partial void LogMasksRequested();

    [LoggerMessage(Level = LogLevel.Information, Message = "Masks update requested with {MaskCount} masks")]
    private partial void LogMasksUpdateRequested(int maskCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Masks validation failed with {ErrorCount} errors")]
    private partial void LogMasksValidationFailed(int errorCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Masks saved ({MaskCount} masks)")]
    private partial void LogMasksSaved(int maskCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Capture settings requested")]
    private partial void LogCaptureRequested();

    [LoggerMessage(Level = LogLevel.Information, Message = "Capture settings update requested")]
    private partial void LogCaptureUpdateRequested();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Capture settings validation failed with {ErrorCount} errors")]
    private partial void LogCaptureValidationFailed(int errorCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Capture settings saved and frame source switched")]
    private partial void LogCaptureSaved();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Threshold settings requested")]
    private partial void LogThresholdsRequested();

    [LoggerMessage(Level = LogLevel.Information, Message = "Threshold settings update requested")]
    private partial void LogThresholdsUpdateRequested();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Threshold settings validation failed with {ErrorCount} errors")]
    private partial void LogThresholdsValidationFailed(int errorCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Threshold settings saved")]
    private partial void LogThresholdsSaved();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Timing settings requested")]
    private partial void LogTimingRequested();

    [LoggerMessage(Level = LogLevel.Information, Message = "Timing settings update requested")]
    private partial void LogTimingUpdateRequested();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timing settings validation failed with {ErrorCount} errors")]
    private partial void LogTimingValidationFailed(int errorCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Timing settings saved")]
    private partial void LogTimingSaved();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Override mode requested")]
    private partial void LogOverrideRequested();

    [LoggerMessage(Level = LogLevel.Information, Message = "Override mode update requested to '{OverrideMode}'")]
    private partial void LogOverrideUpdateRequested(string overrideMode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Override mode validation failed with {ErrorCount} errors")]
    private partial void LogOverrideValidationFailed(int errorCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Override mode saved as '{OverrideMode}'")]
    private partial void LogOverrideSaved(string overrideMode);
}
