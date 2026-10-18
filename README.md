# Smart-X IoT Mesh Gateway — Parts 1 & 2

ASP.NET Core 10 Minimal API + React dashboard for a simulated South African ESP32 mesh (Stellenbosch hydroponics, Sandton estate, Cape Flats microgrid).

- **Part 1** — colour-coded ingest (Threshold Halo Grid, SignalR, `TelemetryPacket<T>`).
- **Part 2** — real-time processing engine: FIFO/priority queues, undo stack, O(1) device dictionary, sorted timeline, unique fault set, and an association-rule insight engine.

## Architecture

- `src/SmartX.Domain` — packets, operators, deployment recursion, queues/commands/faults.
- `src/SmartX.Api` — Minimal APIs, SignalR, mesh simulator that **posts through the same ingest path a device would use**.
- `client` — Vite + React. Live state lives in `GatewayProvider` so Telemetry ↔ Command stream navigation does not drop SignalR or crash the API.

## Prerequisites

- .NET 10 SDK
- Node.js 22+
- Optional: Docker Desktop

## Run locally

```powershell
cd C:\Users\khumo\Projects\SmartX
dotnet restore SmartX.slnx
dotnet build SmartX.slnx
dotnet run --project src/SmartX.Api/SmartX.Api.csproj --launch-profile http
```

Second terminal:

```powershell
cd C:\Users\khumo\Projects\SmartX\client
npm install
npm run dev
```

- API: `http://localhost:5266`
- UI: `http://localhost:5173`
- Health: `http://localhost:5266/health`

Leave the API console open. It is supposed to sit there listening.

## Sign in (MAC presence only)

Open `http://localhost:5173`. You are gated on `/login`.

- Empty MAC → rejected (`MAC address is required`).
- **No format check** — colons, hex length and case are not validated.
- The value is looked up in the live `Dictionary<MAC, device>`. A registered example: `AA:7C:3F:10:00:01`.

```powershell
curl.exe -X POST http://localhost:5266/api/auth/login -H "Content-Type: application/json" --data-binary "{\"macAddress\":\"AA:7C:3F:10:00:01\"}"
```

Command overrides require `Authorization: Bearer <token>`. ESP32 ingest stays open so devices can still POST telemetry with only a present `deviceId`.

## What to click

1. **Gateway** — three pillars. Ingestion and Command Stream are active; Mesh is locked for the final PoE.
2. **Telemetry** — Part 1 halo grid (unchanged).
3. **Command stream** — FIFO vs priority lanes, search/filter, actuator Energise/De-energise, **Undo last override**, **Automated insights**.
4. Navigate between Telemetry and Command stream: the stream keeps running (shared React context, one SignalR connection).

## How to test Part 2 data structures

### Queue + PriorityQueue (critical bypass)

A severe moisture drop is classified as critical and enqueued on `PriorityQueue<T>` so it is drained **before** ordinary FIFO packets.

```powershell
# 1) Inspect live counters (dictionary size, hash-set faults, queue depths)
curl.exe http://localhost:5266/api/realtime/diagnostics

# 2) Prove priority bypass order (critical envelope is processed first)
curl.exe http://localhost:5266/api/realtime/demo-priority

# 3) Hit ingest like an ESP32: Bed A moisture 12% is below the critical floor (25%)
curl.exe -X POST http://localhost:5266/api/telemetry/ingest -H "Content-Type: application/json" --data-binary "@docs/sample-critical-moisture.json"

# 4) Counters: priorityEnqueued / priorityProcessed should rise
curl.exe http://localhost:5266/api/realtime/diagnostics
```

On the Command stream page click **Demo priority bypass**. Drain order starts with `Priority:DEMO-CRIT`.

UI alternative: Telemetry page → select **Bed A — Soil moisture** → **Inject anomaly spike**. Then open Command stream: the node is Critical, **Pump A — Bed A feed** appears under Automated insights.

### Dictionary (O(1) MAC lookup)

`RealtimeMeshEngine` stores `Dictionary<string, LiveDeviceState>` keyed by MAC. `/api/telemetry/ingest` looks the device up in that dictionary before enqueueing. Unknown MACs are rejected.

```powershell
curl.exe -X POST http://localhost:5266/api/telemetry/ingest -H "Content-Type: application/json" --data-binary "@docs/sample-unknown-device.json"
# -> 400 Unknown device
```

`diagnostics.dictionarySize` is the registered node count.

### SortedDictionary (timeline)

```powershell
curl "http://localhost:5266/api/realtime/timeline?deviceId=AA:7C:3F:10:00:01&take=20"
```

Keys are `(timestamp, packetId)` so the dashboard can paint a sequential graph without a client-side sort of the whole buffer.

### Stack (undo)

```powershell
curl.exe -X POST http://localhost:5266/api/commands -H "Content-Type: application/json" --data-binary "@docs/sample-energise-pump.json"
curl.exe -X POST http://localhost:5266/api/commands/undo
curl.exe http://localhost:5266/api/commands
```

Or in the UI: Command stream → **Pump A** → Energise → **Undo last override**. The actuator boolean flips back.

### HashSet (unique faults / disconnects)

`diagnostics.activeFaults` is a `HashSet<ActiveFault>`. A second disconnect for the same MAC does not add another row.

Silence is simulated every ~30s (two nodes stop publishing). Watch **Unique fault set** on the Command stream page for `MAC|DISCONNECT` entries.

### Predictive insights

The engine mines a sliding window of operator **searches** and **commands**, conditioned on the current critical metric (not a static “if moisture then pump” rule). Priors are seeded so a Bed A moisture spike immediately suggests **Energise Pump A**. Typing `Pump A` in the search box while that spike is active strengthens the association.

```powershell
curl http://localhost:5266/api/realtime/insights
```

## Docker

```powershell
cd C:\Users\khumo\Projects\SmartX
docker compose up --build
```

- UI: `http://localhost:8080`
- API: `http://localhost:5266`

## C# mapping

| Requirement | Where |
| --- | --- |
| `Queue<T>` FIFO telemetry | `RealtimeMeshEngine._standardQueue` |
| `PriorityQueue<T>` critical bypass | `RealtimeMeshEngine._priorityQueue` |
| `Stack<T>` undo | `RealtimeMeshEngine._undoStack` |
| `Dictionary<MAC, state>` O(1) | `RealtimeMeshEngine._devices` |
| `SortedDictionary` timeline | `RealtimeMeshEngine._timeline` |
| `HashSet` unique faults | `_activeFaults`, `_disconnected` |
| Generics / operators / recursion | Part 1 domain types (unchanged) |

## GitHub

https://github.com/KChabeli/SmartX
