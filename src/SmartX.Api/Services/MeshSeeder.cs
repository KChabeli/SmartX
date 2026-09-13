using SmartX.Api.Contracts;
using SmartX.Domain.Deployment;
using SmartX.Domain.Sensors;
using SmartX.Domain.Telemetry;

namespace SmartX.Api.Services;

public static class MeshSeeder
{
    public static void Seed(GatewayStore store, TelemetryIngestService ingest)
    {
        foreach (var sensor in BuildCatalogue())
        {
            store.Sensors[sensor.MacAddress] = sensor;
        }

        const int windows = 8;
        const int windowSize = 30;
        var start = DateTimeOffset.UtcNow.AddSeconds(-(windows * windowSize * 2));
        foreach (var sensor in store.Sensors.Values)
        {
            SeedHistory(sensor, ingest, start);
        }
    }

    public static IReadOnlyList<SensorProfile> BuildCatalogue() =>
    [
        Env("AA:7C:3F:10:00:01", "Bed A — Soil moisture", "Stellenbosch Hydroponics / Zone 1 / Sub-Zone B", "soil_moisture", "%", 40, 70, 25, 85),
        Env("AA:7C:3F:10:00:02", "Bed A — Canopy temperature", "Stellenbosch Hydroponics / Zone 1 / Sub-Zone B", "temperature", "°C", 18, 28, 10, 36),
        Env("AA:7C:3F:10:00:03", "Bed A — Nutrient EC", "Stellenbosch Hydroponics / Zone 1 / Sub-Zone B", "electro_conductivity", "mS/cm", 1.2, 2.4, 0.6, 3.2),
        Env("AA:7C:3F:10:00:04", "Bed B — Soil moisture", "Stellenbosch Hydroponics / Zone 1 / Sub-Zone C", "soil_moisture", "%", 40, 70, 25, 85),
        Env("AA:7C:3F:10:00:05", "Reservoir pH", "Stellenbosch Hydroponics / Zone 2 / Sub-Zone A", "ph", "pH", 5.6, 6.6, 4.8, 7.4),
        Act("AA:7C:3F:10:00:06", "Irrigation valve A1", "Stellenbosch Hydroponics / Zone 1 / Sub-Zone B", "valve_open"),
        Act("AA:7C:3F:10:00:07", "Fogger relay", "Stellenbosch Hydroponics / Zone 2 / Sub-Zone A", "relay_on"),
        Pwr("AA:7C:3F:20:00:11", "Pump stack kWh meter", "Stellenbosch Hydroponics / Zone 2 / Sub-Zone A", 400, 1800, 50, 3200),
        Env("BB:91:44:20:10:01", "Unit 12B — Occupancy temp", "Sandton Estate / Tower A / Floor 12", "temperature", "°C", 19, 24, 14, 30),
        Env("BB:91:44:20:10:02", "Unit 12B — Humidity", "Sandton Estate / Tower A / Floor 12", "humidity", "%", 35, 60, 20, 80),
        Pwr("BB:91:44:20:10:03", "Unit 12B — Smart meter", "Sandton Estate / Tower A / Floor 12", 200, 2400, 0, 5000),
        Pwr("BB:91:44:20:10:04", "Basement HVAC meter", "Sandton Estate / Tower A / Plant Room", 800, 4500, 100, 8000),
        Env("BB:91:44:20:10:05", "Water cistern level", "Sandton Estate / Tower A / Plant Room", "tank_level", "%", 30, 90, 10, 98),
        Act("BB:91:44:20:10:06", "Hot-water solenoid", "Sandton Estate / Tower A / Plant Room", "valve_open"),
        Pwr("CC:18:90:30:00:21", "Feeder 3 — Grid wattage", "Cape Flats Microgrid / Substation 3 / Bay A", 1200, 8500, 200, 14000),
        Pwr("CC:18:90:30:00:22", "Feeder 4 — Grid wattage", "Cape Flats Microgrid / Substation 3 / Bay B", 1200, 8500, 200, 14000),
        Env("CC:18:90:30:00:23", "Transformer oil temp", "Cape Flats Microgrid / Substation 3 / Bay A", "temperature", "°C", 35, 70, 20, 95),
        Act("CC:18:90:30:00:24", "Isolator 3A", "Cape Flats Microgrid / Substation 3 / Bay A", "breaker_closed"),
        Env("CC:18:90:30:00:25", "Street cabinet humidity", "Cape Flats Microgrid / Substation 3 / Bay B", "humidity", "%", 20, 70, 10, 90),
        Pwr("CC:18:90:30:00:26", "Community battery inverter", "Cape Flats Microgrid / Storage Yard / Rack 1", 0, 4000, -500, 6200)
    ];

