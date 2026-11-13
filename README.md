# Smart-X IoT Mesh Gateway — Final PoE (Parts 1–3)

ASP.NET Core 10 Minimal API + React dashboard for a simulated South African ESP32 mesh (Stellenbosch hydroponics, Sandton estate, Cape Flats microgrid).

- **Part 1** — colour-coded ingest (Threshold Halo Grid, SignalR, `TelemetryPacket<T>`).
- **Part 2** — real-time processing engine: FIFO/priority queues drained by a **background service**, undo stack, O(1) device dictionary, sorted timeline, unique fault set, live-only insights.
- **Part 3** — Network Topology and Mesh Routing: AVL historical index, radio graph, BFS healing, Kruskal MST hop backbone.

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
curl.exe -X POST http://localhost:5266/api/auth/login -H "Content-Type: application/json" --data-binary '{"macAddress":"AA:7C:3F:10:00:01"}'
```

Command overrides require `Authorization: Bearer <token>`. ESP32 ingest stays open so devices can still POST telemetry with only a present `deviceId`.

## What to click

1. **Gateway** — three pillars, all active.
2. **Telemetry** — Threshold Halo Grid. Category filter is kept if you leave the page.
3. **Command stream** — FIFO vs priority lanes (depths move because a hosted drain pops priority then FIFO without an HTTP “next” call). Search/filter, Energise/De-energise, **Undo last override**, **Automated insights** (live operator counts only).
4. **Mesh routing** — click a MAC, cyan hop path is the Kruskal MST route to the gateway, dashed nodes are BFS-partitioned, AVL range query lists that node’s history.
5. Navigate Telemetry ↔ Commands ↔ Mesh: `GatewayProvider` keeps SignalR, the selected MAC, filters and insights. The API is not restarted.

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

The engine mines a sliding window of operator **searches** and **commands**, conditioned on the current critical metric. **No seeded priors.** Confidence is `support / contextCount` from this session only. Inject a Bed A moisture spike, search `Pump A`, then Energise — the insight appears afterwards with a live count.

```powershell
curl http://localhost:5266/api/realtime/insights
```

### Background queue drain

`QueueDrainHostedService` loops independently of HTTP. Ingest only enqueues. The loop always dequeues `PriorityQueue` first, then `Queue<T>`, then commits via SignalR. Watch `priorityProcessed` / `standardProcessed` on `/api/realtime/diagnostics` rise without calling demo-priority.

```powershell
dotnet test SmartX.slnx
```

## Part 3 — mesh topology

Login, open **Mesh routing**, select `AA:7C:3F:10:00:01`.

```powershell
curl.exe "http://localhost:5266/api/mesh/topology?mac=AA:7C:3F:10:00:01"
curl.exe "http://localhost:5266/api/mesh/history?deviceId=AA:7C:3F:10:00:01"
```

- **AVL tree** (`AvlTree<TelemetryKey, TelemetrySampleDto>`) stores timestamped samples per MAC. The history table is a range query (`from`/`to`), not a scan of the ingest list. Key is `(timestamp, packetId)` so two packets at the same tick are not overwritten.
- **Graph** (`MeshGraph`) — vertices = 21 ESP32 nodes + `GW-SMARTX`. Edges = ESP-NOW / Thread / Zigbee radio with a milli-joule cost.
- **BFS** from the gateway, skipping silent MACs, lists reachable vs partitioned nodes and suggests live neighbours to hop through.
- **Kruskal MST** is the cheapest set of live radio edges that still spans the mesh. The cyan path is that tree walked from gateway → selected MAC.

### Why these structures (efficiency)

**AVL:** an unbalanced list of 20 000 packets would be O(n) per “14:00–16:00 for Node A”. AVL range walks only the overlapping subtree, O(log n + k). Height is kept ±1 by rotations in `AvlTree.Insert`.

```csharp
var rows = tree.Range(TelemetryKey.StartOf(from), TelemetryKey.EndOf(to));
```

**Graph + BFS:** adjacency lists make “who can I hear?” O(V+E). When a node is in the disconnect `HashSet`, BFS simply does not enqueue it, which is how the mesh heals without rewriting the catalogue.

**Kruskal MST:** sort edges by milli-joule, Union-Find to skip cycles. The expensive Stellenbosch↔Sandton long-range links (18–21 mJ) stay out of the backbone unless a facility uplink is down. That is the power-efficient hop story the brief asks for (same idea as Zigbee/Thread/ESP-NOW choosing cheap neighbours).

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
| Background drain | `QueueDrainHostedService` |
| AVL historical index | `AvlTree` + `MeshTopologyService` |
| Graph / BFS / Kruskal MST | `MeshGraph` |
| Generics / operators / recursion | Part 1 domain types (unchanged) |

## GitHub

https://github.com/KChabeli/SmartX

Lecturer feedback log: `docs/FEEDBACK_UPDATES.md`. Project completion report: `docs/POE_COMPLETION_REPORT.md`.
