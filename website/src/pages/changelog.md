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

## 1.1.0

**2026-01-21**

**New Feature: Gateway Self-Update**

You can now update the Gateway directly from the Agent UI when a version mismatch is detected.

- Update Agent first, then trigger Gateway update from the UI
- Yellow notification banner appears when Gateway is outdated
- Click "Update Gateway" button to trigger remote update
- Host watcher automatically pulls and restarts Gateway within 30 seconds
- Configurable via `Gateway__AllowRemoteUpdate` environment variable

**Technical Details:**

- New tunnel protocol messages: `UpdateRequest` and `UpdateResponse`
- Signal file mechanism for safe container updates
- Systemd timer for host-level update watcher
- Full E2E test coverage for the new feature

---

## 1.0.0

**2026-01-21**

Initial public release of Octoporty.

**Core Features:**

- **WebSocket Tunnel** - Efficient binary protocol with MessagePack serialization and Lz4 compression
- **Automatic HTTPS** - Caddy integration provides automatic TLS certificates via Let's Encrypt
- **Web Management UI** - React-based dashboard for managing port mappings
- **Multi-Domain Support** - Route multiple external domains to different internal services
- **Automatic Reconnection** - Agent maintains persistent connection with exponential backoff

**Security:**

- Pre-shared API key authentication between Agent and Gateway
- JWT authentication for web UI with HttpOnly cookies
- Rate limiting with exponential backoff lockout
- Constant-time comparison for security-sensitive operations

**Infrastructure:**

- Docker deployment with chiseled images
- GitHub Container Registry for image distribution
- Caddy reverse proxy integration
- SQL Server database for configuration storage