    public static DeploymentNode DefaultTree() => new()
    {
        Id = "facility-a",
        Name = "Facility A",
        Kind = DeploymentKind.Facility,
        Children =
        [
            new DeploymentNode
            {
                Id = "zone-1",
                Name = "Zone 1",
                Kind = DeploymentKind.Zone,
                Children =
                [
                    new DeploymentNode
                    {
                        Id = "sub-zone-b",
                        Name = "Sub-Zone B",
                        Kind = DeploymentKind.SubZone,
                        Children =
                        [
                            new DeploymentNode
                            {
                                Id = "node-a1",
                                Name = "ESP32 Bed A moisture",
                                Kind = DeploymentKind.Node,
                                MacAddress = "AA:7C:3F:10:00:01"
                            }
                        ]
                    }
                ]
            }
        ]
    };

    private static void SeedHistory(SensorProfile sensor, TelemetryIngestService ingest, DateTimeOffset start)
    {
        const int windowSize = 30;
        const int windows = 8;
        var interval = TimeSpan.FromSeconds(2);

        switch (sensor.ValueKind)
        {
            case TelemetryValueKind.Float:
            {
                var store = new HistoricalBatchStore<float>();
                for (var w = 0; w < windows; w++)
                {
                    var burst = new float[windowSize];
                    for (var i = 0; i < windowSize; i++)
                    {
                        burst[i] = (float)SampleNumeric(sensor, w * windowSize + i);
                    }

                    store.AddWindow(burst);
                }

                foreach (var packet in store.FlushToCollection(sensor.MacAddress, sensor.Metric, sensor.Unit, start, interval))
                {
                    ingest.IngestFloat(packet);
                }

                break;
            }
            case TelemetryValueKind.Integer:
            {
                var store = new HistoricalBatchStore<int>();
                for (var w = 0; w < windows; w++)
                {
                    var burst = new int[windowSize];
                    for (var i = 0; i < windowSize; i++)
                    {
                        burst[i] = (int)Math.Round(SampleNumeric(sensor, w * windowSize + i));
                    }

                    store.AddWindow(burst);
                }

                foreach (var packet in store.FlushToCollection(sensor.MacAddress, sensor.Metric, sensor.Unit, start, interval))
                {
                    ingest.IngestInt(packet);
                }

                break;
            }
            default:
            {
                var store = new HistoricalBatchStore<bool>();
                for (var w = 0; w < windows; w++)
                {
                    var burst = new bool[windowSize];
                    for (var i = 0; i < windowSize; i++)
                    {
                        burst[i] = SampleBool(sensor, w * windowSize + i);
                    }

                    store.AddWindow(burst);
                }

                foreach (var packet in store.FlushToCollection(sensor.MacAddress, sensor.Metric, sensor.Unit, start, interval))
                {
                    ingest.IngestBool(packet);
                }

                break;
            }
        }
    }

    public static double SampleNumeric(SensorProfile sensor, int tick)
    {
        var mid = ((sensor.WarningLow ?? 0) + (sensor.WarningHigh ?? 100)) / 2.0;
        var wave = Math.Sin(tick / 18.0 + sensor.MacAddress.GetHashCode() % 9) * ((sensor.WarningHigh ?? 100) - mid) * 0.45;
        var noise = Math.Sin(tick * 1.7) * 1.4;
        var value = mid + wave + noise;

        if (sensor.MacAddress.EndsWith("04") && tick % 90 > 78)
        {
            value = (sensor.CriticalHigh ?? 100) + 8;
        }

        if (sensor.MacAddress.EndsWith("21") && tick % 110 > 100)
        {
            value = (sensor.CriticalHigh ?? 12000) * 0.98;
        }

        return Math.Round(value, 2);
    }

    public static bool SampleBool(SensorProfile sensor, int tick)
        => !sensor.MacAddress.EndsWith("07") || tick % 40 < 32;

    private static SensorProfile Env(string mac, string name, string location, string metric, string unit,
        double warnLow, double warnHigh, double critLow, double critHigh)
        => new()
        {
            MacAddress = mac,
            DisplayName = name,
            Location = location,
            Category = SensorCategory.Environmental,
            Metric = metric,
            Unit = unit,
            ValueKind = TelemetryValueKind.Float,
            WarningLow = warnLow,
            WarningHigh = warnHigh,
            CriticalLow = critLow,
            CriticalHigh = critHigh,
            DeploymentPath = location
        };

    private static SensorProfile Pwr(string mac, string name, string location, double warnLow, double warnHigh, double critLow, double critHigh)
        => new()
        {
            MacAddress = mac,
            DisplayName = name,
            Location = location,
            Category = SensorCategory.PowerConsumption,
            Metric = "wattage",
            Unit = "W",
            ValueKind = TelemetryValueKind.Integer,
            WarningLow = warnLow,
            WarningHigh = warnHigh,
            CriticalLow = critLow,
            CriticalHigh = critHigh,
            DeploymentPath = location
        };

    private static SensorProfile Act(string mac, string name, string location, string metric)
        => new()
        {
            MacAddress = mac,
            DisplayName = name,
            Location = location,
            Category = SensorCategory.Actuator,
            Metric = metric,
            Unit = "bool",
            ValueKind = TelemetryValueKind.Boolean,
            DeploymentPath = location
        };
}
