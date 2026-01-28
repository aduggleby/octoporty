---
layout: ../layouts/ContentLayout.astro
title: Changelog
description: Release history and changelog for Octoporty.
---

## Installation

Install Octoporty using the quick install scripts:

```bash
# Install Gateway (on cloud server)
curl -fsSL https://octoporty.com/install-gateway.sh | bash

# Install Agent (on private network server)
curl -fsSL https://octoporty.com/install-agent.sh | bash
```

## Updating

Update to the latest version:

```bash
# Update Gateway
curl -fsSL https://octoporty.com/update-gateway.sh | bash

# Update Agent
curl -fsSL https://octoporty.com/update-agent.sh | bash
```

---

## 0.9.0

**2026-01-28**

Initial release of Octoporty - a self-hosted reverse proxy tunneling solution.

**Core Features:**

- **WebSocket Tunnel** - Efficient binary protocol with MessagePack serialization and Lz4 compression
- **Automatic HTTPS** - Caddy integration provides automatic TLS certificates via Let's Encrypt
- **Web Management UI** - React-based dashboard for managing port mappings
- **Multi-Domain Support** - Route multiple external domains to different internal services
- **Automatic Reconnection** - Agent maintains persistent connection with exponential backoff
- **Gateway Self-Update** - Update Gateway remotely from the Agent UI when version mismatch is detected

**Security:**

- Pre-shared API key authentication between Agent and Gateway
- JWT authentication for web UI with HttpOnly cookies
- Rate limiting with exponential backoff lockout
- Constant-time comparison for security-sensitive operations

**Infrastructure:**

- Docker deployment with chiseled images
- GitHub Container Registry for image distribution
- Caddy reverse proxy integration
- SQLite database for Agent configuration storage
