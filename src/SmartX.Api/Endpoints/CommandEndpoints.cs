using SmartX.Api.Contracts;
using SmartX.Api.Services;
using SmartX.Domain.Processing;
using SmartX.Domain.Sensors;
using SmartX.Domain.Telemetry;

namespace SmartX.Api.Endpoints;

public static class CommandEndpoints
{
    public static IEndpointRouteBuilder MapCommandEndpoints(this IEndpointRouteBuilder app)
    {
        var realtime = app.MapGroup("/api/realtime").WithTags("Realtime");

        realtime.MapGet("/diagnostics", (RealtimeMeshEngine engine) => Results.Ok(engine.Diagnostics()));
        realtime.MapGet("/timeline", (string? deviceId, int? take, RealtimeMeshEngine engine)
            => Results.Ok(engine.Timeline(deviceId, take ?? 180)));
        realtime.MapGet("/insights", (RealtimeMeshEngine engine) => Results.Ok(engine.BuildInsights()));
        realtime.MapGet("/demo-priority", (RealtimeMeshEngine engine) => Results.Ok(engine.DemoPriorityBypass()));

        realtime.MapPost("/search", (DeviceSearchRequest request, RealtimeMeshEngine engine, TelemetryIngestService ingest) =>
        {
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                engine.RecordSearch(request.Query);
            }

            var nodes = ingest.Snapshot();
            var filtered = nodes.Where(n =>
            {
                if (!string.IsNullOrWhiteSpace(request.Category)
                    && !n.Sensor.Category.ToString().Equals(request.Category, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(request.Severity)
                    && !n.Severity.ToString().Equals(request.Severity, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(request.Query))
                {
                    return true;
                }

                var q = request.Query.Trim();
                return n.Sensor.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                       || n.Sensor.MacAddress.Contains(q, StringComparison.OrdinalIgnoreCase)
                       || n.Sensor.Location.Contains(q, StringComparison.OrdinalIgnoreCase)
                       || n.Sensor.Metric.Contains(q, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            return Results.Ok(new
            {
                matches = filtered,
                relatedActuators = RealtimeMeshEngine.RelatedActuators,
                insights = engine.BuildInsights()
            });
        });

        var commands = app.MapGroup("/api/commands").WithTags("Commands");
        commands.MapGet("/", (RealtimeMeshEngine engine)
            => Results.Ok(engine.CommandHistory().Select(RealtimeMeshEngine.ToDto)));

        commands.MapPost("/", async (IssueCommandRequest request, RealtimeMeshEngine engine, TelemetryIngestService ingest) =>
        {
            if (!Enum.TryParse<CommandVerb>(request.Verb, true, out var verb))
            {
                return Results.BadRequest("Verb must be Energise or DeEnergise.");
            }

            try
            {
                var command = engine.IssueCommand(request.TargetMac, verb, request.Reason ?? "manual-override");
                ingest.Ingest(new TelemetryIngestRequest(
                    command.TargetMac,
                    "override",
                    TelemetryValueKind.Boolean,
                    null,
                    command.NewState,
                    "bool",
                    Environment.TickCount));
                await engine.BroadcastCommandAsync(command);
                return Results.Ok(new { command = RealtimeMeshEngine.ToDto(command), insights = engine.BuildInsights() });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        commands.MapPost("/undo", async (RealtimeMeshEngine engine, TelemetryIngestService ingest) =>
        {
            var revert = engine.UndoLast();
            if (revert is null)
            {
                return Results.Conflict("Undo stack is empty.");
            }

            ingest.Ingest(new TelemetryIngestRequest(
                revert.TargetMac,
                "override",
                TelemetryValueKind.Boolean,
                null,
                revert.NewState,
                "bool",
                Environment.TickCount));
            await engine.BroadcastCommandAsync(revert);
            return Results.Ok(RealtimeMeshEngine.ToDto(revert));
        });

        return app;
    }
}
