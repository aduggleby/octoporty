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

## 0.9.39

**2026-01-29**

- Update branding colors from red to blue
- Refresh screenshots throughout the application

---

## 0.9.38

**2026-01-29**

- Add error boundary for improved crash recovery in the web UI
- Improve Gateway log error handling

---

## 0.9.37

**2026-01-29**

- Simplify login page button text

---

## 0.9.36

**2026-01-29**

- Add logo branding across the application
- **Gateway Logs** - View real-time Gateway logs in the Agent web UI. Logs are streamed via the tunnel and include historical retrieval with infinite scroll (up to 10,000 entries)
- Improve mappings page responsiveness and layout
- Fix connection status display for all states (Authenticating, Syncing)

---

## 0.9.35

**2026-01-29**

- Remove light theme, keep dark mode only
- Internal improvements

---

## 0.9.34

**2026-01-29**

- Display gateway uptime in the dashboard
- Simplify port mapping by removing external port configuration
- Fix SignalR infinite reconnection loop in web UI

---

## 0.9.33

**2026-01-29**

- Improve Docker container compatibility for platforms requiring custom user IDs

---

## 0.9.32

**2026-01-29**

- Version bump for release consistency

---

## 0.9.31

**2026-01-29**

- Improve reliability of container user ID detection in startup banner

---

## 0.9.30

**2026-01-29**

- Display container user and group IDs in startup banner for easier debugging of permission issues

---

## 0.9.29

**2026-01-29**

- **Improved Permission Handling** - The `/app/data` directory is no longer pre-created in the Docker image. It's now created at runtime, inheriting the container's UID. Error messages now provide clearer platform-specific guidance:
  - TrueNAS SCALE: Set Security Context User ID to 568 (apps user)
  - Docker Compose: Use `user: "1000:1000"` or `chown` the bind mount

---

## 0.9.28

**2026-01-29**

- **Data Directory Validation** - The Agent now validates data directory permissions at startup with clear, actionable error messages. Includes platform-specific guidance for Docker bind mounts, TrueNAS SCALE, and other NAS platforms.

---

## 0.9.27

**2026-01-29**

- Fix data directory ownership in Docker containers for correct database permissions

---

## 0.9.26

**2026-01-29**

- **Database Migration to SQLite** - Replaced SQL Server with SQLite for simpler setup and lower resource usage. No external database required. Data is stored in `/app/data/octoporty.db` with automatic volume mounting in Docker deployments. Existing SQL Server installations should migrate data before upgrading.

---

## 0.9.25

**2026-01-29**

- Version bump for release consistency

---

## 0.9.24

**2026-01-29**

- Auto-apply database migrations on Agent startup - fresh Docker installations now create the database automatically without manual migration steps

---

## 0.9.23

**2026-01-28**

- Update install and update script versions

---

## 0.9.22

**2026-01-28**

- Version bump for release consistency

---

## 0.9.21

**2026-01-28**

- Add support for ARM64 architecture (e.g., Raspberry Pi, Apple Silicon)

---

## 0.9.20

**2026-01-28**

- Internal improvements

---

## 0.9.19

**2026-01-28**

- Fix container file permissions for non-root deployments

---

## 0.9.18

**2026-01-28**

- Fix file watcher issues in read-only Docker containers

---

## 0.9.17

**2026-01-28**

- Improve version display in Docker containers

---

## 0.9.16

**2026-01-28**

- Improve version display in Docker containers

---

## 0.9.15

**2026-01-28**

- Add startup banner showing configuration details when services start

---

## 0.9.14

**2026-01-28**

- Fix container compatibility for read-only environments

---

## 0.9.13

**2026-01-28**

- Internal improvements

---

## 0.9.12

**2026-01-28**

- Update documentation for NAS platforms (QNAP and Unraid)

---

## 0.9.11

**2026-01-28**

- Add new build system configuration options for improved developer workflow

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

- Docker deployment with multi-arch images (amd64/arm64)
- GitHub Container Registry for image distribution
- Caddy reverse proxy integration
- SQLite database for Agent configuration storage
