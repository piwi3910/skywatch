using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyWatch.Alpaca.Models;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Alpaca.Controllers;

/// <summary>
/// ASCOM Alpaca SafetyMonitor API controller.
/// </summary>
[ApiController]
[Route("api/v1/safetymonitor/{deviceNumber:int}")]
public sealed partial class SafetyMonitorController : ControllerBase
{
    private static int serverTransactionCounter;

    private readonly ISafetyStateStore safetyStateStore;
    private readonly AlpacaSettings settings;
    private readonly ILogger<SafetyMonitorController> logger;

    private bool connected;

    public SafetyMonitorController(
        ISafetyStateStore safetyStateStore,
        IOptions<AlpacaSettings> options,
        ILogger<SafetyMonitorController> logger)
    {
        this.safetyStateStore = safetyStateStore;
        settings = options.Value;
        this.logger = logger;
    }

    [HttpGet("issafe")]
    public ActionResult<AlpacaValueResponse<bool>> GetIsSafe(
        int deviceNumber,
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        if (!connected)
        {
            return Ok(CreateNotConnectedResponse<bool>(clientTransactionId));
        }

        LogIsSafeRequested(safetyStateStore.Current.IsSafe);
        return Ok(CreateValueResponse(safetyStateStore.Current.IsSafe, clientTransactionId));
    }

