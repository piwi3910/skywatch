using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SkyWatch.Contracts.Interfaces;

namespace SkyWatch.Web.Controllers;

/// <summary>
/// Provides dashboard status and metrics history endpoints.
/// </summary>
[ApiController]
[Route("api/dashboard")]
public sealed partial class DashboardApiController : ControllerBase
{
    private static readonly string Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
    private static readonly DateTimeOffset StartTime = DateTimeOffset.UtcNow;

    private readonly ISafetyStateStore safetyStateStore;
    private readonly IFrameSource? frameSource;
    private readonly ILogger<DashboardApiController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardApiController"/> class.
    /// </summary>
    /// <param name="safetyStateStore">The safety state store.</param>
    /// <param name="serviceProvider">The service provider for optional services.</param>
    /// <param name="logger">The logger.</param>
    public DashboardApiController(
        ISafetyStateStore safetyStateStore,
        IServiceProvider serviceProvider,
        ILogger<DashboardApiController> logger)
    {
        this.safetyStateStore = safetyStateStore;
        frameSource = serviceProvider.GetService(typeof(IFrameSource)) as IFrameSource;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the current dashboard status including safety state, capture status, and uptime.
    /// </summary>
    /// <returns>The current dashboard status.</returns>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        LogStatusRequested();

        var evaluation = safetyStateStore.Current;
        var captureStatus = frameSource?.GetStatus();

        var status = new
        {
            safetyState = evaluation.State.ToString(),
            evaluation.IsSafe,
            reasons = evaluation.Reasons.ToString(),
            evaluation.Confidence,
            evaluation.Explanation,
            evaluatedAt = evaluation.EvaluatedAt,
            captureStatus = captureStatus is null
                ? null
                : new
                {
                    captureStatus.IsConnected,
                    sourceType = captureStatus.SourceType.ToString(),
                    captureStatus.SourceDescription,
                    captureStatus.LastFrameAt,
                    captureStatus.FramesCaptured,
                    captureStatus.ErrorMessage,
                },
            uptime = (DateTimeOffset.UtcNow - StartTime).ToString(),
            version = Version,
        };

        return Ok(status);
    }

    /// <summary>
    /// Gets a placeholder array of recent metrics history.
    /// </summary>
    /// <returns>An array of recent metric snapshots.</returns>
    [HttpGet("metrics/history")]
    public IActionResult GetMetricsHistory()
    {
        LogMetricsHistoryRequested();

        // Placeholder: return an empty array until a metrics history store is implemented.
        return Ok(Array.Empty<object>());
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dashboard status requested")]
    private partial void LogStatusRequested();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Metrics history requested")]
    private partial void LogMetricsHistoryRequested();
}
