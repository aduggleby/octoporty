using System.Text;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Octoporty.Agent.Data;
using Octoporty.Agent.Hubs;
using Octoporty.Agent.Services;
using Octoporty.Shared.Logging;
using Octoporty.Shared.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseOctoportySerilog("Octoporty.Agent");

// Configuration
var agentOptions = builder.Configuration.GetSection("Agent").Get<AgentOptions>() ?? new AgentOptions();
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.Configure<LoggingOptions>(builder.Configuration.GetSection("Logging"));

// Database
builder.Services.AddDbContext<OctoportyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "Octoporty.Agent",
            ValidAudience = "Octoporty.Agent.Web",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(agentOptions.JwtSecret.PadRight(32, '_')))
        };

        // Allow SignalR to get token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// FastEndpoints
builder.Services.AddFastEndpoints();

// SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<StatusNotifier>();

// Tunnel client services
builder.Services.AddSingleton<TunnelClient>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TunnelClient>());
builder.Services.AddScoped<RequestForwarder>();
builder.Services.AddInternalServicesHttpClient();

var app = builder.Build();

// Static files for React SPA
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(c =>
{
    c.Endpoints.Configurator = ep =>
    {
        // All endpoints require auth except those explicitly marked AllowAnonymous
        if (!ep.EndpointType.Namespace?.Contains("Auth") ?? true)
        {
            ep.AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        }
    };
});

// SignalR hub
app.MapHub<StatusHub>("/hub/status");

// Health check (unauthenticated)
app.MapGet("/health", (TunnelClient tunnelClient) =>
{
    return Results.Ok(new
    {
        status = tunnelClient.State == TunnelClientState.Connected ? "healthy" : "degraded",
        tunnelState = tunnelClient.State.ToString(),
        lastConnected = tunnelClient.LastConnectedAt,
        gatewayVersion = tunnelClient.GatewayVersion,
        lastError = tunnelClient.LastError
    });
});

// SPA fallback - serve index.html for client-side routing
app.MapFallbackToFile("index.html");

// Wire up status notifications
var tunnelClient = app.Services.GetRequiredService<TunnelClient>();
var statusNotifier = app.Services.GetRequiredService<StatusNotifier>();
tunnelClient.StateChanged += async state =>
{
    try
    {
        await statusNotifier.NotifyStatusChangeAsync(state);
    }
    catch (Exception ex)
    {
        app.Services.GetRequiredService<ILogger<Program>>()
            .LogError(ex, "Failed to notify status change");
    }
};

app.Run();
