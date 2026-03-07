namespace SkyWatch.Core.Configuration;

public sealed class AlpacaSettings
{
    public int Port { get; set; } = 11111;

    public int DiscoveryPort { get; set; } = 32227;

    public bool EnableDiscovery { get; set; } = true;

    public string ServerName { get; set; } = "SkyWatch";

    public string Manufacturer { get; set; } = "SkyWatch";

    public string DeviceName { get; set; } = "SkyWatch SafetyMonitor";

    public string UniqueId { get; set; } = Guid.NewGuid().ToString();
}
