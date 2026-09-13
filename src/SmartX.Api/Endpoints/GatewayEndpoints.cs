using SmartX.Api.Contracts;
using SmartX.Api.Services;
using SmartX.Domain.Sensors;

namespace SmartX.Api.Endpoints;

public static class GatewayEndpoints
{
    public static IEndpointRouteBuilder MapGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/pillars", () => Results.Ok(new PillarDto[]
        {
            new("ingestion", "Sensor Data Ingestion and Telemetry",
                "Live ESP32 telemetry gateway with colour-coded threshold alerting.", true, null),
            new("commands", "Real-Time Command Stream and History",
                "Bidirectional actuator command bus and audit trail.", false, "Part 2"),
            new("mesh", "Network Topology and Mesh Routing",
                "Visual mesh routing, hop health and gateway failover.", false, "Final PoE")
        })).WithTags("Gateway");

        var sensors = app.MapGroup("/api/sensors").WithTags("Sensors");
        sensors.MapGet("/", (GatewayStore store) =>
            Results.Ok(store.Sensors.Values.Select(TelemetryIngestService.ToDto)));

        sensors.MapPost("/", (RegisterSensorRequest request, GatewayStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.MacAddress))
            {
                return Results.BadRequest("MAC address / unique identifier is required.");
            }

            var profile = new SensorProfile
            {
                MacAddress = request.MacAddress.Trim().ToUpperInvariant(),
                DisplayName = request.DisplayName,
                Location = request.Location,
                Category = request.Category,
                Metric = request.Metric,
                Unit = request.Unit,
                ValueKind = request.ValueKind,
                WarningLow = request.WarningLow,
                WarningHigh = request.WarningHigh,
                CriticalLow = request.CriticalLow,
                CriticalHigh = request.CriticalHigh,
                DeploymentPath = request.DeploymentPath ?? request.Location
            };
            store.Sensors[profile.MacAddress] = profile;
            return Results.Created($"/api/sensors/{profile.MacAddress}", TelemetryIngestService.ToDto(profile));
        });

        sensors.MapPost("/{mac}/attachments", async (string mac, IFormFile file, GatewayStore store, AttachmentStorage storage, CancellationToken ct) =>
        {
            if (!store.Sensors.TryGetValue(mac, out var sensor))
            {
                return Results.NotFound();
            }

            if (file.Length == 0)
            {
                return Results.BadRequest("Empty file.");
            }

            var attachment = await storage.SaveAsync(sensor.Id, file, ct);
            sensor.Attachments.Add(attachment);
            return Results.Ok(new AttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.ByteSize, attachment.UploadedAt));
        }).DisableAntiforgery();

        sensors.MapGet("/{mac}/attachments/{attachmentId:guid}", (string mac, Guid attachmentId, GatewayStore store, AttachmentStorage storage) =>
        {
            if (!store.Sensors.TryGetValue(mac, out var sensor))
            {
                return Results.NotFound();
            }

            var resolved = storage.Resolve(sensor, attachmentId);
            return resolved is null
                ? Results.NotFound()
                : Results.File(resolved.Value.Path, resolved.Value.ContentType, resolved.Value.DownloadName);
        });

        var telemetry = app.MapGroup("/api/telemetry").WithTags("Telemetry");
        telemetry.MapPost("/ingest", (TelemetryIngestRequest request, TelemetryIngestService ingest) =>
        {
            try
            {
                return Results.Accepted(value: ingest.Ingest(request));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        telemetry.MapGet("/", (string? deviceId, int? take, TelemetryIngestService ingest)
            => Results.Ok(ingest.History(deviceId, take ?? 240)));

        telemetry.MapGet("/snapshot", (TelemetryIngestService ingest) => Results.Ok(ingest.Snapshot()));

        telemetry.MapGet("/heat", (GatewayStore store) =>
        {
            var zones = store.Sensors.Values
                .Select(s => s.Location.Split('/')[0].Trim())
                .Distinct()
                .ToList();
            var grid = new Domain.Telemetry.ZoneHeatGrid(zones.Count, 12);
            for (var z = 0; z < zones.Count; z++)
            {
                var zoneName = zones[z];
                var members = store.Sensors.Values.Where(s => s.Location.StartsWith(zoneName, StringComparison.OrdinalIgnoreCase)).ToList();
                for (var m = 0; m < 12; m++)
                {
                    var worst = Domain.Telemetry.AlertSeverity.Nominal;
                    foreach (var sensor in members)
                    {
                        if (store.LatestSeverity.TryGetValue(sensor.MacAddress, out var severity) && severity > worst)
                        {
                            worst = severity;
                        }
                    }

                    grid[z, m] = worst;
                }
            }

            return Results.Ok(new
            {
                zones,
                cells = zones.Select((name, z) => new { zone = name, worst = grid.WorstInZone(z) })
            });
        });

        telemetry.MapPost("/simulate-spike", (string deviceId, GatewayStore store, TelemetryIngestService ingest) =>
        {
            if (!store.Sensors.TryGetValue(deviceId, out var sensor))
            {
                return Results.NotFound();
            }

            var sample = ingest.Ingest(new TelemetryIngestRequest(
                sensor.MacAddress,
                sensor.Metric,
                sensor.ValueKind,
                (sensor.CriticalHigh ?? 100) * 1.15,
                true,
                sensor.Unit,
                Environment.TickCount));
            return Results.Ok(sample);
        });

        app.MapPost("/api/meters/aggregate", (MeterAggregateRequest request) =>
        {
            var left = new SmartMeterReading(request.LeftId, request.LeftWatts, request.LeftPf);
            var right = new SmartMeterReading(request.RightId, request.RightWatts, request.RightPf);
            var combined = left + right;
            var delta = left - right;
            return Results.Ok(new MeterAggregateDto(
                $"{left} + {right} = {combined} (delta {delta.Watts}W)",
                combined.Watts,
                combined.PowerFactor,
                left > right));
        }).WithTags("Meters");

        app.MapPost("/api/deployments/validate", (DeploymentValidateRequest request) =>
        {
            var domain = MapNode(request.Root);
            var result = Domain.Deployment.DeploymentValidator.Validate(domain, request.RequiredMac);
            var chain = request.RequiredMac is null
                ? null
                : Domain.Deployment.DeploymentValidator.FindAncestorChain(domain, request.RequiredMac);
            return Results.Ok(new DeploymentValidateDto(result.IsSafe, result.Errors, result.ResolvedPath, chain));
        }).WithTags("Deployment");

        app.MapGet("/api/deployments/sample", () =>
        {
            var tree = MeshSeeder.DefaultTree();
            var result = Domain.Deployment.DeploymentValidator.Validate(tree, "AA:7C:3F:10:00:01");
            var chain = Domain.Deployment.DeploymentValidator.FindAncestorChain(tree, "AA:7C:3F:10:00:01");
            return Results.Ok(new
            {
                tree,
                validation = new DeploymentValidateDto(result.IsSafe, result.Errors, result.ResolvedPath, chain)
            });
        }).WithTags("Deployment");

        return app;
    }

    private static Domain.Deployment.DeploymentNode MapNode(DeploymentNodeDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Kind = dto.Kind,
        MacAddress = dto.MacAddress,
        Children = dto.Children?.Select(MapNode).ToList() ?? []
    };
}
