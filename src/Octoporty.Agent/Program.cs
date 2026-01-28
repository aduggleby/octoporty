// Program.cs
// Entry point for the Octoporty Agent service.
// Configures JWT authentication with cookie and header support for SPAs.
// Validates JwtSecret (32+ chars) and Password at startup.
// Serves embedded React SPA from wwwroot with fallback routing.
// Uses CreateSlimBuilder to avoid file watcher issues in read-only containers.

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
using Octoporty.Shared.Startup;

// Use SlimBuilder to avoid default config file loading with reloadOnChange: true
// which fails in read-only containers (chiseled images)
var builder = WebApplication.CreateSlimBuilder(args);

// Manually configure settings that SlimBuilder doesn't include
builder.WebHost.UseKestrelCore();

// Add configuration without file watchers
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

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

// Display startup banner with configuration
StartupBanner.Print("Agent", new Dictionary<string, string?>
{
    ["GatewayUrl"] = agentOptions.GatewayUrl,
    ["ApiKey"] = agentOptions.ApiKey,
    ["JwtSecret"] = agentOptions.JwtSecret,
    ["Username"] = agentOptions.Auth.Username,
    ["Password"] = agentOptions.Auth.Password,
    ["Environment"] = builder.Environment.EnvironmentName
});

// Add routing (required for MapGet, MapHub, etc.)
builder.Services.AddRouting();

// Database (SQLite for lightweight local storage)
builder.Services.AddDbContext<OctoportyDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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

builder.Services.AddAuthorization(options =>
{
    // Default policy requires authenticated user
    options.AddPolicy("Authenticated", policy =>
        policy.RequireAuthenticatedUser());
    options.FallbackPolicy = options.GetPolicy("Authenticated");
});

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
}).AllowAnonymous();

// SPA fallback - serve index.html for client-side routing (SPA handles auth)
app.MapFallbackToFile("index.html").AllowAnonymous();

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
