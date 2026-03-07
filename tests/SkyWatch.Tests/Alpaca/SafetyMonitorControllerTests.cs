using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SkyWatch.Alpaca.Controllers;
using SkyWatch.Alpaca.Models;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tests.Alpaca;

public sealed class SafetyMonitorControllerTests
{
    private readonly Mock<ISafetyStateStore> mockStore;
    private readonly SafetyMonitorController controller;

    public SafetyMonitorControllerTests()
    {
        mockStore = new Mock<ISafetyStateStore>();
        mockStore.Setup(s => s.Current).Returns(CreateSafeEvaluation(true));

        var settings = new AlpacaSettings
        {
            DeviceName = "Test SafetyMonitor",
        };
        var options = Options.Create(settings);
        controller = new SafetyMonitorController(
            mockStore.Object,
            options,
            NullLogger<SafetyMonitorController>.Instance);
    }

    [Fact]
    public void IsSafeWhenConnectedReturnsCorrectValue()
    {
        controller.PutConnect(0);

        var result = controller.GetIsSafe(0);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<bool>>(okResult.Value);
        Assert.True(response.Value);
        Assert.Equal(AlpacaErrorCode.NoError, response.ErrorNumber);
    }

    [Fact]
    public void IsSafeWhenUnsafeReturnsFalse()
    {
        mockStore.Setup(s => s.Current).Returns(CreateSafeEvaluation(false));
        controller.PutConnect(0);

        var result = controller.GetIsSafe(0);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<bool>>(okResult.Value);
        Assert.False(response.Value);
    }

    [Fact]
    public void IsSafeWhenNotConnectedReturnsNotConnectedError()
    {
        var result = controller.GetIsSafe(0);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<bool>>(okResult.Value);
        Assert.Equal(AlpacaErrorCode.NotConnected, response.ErrorNumber);
    }

    [Fact]
    public void ConnectedDefaultsFalse()
    {
        var result = controller.GetConnected(0);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<bool>>(okResult.Value);
        Assert.False(response.Value);
    }

    [Fact]
    public void ConnectSetsConnectedTrue()
    {
        controller.PutConnect(0);

        var result = controller.GetConnected(0);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<bool>>(okResult.Value);
        Assert.True(response.Value);
    }

    [Fact]
    public void DisconnectSetsConnectedFalse()
    {
        controller.PutConnect(0);
        controller.PutDisconnect(0);

        var result = controller.GetConnected(0);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<bool>>(okResult.Value);
        Assert.False(response.Value);
    }

    [Fact]
    public void InvalidDeviceNumberReturnsError()
    {
        var result = controller.GetConnected(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaResponse>(okResult.Value);
        Assert.Equal(AlpacaErrorCode.InvalidValue, response.ErrorNumber);
    }

    [Fact]
    public void ServerTransactionIdIncrements()
    {
        var result1 = controller.GetConnected(0);
        var result2 = controller.GetConnected(0);

        var okResult1 = Assert.IsType<OkObjectResult>(result1.Result);
        var response1 = Assert.IsType<AlpacaValueResponse<bool>>(okResult1.Value);
        var okResult2 = Assert.IsType<OkObjectResult>(result2.Result);
        var response2 = Assert.IsType<AlpacaValueResponse<bool>>(okResult2.Value);

        Assert.True(response2.ServerTransactionID > response1.ServerTransactionID);
    }

    [Fact]
    public void DescriptionReturnsExpectedValue()
    {
        var result = controller.GetDescription(0);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<string>>(okResult.Value);
        Assert.Equal("SkyWatch SafetyMonitor", response.Value);
    }

    [Fact]
    public void NameReturnsSettingsDeviceName()
    {
        var result = controller.GetName(0);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<string>>(okResult.Value);
        Assert.Equal("Test SafetyMonitor", response.Value);
    }

    [Fact]
    public void InterfaceVersionReturns3()
    {
        var result = controller.GetInterfaceVersion(0);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<int>>(okResult.Value);
        Assert.Equal(3, response.Value);
    }

    [Fact]
    public void ClientTransactionIdIsEchoed()
    {
        var result = controller.GetConnected(0, clientTransactionId: 42);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<bool>>(okResult.Value);
        Assert.Equal(42u, response.ClientTransactionID);
    }

    [Fact]
    public void PutConnectedSetsConnectionState()
    {
        controller.PutConnected(0, connectedValue: true);

        var result = controller.GetConnected(0);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<bool>>(okResult.Value);
        Assert.True(response.Value);
    }

    [Fact]
    public void ActionReturnsNotImplemented()
    {
        var result = controller.PutAction(0);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlpacaValueResponse<string>>(okResult.Value);
        Assert.Equal(AlpacaErrorCode.NotImplemented, response.ErrorNumber);
    }

    private static SafetyEvaluation CreateSafeEvaluation(bool isSafe)
    {
        return new SafetyEvaluation
        {
            State = isSafe ? SafetyState.Safe : SafetyState.Unsafe,
            IsSafe = isSafe,
            Reasons = UnsafeReason.None,
            Confidence = 1.0,
            Explanation = isSafe ? "Conditions are safe" : "Conditions are unsafe",
            EvaluatedAt = DateTimeOffset.UtcNow,
        };
    }
}
