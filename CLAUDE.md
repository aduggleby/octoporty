# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build all .NET projects
dotnet build

# Build frontend (outputs to ../Octoporty.Agent/wwwroot)
cd src/Octoporty.Agent.Web && npm run build

# Run Agent locally (includes embedded React SPA)
dotnet run --project src/Octoporty.Agent

# Run Gateway locally
dotnet run --project src/Octoporty.Gateway

# Full dev environment with Docker Compose
docker compose -f infrastructure/docker-compose.dev.yml up --build
```

**Port Assignments (17200-17299 range):**
- Gateway: 17200
- Agent: 17201
- Caddy Admin: 17202
- SQL Server (dev): 17203
- Caddy HTTP: 17280
- Caddy HTTPS: 17243

## Architecture

Octoporty is a self-hosted reverse proxy tunneling solution (like ngrok). It exposes internal services through a public endpoint.

```
Internet → Caddy → Gateway (Hetzner) ←WebSocket→ Agent (private network) → Internal Service
```

**Projects:**
- **Octoporty.Gateway** - Cloud-deployed WebSocket server that receives external traffic and routes to connected Agents
- **Octoporty.Agent** - Runs inside private network, maintains tunnel to Gateway, forwards requests to internal services
- **Octoporty.Agent.Web** - React SPA for managing port mappings (built into Agent's wwwroot)
- **Octoporty.Shared** - Entities, contracts, options, logging extensions

**Tunnel Protocol:**
- WebSocket with MessagePack binary serialization (Lz4 compression)
- Message types in `Octoporty.Shared/Contracts/TunnelMessages.cs`
- Flow: Auth → ConfigSync → Heartbeat loop + Request/Response forwarding

**Key Services:**
- `TunnelClient` (Agent) - WebSocket client with reconnection, state machine: Disconnected→Connecting→Authenticating→Syncing→Connected
- `TunnelConnectionManager` (Gateway) - Manages active Agent connections, routes requests by Host header
- `RequestForwarder` (Agent) - Forwards tunnel requests to internal services via HttpClient
- `CaddyAdminClient` (Gateway) - Manages Caddy routes via Admin API

## Technology Stack

**Backend:** .NET 10, FastEndpoints, EF Core (SQL Server), SignalR, MessagePack, Serilog

**Frontend:** React 19, TypeScript, Tailwind CSS 4, Vite, Motion

**Infrastructure:** Docker (chiseled images), Caddy (reverse proxy + HTTPS), GitHub Container Registry

## Authentication

- **Agent↔Gateway:** Pre-shared API key (min 32 chars), constant-time comparison
- **Web UI:** JWT with HttpOnly cookies, refresh tokens in memory store
- **Rate limiting:** Login endpoint with exponential backoff lockout

## Database

SQL Server with EF Core. Main entities:
- `PortMapping` - ExternalDomain (unique), InternalHost:Port, TLS options
- `ConnectionLog`, `RequestLog` - Audit history

Migrations in `src/Octoporty.Agent/Data/Migrations/`

## Code Style

**File Headers:** Every file must have a header comment summarizing its purpose.

```csharp
// TunnelClient.cs
// Maintains persistent WebSocket connection to the Gateway server.
// Handles authentication, configuration sync, heartbeats, and request forwarding.
// Implements automatic reconnection with exponential backoff.
```

```typescript
// MappingForm.tsx
// Form component for creating and editing port mappings.
// Validates internal host against SSRF patterns before submission.
```

**Header Detection:** Files missing headers can be found with:
```bash
# Find C# files without headers (excluding obj/, Migrations/, Designer files)
find src -name "*.cs" -not -path "*/obj/*" -not -path "*/Migrations/*" -not -name "*.Designer.cs" | \
  xargs grep -L "^// [A-Za-z]"

# Find TypeScript/TSX files without headers
find src -name "*.ts" -o -name "*.tsx" | xargs grep -L "^// [A-Za-z]"
```

**Documentation Requirements:**
- Document all branching logic explaining why each branch exists
- Record architectural decisions as comments when non-obvious choices are made
- Explain the "why" not just the "what" - future readers need context

```csharp
// Use constant-time comparison to prevent timing attacks.
// An attacker could measure response times to guess the API key byte-by-byte.
if (!CryptographicOperations.FixedTimeEquals(expected, provided))
    return false;

// Check rate limiting BEFORE validating credentials.
// This prevents attackers from using timing differences to enumerate valid usernames.
if (_rateLimiter.IsBlocked(clientIp))
{
    // Return 429 with Retry-After header per RFC 6585.
    // Lockout duration increases exponentially: 1min, 5min, 15min, 1hr.
    ...
}
```

## Testing

### E2E Test Requirements

**Test Location:** `tests/Octoporty.Tests.E2E/`

**Run Tests:**
```bash
# Run all E2E tests
cd tests/Octoporty.Tests.E2E && dotnet test

# Run specific test category
dotnet test --filter "FullyQualifiedName~MappingsApi"
dotnet test --filter "FullyQualifiedName~ComprehensiveUi"
```

### UI Testing Rules

**CRITICAL: Every button and interactive element MUST have a corresponding E2E test.**

1. **Complete Coverage Required:**
   - Every button on every page must be tested
   - Every form submission must be tested
   - Every navigation action must be tested
   - Every modal/dialog interaction must be tested

2. **Feedback Requirements:**
   - ALL button actions MUST show visible feedback to the user
   - Use toast notifications to indicate action success/failure
   - Loading states must be shown during async operations
   - Disabled states must prevent double-submission

3. **Test Structure:**
   - Login page: All buttons (submit, forgot password, etc.)
   - Dashboard: All quick action buttons, navigation links
   - Mappings list: Create, filter, search, view toggle, per-row actions
   - Mapping form: All form fields, submit, cancel, delete
   - Modals/dialogs: Confirm, cancel, close buttons
   - Layout: Navigation links, logout button

4. **Toast Notification Standards:**
   - Success actions: Show success toast with description
   - Failed actions: Show error toast with error message
   - In-progress actions: Show info toast or loading state
   - Destructive actions: Require confirmation dialog

### API Testing Rules

**All CRUD operations must have tests:**
- Create: Verify 201 response with created entity
- Read: Verify 200 response with correct data
- Update: Verify 200 response with updated data
- Delete: Verify 204 response and entity removal
- Authentication: Verify 401 for unauthenticated requests

### Test File Structure

```
tests/Octoporty.Tests.E2E/
├── TestBase.cs                 # Base class with login helpers
├── AgentUiTests.cs             # Basic UI smoke tests
├── MappingsApiTests.cs         # CRUD API tests
├── TunnelConnectivityTests.cs  # Tunnel round-trip tests
└── ComprehensiveUiTests.cs     # Complete button coverage tests
```

### Adding New Features

When adding ANY new UI feature:
1. Add corresponding E2E test BEFORE marking feature complete
2. Test must verify button shows feedback (toast/navigation/state change)
3. Test must pass in CI before merging

## Configuration

Environment variables (see `.env.example`):
- `Agent__GatewayUrl` - WebSocket URL to Gateway
- `Agent__ApiKey` / `Gateway__ApiKey` - Shared secret
- `Agent__JwtSecret` - JWT signing (min 32 chars)
- `Agent__Auth__Username/Password` - Web UI credentials
- `Gateway__CaddyAdminUrl` - Caddy Admin API endpoint
