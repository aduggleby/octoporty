```
   ██████╗  ██████╗████████╗ ██████╗ ██████╗  ██████╗ ██████╗ ████████╗██╗   ██╗
  ██╔═══██╗██╔════╝╚══██╔══╝██╔═══██╗██╔══██╗██╔═══██╗██╔══██╗╚══██╔══╝╚██╗ ██╔╝
  ██║   ██║██║        ██║   ██║   ██║██████╔╝██║   ██║██████╔╝   ██║    ╚████╔╝
  ██║   ██║██║        ██║   ██║   ██║██╔═══╝ ██║   ██║██╔══██╗   ██║     ╚██╔╝
  ╚██████╔╝╚██████╗   ██║   ╚██████╔╝██║     ╚██████╔╝██║  ██║   ██║      ██║
   ╚═════╝  ╚═════╝   ╚═╝    ╚═════╝ ╚═╝      ╚═════╝ ╚═╝  ╚═╝   ╚═╝      ╚═╝
```

# Octoporty

**Self-hosted reverse proxy tunneling solution**

[![Website](https://img.shields.io/badge/Website-octoporty.com-dc2626)](https://octoporty.com)
[![GitHub](https://img.shields.io/badge/GitHub-aduggleby%2Foctoporty-black?logo=github)](https://github.com/aduggleby/octoporty)
[![License](https://img.shields.io/badge/License-No'Saasy-dc2626)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-dc2626)](https://dotnet.microsoft.com)

Octoporty is a self-hosted alternative to ngrok that lets you expose internal services through a public endpoint. Deploy the Gateway on a cloud server with a public IP, run the Agent inside your private network, and securely tunnel traffic to your internal services.

## Table of Contents

- [Features](#features)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Installation](#installation)
  - [Docker (Recommended)](#docker-recommended)
  - [Manual Installation](#manual-installation)
- [Updating](#updating)
- [Configuration](#configuration)
  - [Gateway Configuration](#gateway-configuration)
  - [Agent Configuration](#agent-configuration)
- [Web Interface](#web-interface)
- [Development](#development)
- [API Reference](#api-reference)
- [Security](#security)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)

## Features

- **Self-Hosted** - Full control over your infrastructure and data
- **WebSocket Tunnel** - Efficient binary protocol with MessagePack serialization and Lz4 compression
- **Automatic HTTPS** - Caddy integration provides automatic TLS certificates via Let's Encrypt
- **Web Management UI** - React-based dashboard for managing port mappings
- **Multi-Domain Support** - Route multiple domains to different internal services
- **Automatic Reconnection** - Agent maintains persistent connection with exponential backoff
- **Gateway Self-Update** - Update the Gateway from the Agent UI when version mismatch is detected
- **Request Logging** - Audit trail for all tunneled requests
- **Rate Limiting** - Built-in protection against brute force attacks
- **Startup Banner** - Visual configuration display at startup with obfuscated secrets for easy verification
- **Automatic Database Setup** - Agent auto-applies migrations on startup, no manual database setup required
- **Docker Ready** - Multi-arch container images (amd64/arm64) with minimal attack surface

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              INTERNET                                        │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         CLOUD / PUBLIC SERVER                               │
│  ┌─────────────┐       ┌─────────────────────┐                              │
│  │    Caddy    │──────▶│  Octoporty Gateway  │                              │
│  │ (HTTPS/TLS) │       │   (WebSocket Hub)   │                              │
│  └─────────────┘       └─────────────────────┘                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                            WebSocket Tunnel
                         (MessagePack + Lz4)
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PRIVATE NETWORK                                   │
│  ┌─────────────────────┐       ┌─────────────────────────────────────────┐  │
│  │   Octoporty Agent   │──────▶│          Internal Services              │  │
│  │  (Tunnel Client +   │       │  ┌─────────┐ ┌─────────┐ ┌─────────┐   │  │
│  │   Web UI @ :17201)  │       │  │ Web App │ │   API   │ │ Database│   │  │
│  └─────────────────────┘       │  │  :3000  │ │  :8080  │ │  :5432  │   │  │
│                                │  └─────────┘ └─────────┘ └─────────┘   │  │
│                                └─────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Components

| Component | Description |
|-----------|-------------|
| **Octoporty.Gateway** | Cloud-deployed WebSocket server that receives external traffic and routes to connected Agents |
| **Octoporty.Agent** | Runs inside private network, maintains tunnel to Gateway, forwards requests to internal services |
| **Octoporty.Agent.Web** | React SPA for managing port mappings (embedded in Agent) |
| **Octoporty.Shared** | Shared entities, contracts, options, and logging extensions |

### Tunnel Protocol

The tunnel uses WebSocket with MessagePack binary serialization and Lz4 compression for efficient data transfer.

**Message Flow:**
1. **Authentication** - Agent connects and authenticates with pre-shared API key
2. **Configuration Sync** - Agent sends its port mapping configuration to Gateway
3. **Heartbeat Loop** - Maintains connection health with periodic pings
4. **Request/Response** - Gateway forwards incoming HTTP requests through the tunnel

## Quick Start

The fastest way to get started is with Docker Compose:

```bash
# Clone the repository
git clone https://github.com/aduggleby/octoporty.git
cd octoporty

# Copy and configure environment
cp .env.example .env
# Edit .env with your settings

# Start the development environment
docker compose -f infrastructure/docker-compose.dev.yml up --build
```

Access the Agent web UI at `http://localhost:17201`

## Installation

### Docker (Recommended)

**Gateway Deployment:**

```yaml
# docker-compose.gateway.yml
services:
  gateway:
    image: ghcr.io/aduggleby/octoporty-gateway:latest
    environment:
      - Gateway__ApiKey=your-secure-api-key-min-32-chars
      - Gateway__CaddyAdminUrl=http://caddy:2019
    ports:
      - "17200:17200"

  caddy:
    image: caddy:2-alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy_data:/data

volumes:
  caddy_data:
```

**Agent Deployment:**

```yaml
# docker-compose.agent.yml
services:
  agent:
    image: ghcr.io/aduggleby/octoporty-agent:latest
    environment:
      - Agent__GatewayUrl=wss://your-gateway-domain.com/tunnel
      - Agent__ApiKey=your-secure-api-key-min-32-chars
      - Agent__JwtSecret=your-jwt-secret-min-32-chars
      - Agent__Auth__Username=admin
      - Agent__Auth__Password=your-secure-password
    ports:
      - "17201:17201"
    volumes:
      - agent_data:/app/data

volumes:
  agent_data:
```

### Manual Installation

**Prerequisites:**
- .NET 10 SDK
- Node.js 20+ (for frontend build)
- SQL Server (for Agent database)

**Build from source:**

```bash
# Clone repository
git clone https://github.com/aduggleby/octoporty.git
cd octoporty

# Build all .NET projects
dotnet build

# Build frontend (outputs to Agent's wwwroot)
cd src/Octoporty.Agent.Web
npm install
npm run build
cd ../..

# Run Gateway
dotnet run --project src/Octoporty.Gateway

# Run Agent (in separate terminal)
dotnet run --project src/Octoporty.Agent
```

## Updating

### Quick Update (Recommended)

Update to the latest version with a single command:

**Gateway:**

```bash
curl -fsSL https://octoporty.com/update-gateway.sh | bash
```

**Agent:**

```bash
curl -fsSL https://octoporty.com/update-agent.sh | bash
```

### Manual Update

If you prefer to update manually:

**Gateway:**

```bash
cd /opt/octoporty/gateway

# Pull latest images
docker compose pull

# Restart services
docker compose down
docker compose up -d

# Verify the update
docker compose logs -f gateway
```

**Agent:**

```bash
cd /opt/octoporty/agent

# Pull latest images
docker compose pull

# Restart services
docker compose down
docker compose up -d

# Verify the update
docker compose logs -f agent
```

### Checking Versions

```bash
# Check Gateway version
docker inspect ghcr.io/aduggleby/octoporty-gateway:latest --format='{{index .Config.Labels "org.opencontainers.image.version"}}'

# Check Agent version
docker inspect ghcr.io/aduggleby/octoporty-agent:latest --format='{{index .Config.Labels "org.opencontainers.image.version"}}'
```

### Gateway Self-Update

When you update the Agent to a newer version, it can detect that the Gateway is running an older version. The Agent UI will display a notification banner with an "Update Gateway" button.

**How it works:**

1. Update the Agent first (using the methods above)
2. When the Agent connects to the Gateway, it compares versions
3. If the Agent is newer, a yellow banner appears in the Agent UI
4. Click "Update Gateway" to trigger a remote update
5. The Gateway writes a signal file that the host watcher monitors
6. Within 30 seconds, the Gateway is automatically pulled and restarted

**Configuration:**

| Variable | Description | Default |
|----------|-------------|---------|
| `Gateway__AllowRemoteUpdate` | Enable/disable remote update requests | `true` |
| `Gateway__UpdateSignalPath` | Path to the update signal file | `/data/update-signal` |

**Security:** Remote updates are only accepted over authenticated WebSocket connections (API key validated). The update signal file is only writable by the Gateway container, and the host watcher runs separately with access to Docker.

## Configuration

### Gateway Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `Gateway__ApiKey` | Pre-shared key for Agent authentication (min 32 chars) | Required |
| `Gateway__CaddyAdminUrl` | Caddy Admin API endpoint | `http://localhost:2019` |
| `Gateway__Port` | Gateway listening port | `17200` |
| `Gateway__AllowRemoteUpdate` | Allow Agents to trigger Gateway self-updates | `true` |
| `Gateway__UpdateSignalPath` | Path for update signal file | `/data/update-signal` |

### Agent Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `Agent__GatewayUrl` | WebSocket URL to Gateway | Required |
| `Agent__ApiKey` | Pre-shared key matching Gateway | Required |
| `Agent__JwtSecret` | JWT signing key (min 32 chars) | Required |
| `Agent__Auth__Username` | Web UI login username | Required |
| `Agent__Auth__Password` | Web UI login password | Required |
| `Agent__Port` | Agent web UI port | `17201` |
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | SQLite default |

### Port Assignments

Octoporty uses port range 17200-17299:

| Port | Service |
|------|---------|
| 17200 | Gateway |
| 17201 | Agent Web UI |
| 17202 | Caddy Admin API |
| 17203 | SQL Server (dev) |
| 17280 | Caddy HTTP |
| 17243 | Caddy HTTPS |

## Web Interface

The Agent includes an embedded React web application for managing port mappings.

### Features

- **Dashboard** - Overview of tunnel status and active mappings
- **Port Mappings** - Create, edit, and delete domain-to-service mappings
- **Connection Logs** - View connection history and status
- **Request Logs** - Audit trail of all tunneled requests

### Creating a Port Mapping

1. Navigate to **Mappings** in the sidebar
2. Click **Create Mapping**
3. Enter the external domain (e.g., `app.yourdomain.com`)
4. Enter the internal host and port (e.g., `localhost:3000`)
5. Configure TLS options if needed
6. Click **Save**

The Gateway will automatically configure Caddy to route traffic for the domain through the tunnel.

## Development

### Project Structure

```
octoporty/
├── src/
│   ├── Octoporty.Gateway/      # Gateway service
│   ├── Octoporty.Agent/        # Agent service + API
│   ├── Octoporty.Agent.Web/    # React frontend
│   └── Octoporty.Shared/       # Shared library
├── tests/
│   └── Octoporty.Tests.E2E/    # End-to-end tests
├── infrastructure/
│   ├── docker-compose.yml      # Production compose
│   ├── docker-compose.dev.yml  # Development compose
│   └── Caddyfile               # Caddy configuration
└── CLAUDE.md                   # AI assistant instructions
```

### Running Tests

```bash
# Run all E2E tests
cd tests/Octoporty.Tests.E2E
dotnet test

# Run specific test category
dotnet test --filter "FullyQualifiedName~MappingsApi"
dotnet test --filter "FullyQualifiedName~ComprehensiveUi"
```

### Technology Stack

**Backend:**
- .NET 10
- FastEndpoints (API framework)
- Entity Framework Core (SQL Server)
- SignalR (WebSocket management)
- MessagePack (binary serialization)
- Serilog (structured logging)

**Frontend:**
- React 19
- TypeScript
- Tailwind CSS 4
- Vite (build tool)
- Motion (animations)

**Infrastructure:**
- Docker (chiseled images)
- Caddy (reverse proxy + auto HTTPS)
- GitHub Container Registry

## API Reference

### Port Mappings API

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/mappings` | List all port mappings |
| GET | `/api/mappings/{id}` | Get mapping by ID |
| POST | `/api/mappings` | Create new mapping |
| PUT | `/api/mappings/{id}` | Update mapping |
| DELETE | `/api/mappings/{id}` | Delete mapping |

### Authentication

The Agent Web UI uses JWT authentication with HttpOnly cookies. Login via:

```
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "your-password"
}
```

## Security

### Authentication

- **Agent-Gateway:** Pre-shared API key with constant-time comparison to prevent timing attacks
- **Web UI:** JWT with HttpOnly cookies, refresh tokens stored in memory
- **Rate Limiting:** Login endpoint with exponential backoff lockout (1min, 5min, 15min, 1hr)

### Network Security

- All external traffic should go through Caddy with TLS
- WebSocket tunnel uses secure WebSocket (WSS) in production
- Internal services are never directly exposed to the internet

### Best Practices

1. Use strong, unique API keys (minimum 32 characters)
2. Enable TLS for all external connections
3. Regularly rotate credentials
4. Monitor request logs for suspicious activity
5. Keep all components updated

## Troubleshooting

### Agent won't connect to Gateway

1. Verify `Agent__GatewayUrl` is correct and reachable
2. Check that API keys match on both sides
3. Ensure Gateway is running and healthy
4. Check firewall rules allow WebSocket connections

### Requests not reaching internal service

1. Verify port mapping configuration
2. Check internal service is running and accessible from Agent
3. Review Agent logs for forwarding errors
4. Ensure internal host is resolvable from Agent container

### Web UI login fails

1. Verify username and password are set correctly
2. Check for rate limiting lockout
3. Ensure JWT secret is configured
4. Clear browser cookies and try again

### Startup Banner

When the Gateway or Agent starts, it displays a startup banner with the current configuration. This helps verify that environment variables are loaded correctly. Sensitive values (API keys, passwords, secrets) are obfuscated, showing only the first 2 and last 2 characters.

Example Agent output:
```
   ██████╗  ██████╗████████╗ ██████╗ ██████╗  ██████╗ ██████╗ ████████╗██╗   ██╗
  ██╔═══██╗██╔════╝╚══██╔══╝██╔═══██╗██╔══██╗██╔═══██╗██╔══██╗╚══██╔══╝╚██╗ ██╔╝
  ██║   ██║██║        ██║   ██║   ██║██████╔╝██║   ██║██████╔╝   ██║    ╚████╔╝
  ██║   ██║██║        ██║   ██║   ██║██╔═══╝ ██║   ██║██╔══██╗   ██║     ╚██╔╝
  ╚██████╔╝╚██████╗   ██║   ╚██████╔╝██║     ╚██████╔╝██║  ██║   ██║      ██║
   ╚═════╝  ╚═════╝   ╚═╝    ╚═════╝ ╚═╝      ╚═════╝ ╚═╝  ╚═╝   ╚═╝      ╚═╝

  Agent v0.9.20
  ─────────────────────────────────────────────────────────────────────────
  GatewayUrl    : wss://gateway.example.com/tunnel
  ApiKey        : my****ey
  JwtSecret     : se****et
  Username      : admin
  Password      : pa****rd
  Environment   : Production
```

### Logs

```bash
# View Gateway logs
docker logs octoporty-gateway

# View Agent logs
docker logs octoporty-agent

# View Caddy logs
docker logs caddy
```

## Contributing

Contributions are welcome! Please read the following guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Follow the code style guidelines in `CLAUDE.md`
4. Add tests for new functionality
5. Ensure all tests pass
6. Commit your changes (`git commit -m 'Add amazing feature'`)
7. Push to the branch (`git push origin feature/amazing-feature`)
8. Open a Pull Request

### Code Style

- Every file must have a header comment explaining its purpose
- Document all branching logic with explanations
- Use constant-time comparisons for security-sensitive operations
- Follow existing patterns in the codebase

## License

This project is licensed under the **No'Saasy License** (based on the [O'Saasy License Agreement](https://osaasy.dev)).

This means you can freely use, modify, and distribute the software, but you cannot offer it as a commercial SaaS product to third parties. Self-hosting for your own use is always permitted.

See [LICENSE](LICENSE) for the full license text.

---

## Links

- **Official Website:** [https://octoporty.com](https://octoporty.com)
- **GitHub Repository:** [https://github.com/aduggleby/octoporty](https://github.com/aduggleby/octoporty)
- **Container Registry:** [ghcr.io/aduggleby/octoporty-gateway](https://ghcr.io/aduggleby/octoporty-gateway), [ghcr.io/aduggleby/octoporty-agent](https://ghcr.io/aduggleby/octoporty-agent)
- **License:** [O'Saasy License](https://osaasy.dev)

---

Made with care by [Alex Duggleby](https://github.com/aduggleby)
