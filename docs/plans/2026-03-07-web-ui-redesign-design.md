# SkyWatch Web UI Redesign — Design Document

## Overview

Replace the current vanilla HTML/CSS/JS dashboard with a React-based SPA. All configuration (sources, thresholds, timing, profiles) must be manageable through the UI — no manual JSON editing. Real-time updates via WebSocket. Live source switching without restart.

## Architectural Rule

The ASP.NET Core API is the single source of truth. The React UI is a pure presentation layer with zero business logic. Every operation available in the UI must be available through the REST/WebSocket API, so that a future N.I.N.A. plugin (or any other client) can do everything the web UI can do.

## Technology Choices

| Layer | Choice |
|-------|--------|
| Frontend framework | React 18 + TypeScript |
| Build tool | Vite |
| Component library | MUI (Material UI) with dark theme |
| Real-time | WebSocket (native, via `/ws/live`) |
| CRUD | REST API (`/api/*`) |
| Dev hosting | Vite dev server (port 5173) → proxy to ASP.NET Core (port 5050) |
| Prod hosting | React builds to static files, served by ASP.NET Core |

## Project Structure

```
src/SkyWatch.Web/
  ClientApp/                  ← React app
    src/
      components/             ← reusable UI components
      pages/                  ← page-level components
        Dashboard.tsx
        Sources.tsx
        Thresholds.tsx
        Timing.tsx
        Profiles.tsx
        System.tsx
      hooks/
        useWebSocket.ts       ← WebSocket connection + auto-reconnect
      services/
        api.ts                ← REST API client
        wsClient.ts           ← WebSocket message types + handling
      theme/
        darkTheme.ts          ← MUI dark theme config
      App.tsx                 ← router + layout + sidebar
      main.tsx                ← entry point
    vite.config.ts
    package.json
    tsconfig.json
  Controllers/                ← existing + extended API controllers
  Hubs/                       ← WebSocket endpoint
  wwwroot/                    ← replaced by ClientApp build output
```

## Pages

### 1. Dashboard
- Safety status banner (Safe/Unsafe/Unknown with color)
- Live camera preview (JPEG frames via WebSocket binary messages)
- Current metrics: brightness, star count, cloudiness, entropy, edge density, obstruction
- Safety details: state, reasons, explanation, last state change time
- All data pushed via WebSocket — no polling

### 2. Sources
- Select capture source type: USB Camera, RTSP, Replay
- Source-specific settings:
  - USB: device index, resolution (width × height)
  - RTSP: URL, connection timeout
  - Replay: directory path, frame interval
- Common settings: frame interval, flip horizontal/vertical, rotation
- "Connect" button — live switches source without restart
- Connection status shown immediately via WebSocket push

### 3. Thresholds
- All threshold values editable with sliders + numeric inputs
- Fields: max/min brightness, min star count, max cloudiness, min/max entropy, min/max edge density, obstruction threshold
- Visual indicators showing current metric value relative to threshold
- Save button — validates and applies immediately

### 4. Timing
- Unsafe transition time (seconds)
- Safe transition time (seconds)
- Debounce time (seconds)
- Capture interval (seconds)
- Save button — validates and applies

### 5. Profiles
- List all profiles
- Create new profile (with name + description)
- Delete profile
- Switch active profile (applies all settings from that profile)
- Import/export profile as JSON file

### 6. System
- Manual override toggle: Automatic / Force Safe / Force Unsafe
- Alpaca settings: port, discovery port, enable discovery, server name
- Connection status for all subsystems
- App version and uptime
- Log viewer (recent log entries)

## Navigation

Sidebar navigation, collapsible on small screens. MUI Drawer component.

## Real-time Communication

### WebSocket Endpoint: `/ws/live`

Server pushes on every analysis cycle (~3 seconds):

**Status message (JSON text frame):**
```json
{
  "type": "status",
  "data": {
    "safetyState": "Safe",
    "isSafe": true,
    "reasons": "None",
    "confidence": 0.95,
    "explanation": "All metrics within safe thresholds",
    "metrics": {
      "brightness": 42.3,
      "starCount": 15,
      "cloudiness": 0.12,
      "entropy": 5.8,
      "edgeDensity": 0.08,
      "obstructionScore": 0.02
    },
    "capture": {
      "isConnected": true,
      "sourceType": "UsbCamera",
      "framesCaptured": 127,
      "errorMessage": null
    }
  }
}
```

**Preview frame (binary frame):** JPEG bytes sent as WebSocket binary message.

### Client Hook: `useWebSocket`
- Manages connection lifecycle with auto-reconnect
- Exposes status data via React context
- Components subscribe to the data they need

## Live Source Switching

### Backend: `FrameSourceManager`

New service that wraps `IFrameSource`:

- Holds the current `IFrameSource` instance
- `SwitchSourceAsync(CaptureSettings)` — stops current source, creates new one via `FrameSourceFactory`, starts it
- `SkyWatchWorker` and preview endpoints consume `FrameSourceManager` instead of `IFrameSource` directly
- Thread-safe via locking

### API Endpoint

`POST /api/config/capture` — accepts `CaptureSettings`, validates, calls `FrameSourceManager.SwitchSourceAsync`, saves to profile.

### Error Handling

- **On startup:** If configured source fails, app starts normally with source in error state. Dashboard shows the error. Worker retries per `reconnectIntervalSeconds`.
- **On live switch:** If new source fails to connect, error is shown via WebSocket. Safety state = `CameraError` (fail-safe = unsafe).
- **No source configured:** App runs, no frames. Safety state = `Unknown`.
- The `FrameSourceManager` never throws — catches errors, logs them, updates capture status with error message.

## Production Build

- `SkyWatch.Web.csproj` MSBuild target runs `npm run build` in `ClientApp/` during `dotnet publish`
- Vite build output goes to `wwwroot/`
- ASP.NET Core serves static files from `wwwroot/`
- In dev, `vite.config.ts` proxies `/api/*` and `/ws/*` to `http://localhost:5050`
- Single `dotnet publish` = self-contained artifact with React embedded. No Node.js on target.

## API Completeness (N.I.N.A. Plugin Compatibility)

Every action the web UI can perform must be available as a documented API endpoint:

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/dashboard/status` | GET | Current safety state + metrics |
| `/api/preview/frame` | GET | Latest JPEG frame |
| `/api/config/profile` | GET | Get active profile |
| `/api/config/profile` | PUT | Update active profile |
| `/api/config/profiles` | GET | List all profiles |
| `/api/config/profiles/{name}/activate` | POST | Switch active profile |
| `/api/config/capture` | POST | Change capture source (live) |
| `/api/config/capture` | GET | Get current capture settings |
| `/api/config/thresholds` | GET | Get threshold settings |
| `/api/config/thresholds` | PUT | Update thresholds |
| `/api/config/timing` | GET | Get timing settings |
| `/api/config/timing` | PUT | Update timing settings |
| `/api/config/override` | GET | Get manual override mode |
| `/api/config/override` | PUT | Set manual override mode |
| `/api/config/masks` | GET | Get masks |
| `/api/config/masks` | PUT | Update masks |
| `/ws/live` | WebSocket | Real-time status + preview stream |

A future N.I.N.A. plugin calls these same endpoints — no web UI required.
