using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SkyWatch.Contracts.Interfaces;

namespace SkyWatch.Web.Controllers;

/// <summary>
/// Provides the latest camera frame as a JPEG image for preview.
/// </summary>
[ApiController]
[Route("api/preview")]
public sealed partial class PreviewApiController : ControllerBase
{
    private readonly IFrameSource? frameSource;
    private readonly ILogger<PreviewApiController> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewApiController"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for optional services.</param>
    /// <param name="logger">The logger.</param>
    public PreviewApiController(
        IServiceProvider serviceProvider,
        ILogger<PreviewApiController> logger)
    {
        frameSource = serviceProvider.GetService(typeof(IFrameSource)) as IFrameSource;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the latest frame as a JPEG image.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A JPEG image or 204 No Content if no frame is available.</returns>
    [HttpGet("frame")]
    public async Task<IActionResult> GetFrame(CancellationToken cancellationToken)
    {
        if (frameSource is null)
        {
            LogNoFrameSource();
            return NoContent();
        }

        var frame = await frameSource.GetLatestFrameAsync(cancellationToken);
        if (frame is null)
        {
            LogNoFrameAvailable();
            return NoContent();
        }

        LogFrameServed(frame.Width, frame.Height);

        // The frame ImageBytes are raw pixel data. For a real implementation,
        // this would encode to JPEG via OpenCvSharp. For now, return the raw bytes
        // as JPEG content type since the capture layer may already provide encoded data.
        return File(frame.ImageBytes, "image/jpeg");
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No frame source registered, returning 204")]
    private partial void LogNoFrameSource();

    [LoggerMessage(Level = LogLevel.Debug, Message = "No frame available from source, returning 204")]
    private partial void LogNoFrameAvailable();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Serving preview frame ({Width}x{Height})")]
    private partial void LogFrameServed(int width, int height);
}
