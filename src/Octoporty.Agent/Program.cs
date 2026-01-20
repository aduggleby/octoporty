using System.Text;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Octoporty.Agent.Data;
using Octoporty.Agent.Features.Auth;
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

// CRITICAL-03: Validate JWT secret is at least 32 characters
if (string.IsNullOrWhiteSpace(agentOptions.JwtSecret) || agentOptions.JwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "Agent__JwtSecret must be at least 32 characters. " +
        "Generate one with: openssl rand -hex 32");
}

// CRITICAL-07: Require non-empty password
if (string.IsNullOrWhiteSpace(agentOptions.Auth.Password))
{
    throw new InvalidOperationException(
        "Agent__Auth__Password must be set. " +
        "Configure a strong password for the admin user.");
}

// Database
builder.Services.AddDbContext<OctoportyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication with cookie support (HIGH-01)
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
            // CRITICAL-03: Use full secret, no padding
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(agentOptions.JwtSecret))
        };

        // HIGH-01: Support both cookie and header-based authentication
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // 1. Check Authorization header (default behavior)
                // 2. Check cookie (for browser requests)
                // 3. Check query string (for SignalR)

                if (string.IsNullOrEmpty(context.Token))
                {
                    // Try cookie
                    var cookieToken = context.Request.Cookies["octoporty_access"];
                    if (!string.IsNullOrEmpty(cookieToken))
                    {
                        context.Token = cookieToken;
                        return Task.CompletedTask;
                    }
                }

                // SignalR query string token
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

// MEDIUM-02: Rate limiting for login
builder.Services.AddSingleton<LoginRateLimiter>();

// HIGH-03: Refresh token store
builder.Services.AddSingleton<RefreshTokenStore>();

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

// Health check (unauthenticated) - MEDIUM-03: Minimal info exposure
app.MapGet("/health", (TunnelClient tunnelClient) =>
{
    return Results.Ok(new
    {
        status = tunnelClient.State == TunnelClientState.Connected ? "healthy" : "degraded"
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
