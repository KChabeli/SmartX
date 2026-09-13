using SmartX.Domain.Telemetry;

namespace SmartX.Domain.Alerting;

/// <summary>
/// Semantic colour-coded threshold engine from the Task 1 research report.
/// Maps a live reading plus last-seen age onto Nominal / Warning / Critical / Disconnected.
/// </summary>
public static class ThresholdPalette
{
    public const string NominalHex = "#22C55E";
    public const string WarningHex = "#F59E0B";
    public const string CriticalHex = "#EF4444";
    public const string DisconnectedHex = "#94A3B8";

    public static AlertSeverity EvaluateNumeric(double value, SensorThresholds thresholds, bool isStale)
    {
        if (isStale)
        {
            return AlertSeverity.Disconnected;
        }

        if (value <= thresholds.CriticalLow || value >= thresholds.CriticalHigh)
        {
            return AlertSeverity.Critical;
        }

        if (value <= thresholds.WarningLow || value >= thresholds.WarningHigh)
        {
            return AlertSeverity.Warning;
        }

        return AlertSeverity.Nominal;
    }

    public static AlertSeverity EvaluateBoolean(bool value, bool expectedEnergised, bool isStale)
    {
        if (isStale)
        {
            return AlertSeverity.Disconnected;
        }

        return value == expectedEnergised ? AlertSeverity.Nominal : AlertSeverity.Warning;
    }

    public static bool IsStale(DateTimeOffset lastSeenUtc, TimeSpan disconnectAfter, DateTimeOffset nowUtc)
        => nowUtc - lastSeenUtc >= disconnectAfter;

    public static string ToCss(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Nominal => NominalHex,
        AlertSeverity.Warning => WarningHex,
        AlertSeverity.Critical => CriticalHex,
        AlertSeverity.Disconnected => DisconnectedHex,
        _ => NominalHex
    };

    public static string ToLabel(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Nominal => "Nominal",
        AlertSeverity.Warning => "Warning",
        AlertSeverity.Critical => "Critical spike",
        AlertSeverity.Disconnected => "Sensor disconnect",
        _ => "Unknown"
    };
}

public readonly record struct SensorThresholds(
    double WarningLow,
    double WarningHigh,
    double CriticalLow,
    double CriticalHigh);
