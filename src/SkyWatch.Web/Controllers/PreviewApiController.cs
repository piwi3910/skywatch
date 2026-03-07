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

        byte[] jpegBytes;
        if (frame.BitsPerChannel > 8)
        {
            // Auto-stretch 16-bit to 8-bit for JPEG preview
            using var mat = OpenCvSharp.Cv2.ImDecode(
                frame.ImageBytes,
                OpenCvSharp.ImreadModes.AnyDepth | OpenCvSharp.ImreadModes.AnyColor);
            if (mat.Empty())
            {
                return NoContent();
            }

            using var stretched = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.MinMaxLoc(mat, out double minVal, out double maxVal);
            double scale = maxVal > minVal ? 255.0 / (maxVal - minVal) : 1.0;
            double offset = -minVal * scale;
            mat.ConvertTo(stretched, OpenCvSharp.MatType.CV_8U, scale, offset);

            OpenCvSharp.Cv2.ImEncode(".jpg", stretched, out jpegBytes);
        }
        else
        {
            // 8-bit: decode and re-encode as JPEG for consistent output
            using var mat = OpenCvSharp.Cv2.ImDecode(frame.ImageBytes, OpenCvSharp.ImreadModes.Color);
            if (mat.Empty())
            {
                return File(frame.ImageBytes, "image/jpeg");
            }

            OpenCvSharp.Cv2.ImEncode(".jpg", mat, out jpegBytes);
        }

        return File(jpegBytes, "image/jpeg");
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No frame source registered, returning 204")]
    private partial void LogNoFrameSource();

    [LoggerMessage(Level = LogLevel.Debug, Message = "No frame available from source, returning 204")]
    private partial void LogNoFrameAvailable();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Serving preview frame ({Width}x{Height})")]
    private partial void LogFrameServed(int width, int height);
}