    [HttpGet("connected")]
    public ActionResult<AlpacaValueResponse<bool>> GetConnected(
        int deviceNumber,
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateValueResponse(connected, clientTransactionId));
    }

    [HttpPut("connected")]
    [Consumes("application/x-www-form-urlencoded")]
    public ActionResult<AlpacaResponse> PutConnected(
        int deviceNumber,
        [FromForm(Name = "Connected")] bool connectedValue = false,
        [FromForm(Name = "ClientID")] uint clientId = 0,
        [FromForm(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        connected = connectedValue;
        LogConnectionStateChanged(connectedValue);
        return Ok(CreateResponse(clientTransactionId));
    }

    [HttpGet("connecting")]
    public ActionResult<AlpacaValueResponse<bool>> GetConnecting(
        int deviceNumber,
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateValueResponse(false, clientTransactionId));
    }

    [HttpGet("description")]
    public ActionResult<AlpacaValueResponse<string>> GetDescription(
        int deviceNumber,
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateValueResponse("SkyWatch SafetyMonitor", clientTransactionId));
    }

    [HttpGet("devicestate")]
    public ActionResult<AlpacaValueResponse<object[]>> GetDeviceState(
        int deviceNumber,
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        var current = safetyStateStore.Current;
        var state = new object[]
        {
            new { Name = "IsSafe", Value = current.IsSafe.ToString() },
            new { Name = "Timestamp", Value = current.EvaluatedAt.ToString("O") },
        };

        return Ok(CreateValueResponse(state, clientTransactionId));
    }

    [HttpGet("driverinfo")]
    public ActionResult<AlpacaValueResponse<string>> GetDriverInfo(
        int deviceNumber,
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateValueResponse("SkyWatch Safety Monitor Driver", clientTransactionId));
    }

    [HttpGet("driverversion")]
    public ActionResult<AlpacaValueResponse<string>> GetDriverVersion(
        int deviceNumber,
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        return Ok(CreateValueResponse(version, clientTransactionId));
    }

    [HttpGet("interfaceversion")]
    public ActionResult<AlpacaValueResponse<int>> GetInterfaceVersion(
        int deviceNumber,
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateValueResponse(3, clientTransactionId));
    }

    [HttpGet("name")]
    public ActionResult<AlpacaValueResponse<string>> GetName(
        int deviceNumber,
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateValueResponse(settings.DeviceName, clientTransactionId));
    }

    [HttpGet("supportedactions")]
    public ActionResult<AlpacaValueResponse<string[]>> GetSupportedActions(
        int deviceNumber,
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateValueResponse(Array.Empty<string>(), clientTransactionId));
    }

    [HttpPut("connect")]
    [Consumes("application/x-www-form-urlencoded")]
    public ActionResult<AlpacaResponse> PutConnect(
        int deviceNumber,
        [FromForm(Name = "ClientID")] uint clientId = 0,
        [FromForm(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        connected = true;
        LogConnectionStateChanged(true);
        return Ok(CreateResponse(clientTransactionId));
    }

    [HttpPut("disconnect")]
    [Consumes("application/x-www-form-urlencoded")]
    public ActionResult<AlpacaResponse> PutDisconnect(
        int deviceNumber,
        [FromForm(Name = "ClientID")] uint clientId = 0,
        [FromForm(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        connected = false;
        LogConnectionStateChanged(false);
        return Ok(CreateResponse(clientTransactionId));
    }

    [HttpPut("action")]
    [Consumes("application/x-www-form-urlencoded")]
    public ActionResult<AlpacaValueResponse<string>> PutAction(
        int deviceNumber,
        [FromForm(Name = "Action")] string action = "",
        [FromForm(Name = "Parameters")] string parameters = "",
        [FromForm(Name = "ClientID")] uint clientId = 0,
        [FromForm(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateNotImplementedResponse<string>(clientTransactionId));
    }

    [HttpPut("commandblind")]
    [Consumes("application/x-www-form-urlencoded")]
    public ActionResult<AlpacaResponse> PutCommandBlind(
        int deviceNumber,
        [FromForm(Name = "Command")] string command = "",
        [FromForm(Name = "Raw")] bool raw = false,
        [FromForm(Name = "ClientID")] uint clientId = 0,
        [FromForm(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateNotImplementedResponse(clientTransactionId));
    }

    [HttpPut("commandbool")]
    [Consumes("application/x-www-form-urlencoded")]
    public ActionResult<AlpacaValueResponse<bool>> PutCommandBool(
        int deviceNumber,
        [FromForm(Name = "Command")] string command = "",
        [FromForm(Name = "Raw")] bool raw = false,
        [FromForm(Name = "ClientID")] uint clientId = 0,
        [FromForm(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateNotImplementedResponse<bool>(clientTransactionId));
    }

    [HttpPut("commandstring")]
    [Consumes("application/x-www-form-urlencoded")]
    public ActionResult<AlpacaValueResponse<string>> PutCommandString(
        int deviceNumber,
        [FromForm(Name = "Command")] string command = "",
        [FromForm(Name = "Raw")] bool raw = false,
        [FromForm(Name = "ClientID")] uint clientId = 0,
        [FromForm(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        if (!ValidateDeviceNumber(deviceNumber, clientTransactionId, out var errorResponse))
        {
            return Ok(errorResponse);
        }

        return Ok(CreateNotImplementedResponse<string>(clientTransactionId));
    }

    private static uint NextServerTransactionId()
    {
        return (uint)Interlocked.Increment(ref serverTransactionCounter);
    }

    private static AlpacaResponse CreateResponse(uint clientTransactionId)
    {
        return new AlpacaResponse
        {
            ErrorNumber = AlpacaErrorCode.NoError,
            ErrorMessage = string.Empty,
            ClientTransactionID = clientTransactionId,
            ServerTransactionID = NextServerTransactionId(),
        };
    }

    private static AlpacaValueResponse<T> CreateValueResponse<T>(T value, uint clientTransactionId)
    {
        return new AlpacaValueResponse<T>
        {
            Value = value,
            ErrorNumber = AlpacaErrorCode.NoError,
            ErrorMessage = string.Empty,
            ClientTransactionID = clientTransactionId,
            ServerTransactionID = NextServerTransactionId(),
        };
    }

    private static AlpacaResponse CreateNotImplementedResponse(uint clientTransactionId)
    {
        return new AlpacaResponse
        {
            ErrorNumber = AlpacaErrorCode.NotImplemented,
            ErrorMessage = "This method is not implemented.",
            ClientTransactionID = clientTransactionId,
            ServerTransactionID = NextServerTransactionId(),
        };
    }

    private static AlpacaValueResponse<T> CreateNotImplementedResponse<T>(uint clientTransactionId)
    {
        return new AlpacaValueResponse<T>
        {
            ErrorNumber = AlpacaErrorCode.NotImplemented,
            ErrorMessage = "This method is not implemented.",
            ClientTransactionID = clientTransactionId,
            ServerTransactionID = NextServerTransactionId(),
        };
    }

    private static AlpacaValueResponse<T> CreateNotConnectedResponse<T>(uint clientTransactionId)
    {
        return new AlpacaValueResponse<T>
        {
            ErrorNumber = AlpacaErrorCode.NotConnected,
            ErrorMessage = "Device is not connected. Call Connect first.",
            ClientTransactionID = clientTransactionId,
            ServerTransactionID = NextServerTransactionId(),
        };
    }

    private bool ValidateDeviceNumber(int deviceNumber, uint clientTransactionId, out AlpacaResponse errorResponse)
    {
        if (deviceNumber == 0)
        {
            errorResponse = null!;
            return true;
        }

        LogInvalidDeviceNumber(deviceNumber);
        errorResponse = new AlpacaResponse
        {
            ErrorNumber = AlpacaErrorCode.InvalidValue,
            ErrorMessage = $"Invalid device number: {deviceNumber}. Only device 0 is supported.",
            ClientTransactionID = clientTransactionId,
            ServerTransactionID = NextServerTransactionId(),
        };
        return false;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "IsSafe requested, current value: {IsSafe}")]
    private partial void LogIsSafeRequested(bool isSafe);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connection state changed to {Connected}")]
    private partial void LogConnectionStateChanged(bool connected);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid device number requested: {DeviceNumber}")]
    private partial void LogInvalidDeviceNumber(int deviceNumber);
}
