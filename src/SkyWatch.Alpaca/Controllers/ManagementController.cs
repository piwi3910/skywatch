using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkyWatch.Alpaca.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Alpaca.Controllers;

[ApiController]
[Route("management")]
public sealed class ManagementController : ControllerBase
{
    private readonly AlpacaSettings settings;

    public ManagementController(IOptions<AlpacaSettings> options)
    {
        settings = options.Value;
    }

    [HttpGet("apiversions")]
    public ActionResult<AlpacaValueResponse<int[]>> GetApiVersions(
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        return Ok(new AlpacaValueResponse<int[]>
        {
            Value = [1],
            ClientTransactionID = clientTransactionId,
            ServerTransactionID = 1,
        });
    }

    [HttpGet("v1/description")]
    public ActionResult<AlpacaValueResponse<object>> GetDescription(
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        return Ok(new AlpacaValueResponse<object>
        {
            Value = new
            {
                ServerName = settings.ServerName,
                Manufacturer = settings.Manufacturer,
                ManufacturerVersion = version,
                Location = string.Empty,
            },
            ClientTransactionID = clientTransactionId,
            ServerTransactionID = 1,
        });
    }

    [HttpGet("v1/configureddevices")]
    public ActionResult<AlpacaValueResponse<object[]>> GetConfiguredDevices(
        [FromQuery(Name = "ClientID")] uint clientId = 0,
        [FromQuery(Name = "ClientTransactionID")] uint clientTransactionId = 0)
    {
        return Ok(new AlpacaValueResponse<object[]>
        {
            Value =
            [
                new
                {
                    DeviceName = settings.DeviceName,
                    DeviceType = "SafetyMonitor",
                    DeviceNumber = 0,
                    UniqueID = settings.UniqueId,
                },
            ],
            ClientTransactionID = clientTransactionId,
            ServerTransactionID = 1,
        });
    }
}
