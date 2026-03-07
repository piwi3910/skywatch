using System.Net;
using System.Text.Json;

namespace SkyWatch.IntegrationTests;

/// <summary>
/// Integration tests for the ASCOM Alpaca API endpoints.
/// </summary>
public sealed class AlpacaEndpointTests : IDisposable
{
    private readonly SkyWatchWebApplicationFactory factory;
    private readonly HttpClient client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpacaEndpointTests"/> class.
    /// </summary>
    public AlpacaEndpointTests()
    {
        factory = new SkyWatchWebApplicationFactory
        {
            ServerName = "SkyWatch Test",
            DeviceName = "Test SafetyMonitor",
        };
        client = factory.CreateClient();
    }

    [Fact]
    public async Task ManagementApiVersionsReturnsOne()
    {
        var response = await client.GetAsync("/management/apiversions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var value = doc.RootElement.GetProperty("value");
        Assert.Equal(JsonValueKind.Array, value.ValueKind);

        var items = value.EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal(1, items[0].GetInt32());
    }

    [Fact]
    public async Task ManagementDescriptionReturnsServerInfo()
    {
        var response = await client.GetAsync("/management/v1/description");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var value = doc.RootElement.GetProperty("value");
        Assert.Equal("SkyWatch Test", value.GetProperty("serverName").GetString());
    }

    [Fact]
    public async Task ManagementConfiguredDevicesListsSafetyMonitor()
    {
        var response = await client.GetAsync("/management/v1/configureddevices");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var value = doc.RootElement.GetProperty("value");
        Assert.Equal(JsonValueKind.Array, value.ValueKind);

        var devices = value.EnumerateArray().ToList();
        Assert.Single(devices);
        Assert.Equal("Test SafetyMonitor", devices[0].GetProperty("deviceName").GetString());
        Assert.Equal("SafetyMonitor", devices[0].GetProperty("deviceType").GetString());
        Assert.Equal(0, devices[0].GetProperty("deviceNumber").GetInt32());
    }

    [Fact]
    public async Task SafetyMonitorConnectReturnsSuccess()
    {
        var connectContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Connected"] = "true",
        });
        var connectResponse = await client.PutAsync("/api/v1/safetymonitor/0/connected", connectContent);
        Assert.Equal(HttpStatusCode.OK, connectResponse.StatusCode);

        var content = await connectResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.Equal(0, doc.RootElement.GetProperty("errorNumber").GetInt32());
        Assert.True(doc.RootElement.GetProperty("serverTransactionID").GetUInt32() > 0);
    }

    [Fact]
    public async Task SafetyMonitorNotConnectedReturnsError()
    {
        // Use a fresh client to ensure a new controller instance without prior connection
        using var freshFactory = new SkyWatchWebApplicationFactory
        {
            ServerName = "SkyWatch Test",
            DeviceName = "Test SafetyMonitor",
        };
        using var freshClient = freshFactory.CreateClient();

        var response = await freshClient.GetAsync("/api/v1/safetymonitor/0/issafe");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.Equal(0x407, doc.RootElement.GetProperty("errorNumber").GetInt32());
    }

    [Fact]
    public async Task SafetyMonitorInterfaceVersionReturnsThree()
    {
        var response = await client.GetAsync("/api/v1/safetymonitor/0/interfaceversion");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.Equal(0, doc.RootElement.GetProperty("errorNumber").GetInt32());
        Assert.Equal(3, doc.RootElement.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task SafetyMonitorNameReturnsDeviceName()
    {
        var response = await client.GetAsync("/api/v1/safetymonitor/0/name");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.Equal(0, doc.RootElement.GetProperty("errorNumber").GetInt32());
        Assert.Equal("Test SafetyMonitor", doc.RootElement.GetProperty("value").GetString());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }
}
