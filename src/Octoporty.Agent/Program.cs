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
// Default to /app/data/octoporty.db which is the expected volume mount location in Docker.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = "Data Source=/app/data/octoporty.db";
}

// Validate that the data directory exists and is writable before proceeding.
// This catches permission issues early with a clear error message, rather than
// letting EF Core fail with a cryptic "unable to open database file" error.
var dbPath = connectionString
    .Split(';')
    .Select(p => p.Trim())
    .FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
    ?.Substring("Data Source=".Length);

if (!string.IsNullOrEmpty(dbPath))
{
    var dataDir = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dataDir))
    {
        // Try to create the directory if it doesn't exist
        if (!Directory.Exists(dataDir))
        {
            try
            {
                Directory.CreateDirectory(dataDir);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot create data directory '{dataDir}'. " +
                    $"Ensure the volume/bind mount exists and is writable by UID 1654 (the app user in chiseled containers). " +
                    $"For TrueNAS: Set dataset permissions to UID 1654 or use 'Apps' preset. " +
                    $"Error: {ex.Message}");
            }
        }

        // Verify the directory is writable by attempting to create a temp file
        var testFile = Path.Combine(dataDir, $".write-test-{Guid.NewGuid()}");
        try
        {
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Data directory '{dataDir}' is not writable. " +
                $"For TrueNAS SCALE: Set 'User ID' to 568 in Security Context to run as the apps user, " +
                $"and ensure your dataset is owned by the apps user (568:568). " +
                $"For Docker Compose: Either set 'user: \"1000:1000\"' to match your host user, " +
                $"or run 'sudo chown -R 1000:1000 /path/to/data' on the bind mount. " +
                $"Error: {ex.Message}");
        }
    }
}
builder.Services.AddDbContext<OctoportyDbContext>(options =>
    options.UseSqlite(connectionString));

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

// Apply database migrations on startup.
// This ensures the database schema is up-to-date, including creating the database
// if it doesn't exist (required for fresh installations in Docker containers).
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OctoportyDbContext>();
    dbContext.Database.Migrate();
}

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
