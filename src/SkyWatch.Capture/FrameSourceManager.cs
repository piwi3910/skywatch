using Microsoft.Extensions.Logging;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture;

/// <summary>
/// Manages the active <see cref="IFrameSource"/> and supports live source switching
/// without requiring an application restart. Implements <see cref="IFrameSource"/>
/// so existing consumers can use it as a drop-in replacement.
/// </summary>
public sealed partial class FrameSourceManager : IFrameSource
{
    private readonly ILogger<FrameSourceManager> logger;
    private readonly Func<CaptureSettings, IFrameSource> sourceFactory;
    private readonly SemaphoreSlim switchLock = new(1, 1);
    private IFrameSource? currentSource;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameSourceManager"/> class
    /// using the default <see cref="FrameSourceFactory"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="loggerFactory">The logger factory for creating sources.</param>
    public FrameSourceManager(ILogger<FrameSourceManager> logger, ILoggerFactory loggerFactory)
        : this(logger, loggerFactory, settings => FrameSourceFactory.Create(settings, loggerFactory))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameSourceManager"/> class
    /// with a custom factory delegate (primarily for testing).
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="loggerFactory">The logger factory (retained for the public constructor overload).</param>
    /// <param name="sourceFactory">A delegate that creates an <see cref="IFrameSource"/> from settings.</param>
    internal FrameSourceManager(
        ILogger<FrameSourceManager> logger,
        ILoggerFactory loggerFactory,
        Func<CaptureSettings, IFrameSource> sourceFactory)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(sourceFactory);

        this.logger = logger;
        this.sourceFactory = sourceFactory;
    }

    /// <summary>
    /// Creates and starts the initial frame source from the given capture settings.
    /// Catches all errors so the application can continue even if the source fails to start.
    /// </summary>
    /// <param name="settings">The capture settings to use.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync(CaptureSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await switchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IFrameSource newSource;
            try
            {
                newSource = sourceFactory(settings);
            }
            catch (Exception ex)
            {
                LogSourceCreationFailed(ex);
                return;
            }

            try
            {
                await newSource.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogSourceStartFailed(ex);
            }

            currentSource = newSource;
            LogSourceInitialized(settings.SourceType.ToString());
        }
        finally
        {
            switchLock.Release();
        }
    }

    /// <summary>
    /// Switches to a new frame source, stopping and disposing the current one.
    /// Catches all errors so the application can continue.
    /// </summary>
    /// <param name="newSource">The new frame source to switch to.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SwitchSourceAsync(IFrameSource newSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newSource);

        await switchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopAndDisposeCurrentSourceAsync().ConfigureAwait(false);

            try
            {
                await newSource.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogSourceStartFailed(ex);
            }

            currentSource = newSource;
            LogSourceSwitched();
        }
        finally
        {
            switchLock.Release();
        }
    }

    /// <summary>
    /// Creates a new frame source from settings and switches to it.
    /// Catches all errors so the application can continue.
    /// </summary>
    /// <param name="settings">The capture settings to create the new source from.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SwitchSourceAsync(CaptureSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        IFrameSource newSource;
        try
        {
            newSource = sourceFactory(settings);
        }
        catch (Exception ex)
        {
            LogSourceCreationFailed(ex);
            return;
        }

        await SwitchSourceAsync(newSource, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var source = currentSource;
        if (source is null)
        {
            return;
        }

        try
        {
            await source.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogSourceStartFailed(ex);
        }
    }

    /// <inheritdoc/>
    public async Task<FrameData?> GetLatestFrameAsync(CancellationToken cancellationToken)
    {
        var source = currentSource;
        if (source is null)
        {
            return null;
        }

        try
        {
            return await source.GetLatestFrameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFrameRetrievalFailed(ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var source = currentSource;
        if (source is null)
        {
            return;
        }

        try
        {
            await source.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogSourceStopFailed(ex);
        }
    }

    /// <inheritdoc/>
    public CaptureStatus GetStatus()
    {
        var source = currentSource;
        if (source is null)
        {
            return new CaptureStatus
            {
                IsConnected = false,
                SourceType = FrameSourceType.Replay,
                SourceDescription = "No source configured",
            };
        }

        return source.GetStatus();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await StopAndDisposeCurrentSourceAsync().ConfigureAwait(false);
        switchLock.Dispose();
    }

    private async Task StopAndDisposeCurrentSourceAsync()
    {
        var source = currentSource;
        if (source is null)
        {
            return;
        }

        currentSource = null;

        try
        {
            await source.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogSourceStopFailed(ex);
        }

        try
        {
            await source.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogSourceDisposeFailed(ex);
        }
    }

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Frame source initialized: {SourceType}")]
    private partial void LogSourceInitialized(string sourceType);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Frame source switched successfully")]
    private partial void LogSourceSwitched();

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to create frame source")]
    private partial void LogSourceCreationFailed(Exception exception);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to start frame source")]
    private partial void LogSourceStartFailed(Exception exception);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to stop frame source")]
    private partial void LogSourceStopFailed(Exception exception);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to dispose frame source")]
    private partial void LogSourceDisposeFailed(Exception exception);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to retrieve frame from source")]
    private partial void LogFrameRetrievalFailed(Exception exception);
}
