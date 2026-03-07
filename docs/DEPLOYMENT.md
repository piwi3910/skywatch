# SkyWatch Deployment Guide

## Prerequisites

- **.NET 8 SDK** (for building) or **.NET 8 Runtime** (for running published binaries)
- **OpenCvSharp4 native runtime** -- included automatically via NuGet for Windows and Linux x64. ARM64 (Raspberry Pi) requires additional setup (see below).

## Building from Source

```bash
git clone https://github.com/piwi3910/skywatch.git
cd skywatch
dotnet build SkyWatch.sln
dotnet test SkyWatch.sln   # verify: 119 tests (111 unit + 8 integration)
```

## Publishing

Publish a self-contained binary so the target machine does not need the .NET runtime installed:

```bash
# Windows x64
dotnet publish src/SkyWatch.Host -c Release -r win-x64 --self-contained

# Linux x64
dotnet publish src/SkyWatch.Host -c Release -r linux-x64 --self-contained

# Linux ARM64 (Raspberry Pi 4/5)
dotnet publish src/SkyWatch.Host -c Release -r linux-arm64 --self-contained
```

The output is placed in `src/SkyWatch.Host/bin/Release/net8.0/<rid>/publish/`.

To produce a smaller binary, add `-p:PublishTrimmed=true` (may require testing; OpenCvSharp interop can be sensitive to trimming).

## Windows Deployment

### Running Directly

```powershell
cd publish
.\SkyWatch.Host.exe
```

The web UI is available at `http://localhost:5000` and the Alpaca API at `http://localhost:5000/api/v1/safetymonitor/0/issafe`. Alpaca discovery runs on UDP port 32227.

### Running as a Windows Service

Use [NSSM](https://nssm.cc/) or the built-in `sc.exe` to register the executable as a Windows service:

```powershell
# Using NSSM
nssm install SkyWatch "C:\SkyWatch\SkyWatch.Host.exe"
nssm set SkyWatch AppDirectory "C:\SkyWatch"
nssm start SkyWatch

# Or using sc.exe (requires the app to support Windows Service lifetime)
sc.exe create SkyWatch binPath="C:\SkyWatch\SkyWatch.Host.exe"
sc.exe start SkyWatch
```

### Firewall

Allow inbound connections on:
- TCP port 5000 (web UI)
- UDP port 32227 (Alpaca discovery)

```powershell
netsh advfirewall firewall add rule name="SkyWatch Web" dir=in action=allow protocol=tcp localport=5000
netsh advfirewall firewall add rule name="SkyWatch Alpaca Discovery" dir=in action=allow protocol=udp localport=32227
```

## Linux Deployment

### Running Directly

```bash
chmod +x SkyWatch.Host
./SkyWatch.Host
```

### Running as a systemd Service

Create `/etc/systemd/system/skywatch.service`:

```ini
[Unit]
Description=SkyWatch Sky Safety Monitor
After=network.target

[Service]
Type=exec
WorkingDirectory=/opt/skywatch
ExecStart=/opt/skywatch/SkyWatch.Host
Restart=always
RestartSec=10
User=skywatch
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Enable and start:

```bash
sudo useradd -r -s /usr/sbin/nologin skywatch
sudo mkdir -p /opt/skywatch
sudo cp -r publish/* /opt/skywatch/
sudo chown -R skywatch:skywatch /opt/skywatch
sudo systemctl daemon-reload
sudo systemctl enable skywatch
sudo systemctl start skywatch
sudo systemctl status skywatch
```

### Firewall (ufw)

```bash
sudo ufw allow 5000/tcp    # Web UI
sudo ufw allow 32227/udp   # Alpaca discovery
```

## Raspberry Pi (ARM64)

SkyWatch runs on Raspberry Pi 4 and 5 with a 64-bit OS (e.g., Raspberry Pi OS Lite 64-bit).

### OpenCvSharp Native Runtime

The OpenCvSharp4 NuGet package includes native libraries for `linux-x64` but not `linux-arm64`. On ARM64 you must install the OpenCV native library separately:

```bash
sudo apt update
sudo apt install -y libopencv-dev
```

Then set the library path so the .NET runtime can find it:

```bash
export LD_LIBRARY_PATH=/usr/lib/aarch64-linux-gnu:$LD_LIBRARY_PATH
```

Add this to the systemd unit file's `Environment` directive or to `/etc/environment` for persistence.

### Performance Notes

- Frame analysis on a Pi 4 may take 200--500ms per frame depending on resolution. Adjust `FrameIntervalSeconds` accordingly (5--10 seconds is reasonable).
- Reduce `TargetWidth` and `TargetHeight` (e.g., 640x480) to lower CPU usage.
- Use a self-contained publish to avoid installing the full .NET SDK on the Pi.

### Headless Operation

SkyWatch runs headless by default. No display, desktop environment, or GUI toolkit is required. The web UI is accessed remotely via a browser on another machine.

## Replay Tool

The replay tool runs analysis against a directory of saved images, useful for threshold tuning and regression testing.

```bash
# Build
dotnet build tools/SkyWatch.Tools.Replay

# Run
dotnet run --project tools/SkyWatch.Tools.Replay -- \
  --source /path/to/images \
  --profile myprofile \
  --output report.json
```

Options:
- `--source <dir>` -- directory containing images to replay (required)
- `--profile <name>` -- profile name to load from the profiles directory (optional; uses defaults if omitted)
- `--output <path>` -- write a JSON report to this path (optional; prints summary to stdout if omitted)

## Connecting NINA to SkyWatch

1. Start SkyWatch on the observatory machine.
2. In NINA, go to **Equipment > Safety Monitor**.
3. If Alpaca discovery is enabled (default), SkyWatch should appear automatically in the device list as "SkyWatch SafetyMonitor".
4. If discovery does not work, add a manual Alpaca device:
   - Host: the IP address of the SkyWatch machine
   - Port: 5000 (or whatever `Urls` is configured to in `appsettings.json`)
   - Device type: SafetyMonitor
   - Device number: 0
5. Connect the device. NINA will poll `GET /api/v1/safetymonitor/0/issafe` to check safety status.

## Configuration File Locations

| File | Purpose |
|------|---------|
| `appsettings.json` | Application-level settings (next to the executable). |
| `profiles/<name>.json` | Per-profile settings (thresholds, capture, timing, masks). |
| `logs/skywatch-*.log` | Rolling daily log files (retained for 30 days). |
| `debug-frames/` | Saved frames on state changes (when enabled). |

See [docs/CONFIGURATION.md](CONFIGURATION.md) for a full reference of all settings.
