using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class MeshEndpoints
{
    public static IEndpointRouteBuilder MapMeshEndpoints(this IEndpointRouteBuilder app)
    {
        var mesh = app.MapGroup("/api/mesh").WithTags("Mesh");
        mesh.AddEndpointFilter(async (context, next) =>
        {
            var auth = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
            if (!AuthService.TryReadToken(context.HttpContext, out var token) || auth.Find(token) is null)
            {
                return Results.Unauthorized();
            }

            return await next(context);
        });

        mesh.MapGet("/topology", (string? mac, MeshTopologyService topology) =>
            Results.Ok(topology.Snapshot(mac)));

        mesh.MapGet("/history", (string deviceId, DateTimeOffset? from, DateTimeOffset? to, MeshTopologyService topology) =>
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return Results.BadRequest("deviceId is required.");
            }

            var start = from ?? DateTimeOffset.UtcNow.AddHours(-2);
            var end = to ?? DateTimeOffset.UtcNow;
            if (end < start)
            {
                (start, end) = (end, start);
            }

            var rows = topology.Range(deviceId, start, end);
            return Results.Ok(new
            {
                deviceId,
                from = start,
                to = end,
                avlCount = topology.IndexedCount(deviceId),
                matched = rows.Count,
                samples = rows
            });
        });

        return app;
    }
}
