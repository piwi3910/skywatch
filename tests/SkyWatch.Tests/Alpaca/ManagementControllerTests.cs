using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkyWatch.Alpaca.Controllers;
using SkyWatch.Alpaca.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tests.Alpaca;

public sealed class ManagementControllerTests
{
    private readonly ManagementController controller;
    private readonly AlpacaSettings settings;

    public ManagementControllerTests()
    {
        settings = new AlpacaSettings
        {
            ServerName = "TestServer",
            Manufacturer = "TestManufacturer",
            DeviceName = "TestDevice",
            UniqueId = "test-unique-id",
        };
        controller = new ManagementController(Options.Create(settings));
    }

    [Fact]
    public void ApiVersionsReturnsArrayContaining1()
    {
        var result = controller.GetApiVersions();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<int[]>>(okResult.Value);
        Assert.NotNull(response.Value);
        Assert.Single(response.Value);
        Assert.Equal(1, response.Value[0]);
    }

    [Fact]
    public void DescriptionReturnsServerInfo()
    {
        var result = controller.GetDescription();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<object>>(okResult.Value);
        Assert.NotNull(response.Value);

        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        Assert.Contains("TestServer", json);
        Assert.Contains("TestManufacturer", json);
    }

    [Fact]
    public void ConfiguredDevicesListsSafetyMonitor()
    {
        var result = controller.GetConfiguredDevices();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<object[]>>(okResult.Value);
        Assert.NotNull(response.Value);
        Assert.Single(response.Value);

        var json = System.Text.Json.JsonSerializer.Serialize(response.Value[0]);
        Assert.Contains("SafetyMonitor", json);
        Assert.Contains("TestDevice", json);
        Assert.Contains("test-unique-id", json);
    }

    [Fact]
    public void ClientTransactionIdIsEchoed()
    {
        var result = controller.GetApiVersions(clientTransactionId: 99);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<int[]>>(okResult.Value);
        Assert.Equal(99u, response.ClientTransactionID);
    }
}
