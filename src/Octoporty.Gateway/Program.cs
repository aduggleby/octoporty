// Program.cs
// Entry point for the Octoporty Gateway service.
// Configures WebSocket endpoint for Agent connections with pre-connection API key validation.
// Validates API key length at startup (minimum 32 characters).
// Registers tunnel services and request routing middleware.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Octoporty.Gateway.Features.Test;
using Octoporty.Gateway.Services;
using Octoporty.Shared.Logging;
using Octoporty.Shared.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOctoportySerilog("Octoporty.Gateway");

builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection("Gateway"));
builder.Services.Configure<LoggingOptions>(builder.Configuration.GetSection("Logging"));

builder.Services.AddSingleton<TunnelConnectionManager>();
builder.Services.AddSingleton<ITunnelConnectionManager>(sp => sp.GetRequiredService<TunnelConnectionManager>());
builder.Services.AddTransient<TunnelWebSocketHandler>();
builder.Services.AddHttpClient<ICaddyAdminClient, CaddyAdminClient>();

var app = builder.Build();

// Validate required configuration at startup
var gatewayOptions = app.Services.GetRequiredService<IOptions<GatewayOptions>>().Value;
if (string.IsNullOrWhiteSpace(gatewayOptions.ApiKey) || gatewayOptions.ApiKey.Length < 32)
{
    throw new InvalidOperationException("Gateway__ApiKey must be at least 32 characters");
}

app.UseWebSockets();
app.UseRequestRouting();

// Health endpoint - minimal info for unauthenticated access
app.MapGet("/health", (ITunnelConnectionManager connectionManager) =>
{
    var hasConnection = connectionManager.HasActiveConnection;
    return Results.Ok(new
    {
        status = hasConnection ? "healthy" : "degraded"
    });
});

// Test endpoints for verifying tunnel connectivity without ACME/Caddy
app.MapTunnelTestEndpoints();

// CRITICAL-02: Validate API key BEFORE accepting WebSocket connection
app.MapGet("/tunnel", async (HttpContext context, TunnelWebSocketHandler handler, IOptions<GatewayOptions> options, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
        return;
    }

    // Pre-connection authentication - validate API key header before accepting WebSocket
    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault()
                 ?? context.Request.Query["api_key"].FirstOrDefault();

    if (string.IsNullOrEmpty(apiKey))
    {
        logger.LogWarning("Tunnel connection rejected: missing API key from {RemoteIp}",
            context.Connection.RemoteIpAddress);
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("API key required");
        return;
    }

    // Constant-time comparison to prevent timing attacks
    var expectedKey = Encoding.UTF8.GetBytes(options.Value.ApiKey);
    var providedKey = Encoding.UTF8.GetBytes(apiKey);
    if (!CryptographicOperations.FixedTimeEquals(expectedKey, providedKey))
    {
        logger.LogWarning("Tunnel connection rejected: invalid API key from {RemoteIp}",
            context.Connection.RemoteIpAddress);
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Invalid API key");
        return;
    }

    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    await handler.HandleConnectionAsync(webSocket, ct);
});

app.Run();
