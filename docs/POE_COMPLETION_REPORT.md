# PROG7312 — Smart-X Project Completion Report

**Module:** PROG7312  
**Name:** Khumo-Thato Chabeli  
**Student number:** ST10448834  
**Campus:** Emeris, Ruimsig  
**GitHub:** https://github.com/KChabeli/SmartX.git  
**Date:** 11 November 2026

This report covers the final Portfolio of Evidence: Network Topology and Mesh Routing, plus how Parts 1 and 2 were closed after marker feedback.

## 1. Implementation report

### 1.1 How to compile and run

Restore and build `SmartX.slnx` (.NET 10). Run the API with the `http` launch profile (`http://localhost:5266`). In `client`, `npm install` then `npm run dev` (`http://localhost:5173`). Sign in with MAC `AA:7C:3F:10:00:01` (presence check only). Optional: `docker compose up --build`. Tests: `dotnet test SmartX.slnx`. Full click-path and curl samples live in the root `README.md`.

### 1.2 AVL tree — historical range queries

Part 3 requires a BST/AVL/Red-Black structure so “all moisture readings between 14:00 and 16:00 for Node A” does not scan an unorganised list. Each MAC owns an `AvlTree<TelemetryKey, TelemetrySampleDto>`. The key is `(timestamp, packetId)` so two packets in the same millisecond are not overwritten — the same idea as Part 2’s `SortedDictionary` log key, but here the tree is height-balanced and queried by range.

Insert rebalances with single/double rotations when the balance factor leaves ±1. `Range` walks left only when the window can still sit on that side, emits in-order hits, then walks right. Cost is O(log n + k) for k matches, not O(n).

The Mesh page **AVL range query** posts `from`/`to` to `/api/mesh/history`. The count labelled “Indexed N samples” is the tree size for that MAC; “Matched” is the window.

### 1.3 Graph, BFS and Kruskal MST

A mesh is not a star. ESP32 nodes talk to neighbours (ESP-NOW / Thread / Zigbee in the catalogue) and hop toward `GW-SMARTX`. `MeshGraph` stores vertices (21 sensors + gateway) and undirected weighted edges. Weight is a milli-joule radio budget so “cheapest path” has a physical meaning.

**BFS** starts at the gateway and refuses to enqueue MACs that are in the disconnect set. Reachable vs partitioned lists answer “who is still on this subnet?” and “where can we hop if this node is down?”. Alternate hops are the live neighbours of a silent vertex, cheapest first.

**Kruskal** sorts every live edge by cost, Union-Find accepts an edge only when it joins two components. Long-range Stellenbosch↔Sandton and Sandton↔Cape Flats links cost ~18–21 mJ and stay off the backbone unless an uplink is missing. The cyan overlay is that MST walked from the gateway to the selected MAC — the power-efficient hop path the brief describes.

### 1.3.1 Code (Kruskal accept)

Edges are considered in ascending milli-joule order. `Union` returns false when both ends already share a root, which is how cycles (and expensive shortcuts) are dropped.

### 1.4 Part 2 feedback wired into this PoE

- **Navigation state:** selected MAC, telemetry category, command filters and insights sit on `GatewayProvider` with the SignalR hub, so changing pillar no longer throws operator work away.
- **Queue drain:** HTTP ingest only enqueues. `QueueDrainHostedService` always pops the priority lane first, then FIFO, without a client asking for the next envelope.
- **Tests:** AVL window, Kruskal vs a 40 mJ cheat link, BFS partition, drain order, live-only insights (`tests/SmartX.Tests`).
- **Insights:** `SeedAssociationPriors` is gone. Confidence is support ÷ context events from this session, source `live-operator`.

## 2. Architectural and algorithmic challenges (Task 3)

**Integrating C# mesh math with React.** The graph cannot live only in the browser: Kruskal and BFS must see the same disconnect `HashSet` the ingest engine already maintains. The API owns `MeshGraph` and AVL indexes; the React page is a renderer (SVG vertices/edges, MST vs radio vs hop path). Shared `selectedMac` means a halo click on Telemetry is the node whose hop path Mesh draws.

**Drain vs request.** Part 2 dequeued inside `Ingest()`, so processing only happened when someone posted. The simulator hid that because it posted continuously. A hosted loop made the queues real: the HTTP call returns `TelemetryQueuedDto`; SignalR carries the committed sample after the drain. Command overrides enqueue a boolean packet the same way a device would.

**Healing without rewriting the catalogue.** Edges are static radio neighbours. Down-ness is a filter on BFS/Kruskal, not a delete. That matches ESP-NOW: the neighbour table remains, the next hop is whoever still answers.

**Two working copies.** Visual Studio on the Desktop tree and Git on `Projects\SmartX` drifted during earlier parts. Part 3 is authored in the Git tree so GitHub stays the source of truth.

## 3. Key learnings (tied to the brief)

- **Mesh vs star:** modelling hop cost as edge weight made MST a radio-budget problem, not a drawing.
- **Balanced trees vs lists:** Task 3’s range query is why AVL exists; Part 2’s sorted log is why keys need a packet-id tie-break.
- **Queues as a contract:** “priority then FIFO” is only true if a single drain loop owns both collections.
- **Learning vs seeding:** association confidence that includes hand-written boosts is not evidence of pattern analysis. Removing priors made the empty UI honest.
- **Frontend session:** routed SPAs unmount pages; anything the operator is mid-way through has to live above the router outlet.

## 4. Technology recommendations (10 marks)

1. **MQTT + Azure IoT Hub / Event Hubs** — replace the in-process simulator with MQTT devices. Compatible with .NET (`MQTTnet`, Azure IoT SDK) and matches how real ESP32 firmware publishes.
2. **TimescaleDB or Azure Data Explorer** — AVL is correct for the in-memory PoE; a production gateway should persist hypertables so range queries survive process recycle.
3. **GitHub Actions CI** — `dotnet test` + `npm run build` on every push. The new test project is already the job body. Fits the existing GitHub deliverable.
4. **Home Assistant** (https://www.home-assistant.io/) — expose Smart-X actuators as MQTT entities so operators can use HA dashboards/automations without replacing the Minimal API.
5. **LLM summary API (carefully)** — a daily digest of unique `HashSet` faults, not live control. Keep commands deterministic; use the model only to narrate the already-computed insights list.

These stay inside a .NET-centred ecosystem (CI, Azure, MQTT) and do not require rewriting the Minimal API.

## 5. Declaration

I, Khumo-Thato Chabeli (ST10448834), submit this report together with https://github.com/KChabeli/SmartX.git as the complete functioning Smart-X application for PROG7312 Parts 1–3.
