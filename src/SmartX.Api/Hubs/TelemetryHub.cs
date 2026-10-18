using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Contracts;
using SmartX.Api.Services;

namespace SmartX.Api.Hubs;

public sealed class TelemetryHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var auth = http?.RequestServices.GetRequiredService<AuthService>();
        var raw = http?.Request.Query["access_token"].ToString();
        if (auth is null || !Guid.TryParse(raw, out var token) || auth.Find(token) is null)
        {
            Context.Abort();
            return;
        }

        await Clients.Caller.SendAsync("hello", new { message = "Smart-X telemetry stream armed." });
        await base.OnConnectedAsync();
    }
}

public interface ITelemetryClient
{
    Task telemetry(TelemetrySampleDto sample);
    Task alert(TelemetrySampleDto sample);
}
