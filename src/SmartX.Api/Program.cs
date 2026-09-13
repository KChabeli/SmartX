using System.Text.Json;
using System.Text.Json.Serialization;
using SmartX.Api.Endpoints;
using SmartX.Api.Hubs;
using SmartX.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddOpenApi();
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("smartx", policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:8080",
                "http://127.0.0.1:8080")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddSingleton<GatewayStore>();
builder.Services.AddSingleton<AttachmentStorage>();
builder.Services.AddSingleton<TelemetryIngestService>();
builder.Services.AddHostedService<TelemetrySimulatorHostedService>();

var app = builder.Build();

var store = app.Services.GetRequiredService<GatewayStore>();
var ingest = app.Services.GetRequiredService<TelemetryIngestService>();
MeshSeeder.Seed(store, ingest);

app.UseCors("smartx");
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGatewayEndpoints();
app.MapHub<TelemetryHub>("/hubs/telemetry");
app.MapGet("/health", () => Results.Ok(new { status = "ok", nodes = store.Sensors.Count }));

app.Lifetime.ApplicationStarted.Register(() =>
{
    var log = app.Logger;
    log.LogInformation("Smart-X API is running and waiting for requests — this window is supposed to stay open.");
    log.LogInformation("Dashboard: cd client && npm run dev  →  http://localhost:5173");
    log.LogInformation("Health check: {Health}", "http://localhost:5266/health");
});

app.Run();
