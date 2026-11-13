using Microsoft.AspNetCore.SignalR;
using Xunit;
using SmartX.Api.Contracts;
using SmartX.Api.Hubs;
using SmartX.Api.Services;
using SmartX.Domain.Alerting;
using SmartX.Domain.Processing;
using SmartX.Domain.Sensors;
using SmartX.Domain.Telemetry;

namespace SmartX.Tests;

public class RealtimeEngineTests
{
    [Fact]
    public void Background_style_drain_pops_priority_before_fifo()
    {
        var engine = new RealtimeMeshEngine(new UnusedHub());
        var moisture = Moisture();
        engine.RegisterDevice(moisture);

        engine.Enqueue(new TelemetryIngestRequest(moisture.MacAddress, "soil_moisture", TelemetryValueKind.Float, 50, null, "%", 1), moisture);
        engine.Enqueue(new TelemetryIngestRequest(moisture.MacAddress, "soil_moisture", TelemetryValueKind.Float, 48, null, "%", 2), moisture);
        engine.Enqueue(new TelemetryIngestRequest(moisture.MacAddress, "soil_moisture", TelemetryValueKind.Float, 12, null, "%", 3), moisture);

        Assert.True(engine.TryDequeueNext(out var first));
        Assert.Equal(IngestLane.Priority, first.Lane);
        Assert.Equal(12, first.NumericValue);
        Assert.True(engine.TryDequeueNext(out var second));
        Assert.Equal(IngestLane.Standard, second.Lane);
    }

    [Fact]
    public void Insights_use_only_live_operator_counts()
    {
        var engine = new RealtimeMeshEngine(new UnusedHub());
        var moisture = Moisture();
        var pump = Pump();
        engine.RegisterDevice(moisture);
        engine.RegisterDevice(pump);

        engine.ObserveCommitted(new TelemetrySampleDto(
            Guid.NewGuid(), moisture.MacAddress, moisture.DisplayName, moisture.Location, moisture.Metric, "%",
            TelemetryValueKind.Float, 12, null, DateTimeOffset.UtcNow, 1, AlertSeverity.Critical, "Critical spike", "#EF4444", false, 0));

        Assert.Empty(engine.BuildInsights());

        engine.IssueCommand(pump.MacAddress, CommandVerb.Energise, "live-test");
        var insights = engine.BuildInsights();
        Assert.Single(insights);
        Assert.Equal("live-operator", insights[0].Source);
        Assert.Equal(1, insights[0].SupportCount);
        Assert.Equal(1, insights[0].ContextCount);
        Assert.Equal(100, insights[0].Confidence);
        Assert.Contains("no preloaded associations", insights[0].Rationale);
    }

    private static SensorProfile Moisture() => new()
    {
        MacAddress = "AA:7C:3F:10:00:01",
        DisplayName = "Bed A — Soil moisture",
        Location = "Stellenbosch Hydroponics / Zone 1 / Sub-Zone B",
        Category = SensorCategory.Environmental,
        Metric = "soil_moisture",
        Unit = "%",
        ValueKind = TelemetryValueKind.Float,
        WarningLow = 40,
        WarningHigh = 70,
        CriticalLow = 25,
        CriticalHigh = 85,
        DeploymentPath = "Stellenbosch Hydroponics / Zone 1 / Sub-Zone B"
    };

    private static SensorProfile Pump() => new()
    {
        MacAddress = "AA:7C:3F:10:00:08",
        DisplayName = "Pump A — Bed A feed",
        Location = "Stellenbosch Hydroponics / Zone 1 / Sub-Zone B",
        Category = SensorCategory.Actuator,
        Metric = "pump_on",
        Unit = "bool",
        ValueKind = TelemetryValueKind.Boolean,
        DeploymentPath = "Stellenbosch Hydroponics / Zone 1 / Sub-Zone B"
    };

    private sealed class UnusedHub : IHubContext<TelemetryHub>
    {
        public IHubClients Clients => throw new NotSupportedException();
        public IGroupManager Groups => throw new NotSupportedException();
    }
}
