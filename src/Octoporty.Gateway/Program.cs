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

app.UseWebSockets();
app.UseRequestRouting();

app.MapGet("/health", (ITunnelConnectionManager connectionManager) =>
{
    var hasConnection = connectionManager.HasActiveConnection;
    return Results.Ok(new
    {
        status = hasConnection ? "healthy" : "degraded",
        tunnelConnected = hasConnection
    });
});

app.MapGet("/tunnel", async (HttpContext context, TunnelWebSocketHandler handler, CancellationToken ct) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
        return;
    }

    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    await handler.HandleConnectionAsync(webSocket, ct);
});

app.Run();
