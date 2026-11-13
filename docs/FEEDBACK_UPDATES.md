# Updates made from lecturer feedback (Parts 1 and 2)

Student: Khumo-Thato Chabeli · ST10448834 · PROG7312

This file is the Part 3 deliverable that lists what changed after marker comments. It is not a substitute for the PDF completion report.

## Part 1 feedback

| Comment | Change |
| --- | --- |
| Add authentication in Part 2. Presence check on MAC only, no format check | `/login` plus `AuthService`. Empty MAC is rejected. Junk such as `not-a-real-mac` is not format-validated; it is looked up in the live device dictionary and refused if unregistered. Commands and SignalR need the bearer token. Device ingest stays open. |
| Live dashboard with a strong visual concept, but no responsive rules | Threshold Halo Grid kept. Explicit `@media` rules at 1024 / 768 / 480 px for header, halo board, command board, sensors, tools and mesh board. |

## Part 2 feedback

| Comment | Change |
| --- | --- |
| Work is not kept when moving between routed pillar pages | `GatewayProvider` already owned SignalR. Selection, telemetry category, command search/filters, insights and the command message now live in that same context, so Telemetry ↔ Command stream ↔ Mesh does not reset operator work. |
| Real queue flow depended on a request asking for the next item. A background service should drain priority then FIFO | `TelemetryIngestService.Ingest` only enqueues. `QueueDrainHostedService` loops, always pops `PriorityQueue` before `Queue<T>`, then commits. Demo-priority remains a teaching overlay; tests prove the live drain order. |
| Add tests | `tests/SmartX.Tests` — AVL range query, Kruskal vs expensive shortcut, BFS partition when a hop is down, drain order, insights with live counts only. `dotnet test SmartX.slnx`. |
| Suggestion engine seeded starting patterns; confidence must come from real counts | `SeedAssociationPriors` removed. Insights are labelled `live-operator`. Confidence is `support / contextCount` from this session. Empty state tells the operator to search/command during a spike. |

## Part 3 (this submission)

Network Topology is unlocked. AVL trees index history per MAC. The mesh graph, BFS healing and Kruskal MST are rendered on **Mesh routing**.
