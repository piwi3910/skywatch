using System.Net;
using System.Text.Json;

namespace SkyWatch.IntegrationTests;

/// <summary>
/// Integration tests for the /health endpoint.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<SkyWatchWebApplicationFactory>
{
    private readonly HttpClient client;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthEndpointTests"/> class.
    /// </summary>
    /// <param name="factory">The web application factory provided by xUnit.</param>
    public HealthEndpointTests(SkyWatchWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpointReturnsOk()
    {
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.TryGetProperty("timestamp", out _));
    }
}
