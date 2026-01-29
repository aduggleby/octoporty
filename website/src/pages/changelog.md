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

- Docker deployment with chiseled images
- GitHub Container Registry for image distribution
- Caddy reverse proxy integration
- SQLite database for Agent configuration storage
