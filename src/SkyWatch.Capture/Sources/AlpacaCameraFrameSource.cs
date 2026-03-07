using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture.Sources;

/// <summary>
/// A frame source that captures images from an ASCOM Alpaca camera via HTTP REST API.
/// </summary>
public sealed partial class AlpacaCameraFrameSource : IFrameSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CaptureSettings settings;
    private readonly HttpClient httpClient;
    private readonly ILogger<AlpacaCameraFrameSource> logger;

    private bool isStarted;
    private int framesCaptured;
    private DateTimeOffset? lastFrameTime;
    private string? errorMessage;
    private double currentExposure;
    private int clientTransactionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpacaCameraFrameSource"/> class.
    /// </summary>
    /// <param name="settings">The capture settings.</param>
    /// <param name="httpClient">The HTTP client for Alpaca REST calls.</param>
    /// <param name="logger">The logger instance.</param>
    public AlpacaCameraFrameSource(CaptureSettings settings, HttpClient httpClient, ILogger<AlpacaCameraFrameSource> logger)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        currentExposure = settings.ExposureDuration;
    }

    private string BaseUrl => $"http://{settings.AlpacaHost}:{settings.AlpacaPort}/api/v1/camera/{settings.AlpacaDeviceNumber}";

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogConnecting(settings.AlpacaHost, settings.AlpacaPort, settings.AlpacaDeviceNumber);

        var content = CreateFormContent(("Connected", "true"));
        var response = await httpClient.PutAsync($"{BaseUrl}/connected", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ValidateAlpacaResponse(body);

        isStarted = true;
        errorMessage = null;
        LogConnected(settings.AlpacaHost, settings.AlpacaPort, settings.AlpacaDeviceNumber);
    }

    /// <inheritdoc/>
    public async Task<FrameData?> GetLatestFrameAsync(CancellationToken cancellationToken)
    {
        if (!isStarted)
        {
            return null;
        }

        try
        {
            // Start the exposure
            var exposureContent = CreateFormContent(
                ("Duration", currentExposure.ToString("G", System.Globalization.CultureInfo.InvariantCulture)),
                ("Light", "true"));
            var startResponse = await httpClient.PutAsync($"{BaseUrl}/startexposure", exposureContent, cancellationToken);
            startResponse.EnsureSuccessStatusCode();

            var startBody = await startResponse.Content.ReadAsStringAsync(cancellationToken);
            ValidateAlpacaResponse(startBody);

            LogExposureStarted(currentExposure);

            // Poll for image ready
            while (!cancellationToken.IsCancellationRequested)
            {
                var txId = GetNextTransactionId();
                var readyResponse = await httpClient.GetAsync(
                    $"{BaseUrl}/imageready?ClientID=1&ClientTransactionID={txId}",
                    cancellationToken);
                readyResponse.EnsureSuccessStatusCode();

                var readyBody = await readyResponse.Content.ReadAsStringAsync(cancellationToken);
                var readyResult = JsonSerializer.Deserialize<AlpacaResponse<bool>>(readyBody, JsonOptions);

                if (readyResult is not null && readyResult.ErrorNumber != 0)
                {
                    throw new InvalidOperationException($"Alpaca error {readyResult.ErrorNumber}: {readyResult.ErrorMessage}");
                }

                if (readyResult?.Value == true)
                {
                    break;
                }

                await Task.Delay(100, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Download the image bytes
            var imgTxId = GetNextTransactionId();
            var imageResponse = await httpClient.GetAsync(
                $"{BaseUrl}/imagebytes?ClientID=1&ClientTransactionID={imgTxId}",
                cancellationToken);
            imageResponse.EnsureSuccessStatusCode();

            var imageData = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            var frameData = ParseImageBytes(imageData);

            framesCaptured++;
            lastFrameTime = DateTimeOffset.UtcNow;
            errorMessage = null;

            if (settings.AutoExposure)
            {
                AdjustExposure(frameData);
            }

            return frameData;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCaptureError(ex);
            errorMessage = ex.Message;
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!isStarted)
        {
            return;
        }

        try
        {
            var content = CreateFormContent(("Connected", "false"));
            var response = await httpClient.PutAsync($"{BaseUrl}/connected", content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            LogDisconnectError(ex);
        }

        isStarted = false;
        LogDisconnected(settings.AlpacaHost, settings.AlpacaPort, settings.AlpacaDeviceNumber);
    }

    /// <inheritdoc/>
    public CaptureStatus GetStatus()
    {
        return new CaptureStatus
        {
            IsConnected = isStarted,
            SourceType = FrameSourceType.AlpacaCamera,
            SourceDescription = $"Alpaca Camera: {settings.AlpacaHost}:{settings.AlpacaPort} Device {settings.AlpacaDeviceNumber}",
            LastFrameAt = lastFrameTime,
            FramesCaptured = framesCaptured,
            ErrorMessage = errorMessage,
            CurrentExposureDuration = currentExposure,
            AutoExposureEnabled = settings.AutoExposure,
            AutoExposureTarget = settings.AutoExposure ? settings.AutoExposureTarget : null,
        };
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (isStarted)
        {
            await StopAsync(CancellationToken.None);
        }
    }

    private static FrameData ParseImageBytes(byte[] data)
    {
        const int minimumHeaderSize = 44;

        if (data.Length < minimumHeaderSize)
        {
            throw new InvalidOperationException($"Alpaca ImageBytes data too small: {data.Length} bytes (minimum header is {minimumHeaderSize} bytes).");
        }

        var dataStart = BitConverter.ToInt32(data, 16);
        var transmissionElementType = BitConverter.ToInt32(data, 24);
        var rank = BitConverter.ToInt32(data, 28);
        var width = BitConverter.ToInt32(data, 32);
        var height = BitConverter.ToInt32(data, 36);
        var colorPlanes = rank == 3 ? BitConverter.ToInt32(data, 40) : 1;

        int bitsPerChannel = transmissionElementType switch
        {
            1 => 8,   // Byte
            2 => 16,  // Int16
            6 => 16,  // UInt16
            _ => throw new InvalidOperationException($"Unsupported Alpaca transmission element type: {transmissionElementType}"),
        };

        string pixelFormat = (rank, bitsPerChannel) switch
        {
            (2, 8) => "Mono8",
            (2, 16) => "Mono16",
            (3, 8) => "RGB24",
            (3, 16) => "RGB48",
            _ => $"Unknown-Rank{rank}-{bitsPerChannel}bpc",
        };

        int channels = rank == 3 ? colorPlanes : 1;
        int bytesPerPixel = channels * (bitsPerChannel / 8);
        int expectedDataSize = width * height * bytesPerPixel;
        int availableData = data.Length - dataStart;

        if (availableData < expectedDataSize)
        {
            throw new InvalidOperationException(
                $"Alpaca ImageBytes data incomplete: expected {expectedDataSize} bytes of pixel data but only {availableData} available.");
        }

        var pixelData = new byte[expectedDataSize];
        Buffer.BlockCopy(data, dataStart, pixelData, 0, expectedDataSize);

        return new FrameData
        {
            ImageBytes = pixelData,
            Width = width,
            Height = height,
            Channels = channels,
            BitsPerChannel = bitsPerChannel,
            PixelFormat = pixelFormat,
            CapturedAt = DateTimeOffset.UtcNow,
            SourceType = FrameSourceType.AlpacaCamera,
        };
    }

    private static void ValidateAlpacaResponse(string json)
    {
        var response = JsonSerializer.Deserialize<AlpacaResponse<object>>(json, JsonOptions);
        if (response is not null && response.ErrorNumber != 0)
        {
            throw new InvalidOperationException($"Alpaca error {response.ErrorNumber}: {response.ErrorMessage}");
        }
    }

    private void AdjustExposure(FrameData frame)
    {
        // Calculate mean brightness, scaling to 0-255 range
        double sum = 0;
        int bytesPerSample = frame.BitsPerChannel / 8;
        int sampleCount = frame.ImageBytes.Length / bytesPerSample;

        if (sampleCount == 0)
        {
            return;
        }

        double maxValue = frame.BitsPerChannel == 16 ? 65535.0 : 255.0;

        for (int i = 0; i < frame.ImageBytes.Length; i += bytesPerSample)
        {
            double sampleValue = bytesPerSample == 2
                ? BitConverter.ToUInt16(frame.ImageBytes, i)
                : frame.ImageBytes[i];
            sum += sampleValue;
        }

        double meanRaw = sum / sampleCount;
        double meanBrightness = meanRaw / maxValue * 255.0;

        // Adjust exposure with 0.5 damping factor
        const double dampingFactor = 0.5;
        double ratio = settings.AutoExposureTarget / Math.Max(meanBrightness, 1.0);
        double adjustment = 1.0 + ((ratio - 1.0) * dampingFactor);
        double newExposure = currentExposure * adjustment;

        // Clamp to configured min/max
        newExposure = Math.Clamp(newExposure, settings.ExposureMin, settings.ExposureMax);

        LogAutoExposureAdjusted(currentExposure, newExposure, meanBrightness);
        currentExposure = newExposure;
    }

    private FormUrlEncodedContent CreateFormContent(params (string Key, string Value)[] parameters)
    {
        var txId = GetNextTransactionId();
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("ClientID", "1"),
            new("ClientTransactionID", txId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };

        foreach (var (key, value) in parameters)
        {
            pairs.Add(new KeyValuePair<string, string>(key, value));
        }

        return new FormUrlEncodedContent(pairs);
    }

    private int GetNextTransactionId()
    {
        return Interlocked.Increment(ref clientTransactionId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to Alpaca camera at {Host}:{Port} device {DeviceNumber}")]
    private partial void LogConnecting(string host, int port, int deviceNumber);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to Alpaca camera at {Host}:{Port} device {DeviceNumber}")]
    private partial void LogConnected(string host, int port, int deviceNumber);

    [LoggerMessage(Level = LogLevel.Information, Message = "Disconnected from Alpaca camera at {Host}:{Port} device {DeviceNumber}")]
    private partial void LogDisconnected(string host, int port, int deviceNumber);

    [LoggerMessage(Level = LogLevel.Information, Message = "Alpaca exposure started: {Duration}s")]
    private partial void LogExposureStarted(double duration);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error capturing frame from Alpaca camera")]
    private partial void LogCaptureError(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error disconnecting from Alpaca camera")]
    private partial void LogDisconnectError(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Auto-exposure adjusted from {OldExposure:F3}s to {NewExposure:F3}s (mean brightness: {MeanBrightness:F1})")]
    private partial void LogAutoExposureAdjusted(double oldExposure, double newExposure, double meanBrightness);

    private sealed class AlpacaResponse<T>
    {
        [JsonPropertyName("Value")]
        public T? Value { get; set; }

        [JsonPropertyName("ErrorNumber")]
        public int ErrorNumber { get; set; }

        [JsonPropertyName("ErrorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
