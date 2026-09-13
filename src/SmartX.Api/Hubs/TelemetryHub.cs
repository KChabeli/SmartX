using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Contracts;

namespace SmartX.Api.Hubs;

public sealed class TelemetryHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("hello", new { message = "Smart-X telemetry stream armed." });
        await base.OnConnectedAsync();
    }
}

public interface ITelemetryClient
{
    Task telemetry(TelemetrySampleDto sample);
    Task alert(TelemetrySampleDto sample);
}
