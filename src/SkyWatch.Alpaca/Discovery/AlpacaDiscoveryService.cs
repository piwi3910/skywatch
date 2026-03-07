using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Alpaca.Discovery;

/// <summary>
/// Background service that responds to ASCOM Alpaca UDP discovery requests on port 32227.
/// </summary>
public sealed partial class AlpacaDiscoveryService : BackgroundService
{
    private readonly AlpacaSettings settings;
    private readonly ILogger<AlpacaDiscoveryService> logger;

    public AlpacaDiscoveryService(
        IOptions<AlpacaSettings> options,
        ILogger<AlpacaDiscoveryService> logger)
    {
        settings = options.Value;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.EnableDiscovery)
        {
            LogDiscoveryDisabled();
            return;
        }

        LogDiscoveryStarting(settings.DiscoveryPort);

        using var udpClient = new UdpClient();
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, settings.DiscoveryPort));

        var responseJson = JsonSerializer.Serialize(new { AlpacaPort = settings.Port });
        var responseBytes = Encoding.UTF8.GetBytes(responseJson);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(stoppingToken).ConfigureAwait(false);
                var message = Encoding.UTF8.GetString(result.Buffer);

                if (message.StartsWith("alpacadiscovery1", StringComparison.OrdinalIgnoreCase))
                {
                    LogDiscoveryRequestReceived(result.RemoteEndPoint);
                    await udpClient.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogDiscoveryError(ex);
            }
        }

        LogDiscoveryStopped();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Alpaca discovery service starting on UDP port {Port}")]
    private partial void LogDiscoveryStarting(int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Alpaca discovery service is disabled")]
    private partial void LogDiscoveryDisabled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Alpaca discovery request received from {RemoteEndPoint}")]
    private partial void LogDiscoveryRequestReceived(IPEndPoint remoteEndPoint);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in Alpaca discovery service")]
    private partial void LogDiscoveryError(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Alpaca discovery service stopped")]
    private partial void LogDiscoveryStopped();
}
