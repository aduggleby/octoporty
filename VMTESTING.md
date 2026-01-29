# VM Testing Documentation

This document captures testing procedures and learnings for testing Octoporty on Hetzner VMs.

## Test Environment

**Servers (Hetzner Cloud - "claude" project):**
- Gateway: `claude-octoporty-gateway` - 91.99.233.13 (cax11 ARM)
- Agent: `claude-octoporty-agent` - 46.224.221.66 (cax11 ARM)

**DNS Records (pointing to Gateway):**
- gateway.octoporty.com → 91.99.233.13
- test.octoporty.com → 91.99.233.13
- agent.octoporty.com → 46.224.221.66

## Credentials

Stored in scratchpad during testing session:
- **Gateway API Key**: gyGT3B9frIb3PhkMjDNQq1n6q9MiUn9h
- **Agent Web UI**: admin / UosCIZyCIqtRJInv
- **JWT Secret**: 12vNA9FiBQkuFcij0tWZnJ607oyXJ9KS

## Installation

### Gateway Installation
```bash
# SSH to gateway server, then run:
curl -fsSL https://octoporty.com/install-gateway.sh | bash
```

### Agent Installation
```bash
# SSH to agent server, then run:
curl -fsSL https://octoporty.com/install-agent.sh | bash
```

## Updating After Code Changes

1. Build and release locally:
   ```bash
   ando release
   ```

2. On the Gateway server:
   ```bash
   cd /opt/octoporty/gateway
   curl -fsSL https://octoporty.com/update-gateway.sh | bash
   docker compose logs -f gateway  # Verify new version
   ```

3. On the Agent server:
   ```bash
   cd /opt/octoporty/agent
   curl -fsSL https://octoporty.com/update-agent.sh | bash
   docker compose logs -f agent  # Verify new version
   ```

## Test Services

### Bun Test Service (on Agent server)
Located at `/opt/test-service/index.ts`:
```typescript
const server = Bun.serve({
  port: 3000,
  fetch(request) {
    return new Response("Hello");
  },
});
```

Running as systemd service:
```bash
# Check status
systemctl status test-service

# View logs
journalctl -u test-service -f

# Test locally
curl http://localhost:3000
```

## Testing Tunnel Connectivity

1. Verify Agent is connected to Gateway:
   ```bash
   # On Agent server
   docker compose logs agent | grep -i connected
   ```

2. Test tunnel via external domain:
   ```bash
   curl -v https://test.octoporty.com
   ```

## Common Issues

### crypto.randomUUID not available
**Symptom**: Toasts don't appear, console shows "crypto.randomUUID is not a function"
**Cause**: Some browsers (like Playwright's headless mode) don't support crypto.randomUUID
**Fix**: Added fallback in useToast.tsx to use timestamp + random string

### SSL Certificate Not Ready
**Symptom**: TLS handshake errors when accessing tunnel domain
**Cause**: Caddy hasn't provisioned the SSL certificate yet
**Solution**: Wait for Caddy to automatically provision, or check Caddy logs:
```bash
docker compose logs caddy | grep -i acme
```

### SignalR Connection Errors
**Symptom**: Multiple "Failed to connect" errors in browser console
**Cause**: SignalR WebSocket connection issues, often JWT token expiration
**Solution**: Refresh the page to get a new token

### Agent Disconnects Immediately After Connecting
**Symptom**: Agent logs show state changing to Connected, then immediately to Disconnected
**Cause**: Bug in TunnelClient.cs where the outbound channel was created once in the constructor but marked as completed after each disconnect. On reconnect, SendLoopAsync would exit immediately because the channel was already complete.
**Fix**: Recreate the outbound channel at the start of each connection attempt in ConnectAndRunAsync.
```csharp
// In ConnectAndRunAsync(), before creating WebSocket:
_outboundChannel = CreateOutboundChannel();
```

### 502 Bad Gateway on Tunnel Requests
**Symptom**: Tunnel requests return HTTP 502, Gateway shows no request forwarding logs
**Cause**: CaddyAdminClient was creating routes with `localhost:17200` as the dial target. This doesn't work in Docker because Caddy runs in a separate container.
**Fix**: Changed dial target to `gateway:17200` (Docker service hostname).
```csharp
// In EnsureRouteExistsAsync:
Upstreams = [new CaddyUpstream { Dial = $"gateway:{_options.ListenPort}" }],
```

### Mappings Not Synced After Creation
**Symptom**: New mappings don't work until Agent is restarted
**Cause**: Config sync only happened on initial connection, not when mappings were created/modified
**Fix**: Added ResyncConfigurationAsync calls to CreateMappingEndpoint, UpdateMappingEndpoint, DeleteMappingEndpoint, and ToggleMappingEndpoint

## Browser Testing with Playwright

The Playwright MCP server can be used to interact with the Agent web UI:
```typescript
// Navigate
await mcp__playwright__browser_navigate({ url: 'http://agent-ip:17201' })

// Take screenshot
await mcp__playwright__browser_take_screenshot({ filename: 'test.png', type: 'png' })

// Fill form
await mcp__playwright__browser_fill_form({ fields: [...] })
```

**Note**: Playwright's headless browser may not support all modern JS APIs like `crypto.randomUUID`.

## Port Assignments

- Gateway WebSocket: 17200
- Agent Web UI: 17201
- Caddy Admin API: 17202
- Test Service: 3000
