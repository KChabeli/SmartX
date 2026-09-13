# Smart-X IoT Mesh Gateway — Part 1

ASP.NET Core 10 Minimal API + React dashboard for ingesting, validating and visualising multi-typed ESP32 telemetry (floats, integers, booleans) without boxing.

The console implements the Task 1 research strategy: **semantic colour-coded threshold alerting** combined with **real-time SignalR telemetry streaming**. Sensor cards use a Threshold Halo (not a progress bar): green pulse = nominal, amber = warning, red expanding shockwave = spike, dashed grey orbit = disconnect / missed heartbeat.

## Architecture

- `src/SmartX.Domain` — `TelemetryPacket<T>`, operator-overloaded meter/moisture/actuator types, jagged historical batches, recursive Facility → Zone → Sub-Zone → Node validation, colour palette.
- `src/SmartX.Api` — Minimal APIs, multipart uploads, SignalR hub, seeded mock mesh (Stellenbosch hydroponics, Sandton estate, Cape Flats microgrid).
- `client` — Vite + React + TypeScript dashboard.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 22+
- Optional: Docker Desktop

## Run locally

### 1. Restore and compile

```powershell
cd C:\Users\khumo\Projects\SmartX
dotnet restore SmartX.slnx
dotnet build SmartX.slnx
cd client
npm install
```

### 2. Boot the backend API

```powershell
cd C:\Users\khumo\Projects\SmartX
dotnet run --project src/SmartX.Api/SmartX.Api.csproj --launch-profile http
```

API listens on `http://localhost:5266`.

- Health: `http://localhost:5266/health`
- OpenAPI: `http://localhost:5266/openapi/v1.json`

### 3. Run the client UI

In a second terminal:

```powershell
cd C:\Users\khumo\Projects\SmartX\client
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` and `/hubs` to the API.

### 4. What to click

1. Landing page — three architectural pillars. Only **Sensor Data Ingestion and Telemetry** is enabled.
2. Telemetry console — live halo grid, disconnect watch, spike radar, threshold corridor chart.
3. Sensors — register a MAC / location / category and attach a config/photo/log file.
4. Operators & Tree — `Meter3 = Meter1 + Meter2` via C# operator overloads, plus recursive deployment-tree validation.

## Docker

```powershell
cd C:\Users\khumo\Projects\SmartX
docker compose up --build
```

- UI: `http://localhost:8080`
- API: `http://localhost:5266`

## Advanced C# mapping

| Requirement | Where |
| --- | --- |
| Generics without boxing | `TelemetryPacket<T> where T : struct` plus typed `List<TelemetryPacket<float/int/bool>>` |
| Operator overloading | `SmartMeterReading` `+` `-` `>` `<`, `MoistureReading`, `ActuatorState` `&` `\|` |
| Jagged / multi-dimensional arrays | `HistoricalBatchStore<T>` (`T[][]` windows) and `ZoneHeatGrid` (`AlertSeverity[,]`) |
| Recursion | `DeploymentValidator.Validate` / `FindAncestorChain` |

## GitHub

Repository: https://github.com/KChabeli/SmartX

```powershell
git remote add origin https://github.com/KChabeli/SmartX.git
git push -u origin main
```
