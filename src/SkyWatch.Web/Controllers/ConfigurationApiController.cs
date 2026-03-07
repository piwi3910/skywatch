using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Core.Validation;

namespace SkyWatch.Web.Controllers;

/// <summary>
/// Provides configuration management endpoints for profiles and masks.
/// </summary>
[ApiController]
[Route("api/config")]
public sealed partial class ConfigurationApiController : ControllerBase
{
    private readonly ProfileManager profileManager;
    private readonly IOptionsMonitor<SkyWatchOptions> options;
    private readonly ILogger<ConfigurationApiController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationApiController"/> class.
    /// </summary>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="options">The SkyWatch options.</param>
    /// <param name="logger">The logger.</param>
    public ConfigurationApiController(
        ProfileManager profileManager,
        IOptionsMonitor<SkyWatchOptions> options,
        ILogger<ConfigurationApiController> logger)
    {
        this.profileManager = profileManager;
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
}
