---
layout: ../layouts/ContentLayout.astro
title: Installation Guide
description: Installation guides for Octoporty Gateway and Agent on various platforms.
---

Octoporty consists of two components:

- **Gateway** - Runs on a public server (cloud VPS) with a domain name
- **Agent** - Runs on your private network to expose internal services

## Quick Install (Linux)

The fastest way to get started on Linux servers. The install scripts are interactive and will:
- Check and optionally install Docker and Docker Compose
- Prompt for required configuration (domain, API key, etc.)
- Generate secure credentials automatically
- Create all necessary configuration files

**Gateway (on your public server):**

```bash
curl -fsSL https://octoporty.com/install-gateway.sh | bash
```

The Gateway installer will ask for your gateway domain (e.g., `gateway.yourdomain.com`) and automatically configure Caddy with HTTPS. At the end, it displays the API key you'll need for the Agent installation.

**Agent (on your private network):**

```bash
curl -fsSL https://octoporty.com/install-agent.sh | bash
```

The Agent installer will prompt for the Gateway URL and API key (provided by the Gateway installer).

---

## Platform-Specific Guides

### NAS Systems

| Platform | Guide |
|----------|-------|
| **TrueNAS Scale** | [TrueNAS Installation Guide](/truenas) |

### Coming Soon

- Synology DSM
- Unraid
- QNAP

---

## Manual Installation

For platforms not listed above, you can install manually using Docker Compose.

### Gateway

```yaml
# docker-compose.yml
services:
  gateway:
    image: ghcr.io/aduggleby/octoporty-gateway:latest
    environment:
      - Gateway__ApiKey=your-api-key-min-32-chars
      - Gateway__CaddyAdminUrl=http://caddy:2019
    ports:
      - "17200:17200"
    restart: unless-stopped

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

### Agent

```yaml
# docker-compose.yml
services:
  agent:
    image: ghcr.io/aduggleby/octoporty-agent:latest
    environment:
      - Agent__GatewayUrl=wss://gateway.yourdomain.com/tunnel
      - Agent__ApiKey=your-api-key-min-32-chars
      - Agent__JwtSecret=your-jwt-secret-min-32-chars
      - Agent__Auth__Username=admin
      - Agent__Auth__Password=your-password
    ports:
      - "17201:17201"
    volumes:
      - agent_data:/app/data
    restart: unless-stopped

volumes:
  agent_data:
```

---

## Need Help?

- [Documentation](/)
- [GitHub Issues](https://github.com/aduggleby/octoporty/issues)
- [Changelog](/changelog)
