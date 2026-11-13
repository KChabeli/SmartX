using SmartX.Domain.Trees;
using Xunit;

namespace SmartX.Tests;

public class AvlTreeTests
{
    [Fact]
    public void Range_returns_only_samples_inside_the_time_window()
    {
        var tree = new AvlTree<TelemetryKey, string>();
        var day = new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);
        tree.Insert(new TelemetryKey(day.AddHours(13), Guid.NewGuid()), "13:00");
        tree.Insert(new TelemetryKey(day.AddHours(14).AddMinutes(5), Guid.NewGuid()), "14:05");
        tree.Insert(new TelemetryKey(day.AddHours(15).AddMinutes(40), Guid.NewGuid()), "15:40");
        tree.Insert(new TelemetryKey(day.AddHours(17), Guid.NewGuid()), "17:00");

        var window = tree.Range(
            TelemetryKey.StartOf(day.AddHours(14)),
            TelemetryKey.EndOf(day.AddHours(16)));

        Assert.Equal(["14:05", "15:40"], window);
        Assert.Equal(4, tree.Count);
    }
}
