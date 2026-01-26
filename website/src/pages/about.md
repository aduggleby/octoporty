---
layout: ../layouts/ContentLayout.astro
title: About
description: About Octoporty, its purpose, and design principles.
---

## What is Octoporty?

Octoporty is a self-hosted reverse proxy tunneling solution. It allows you to expose services running on a private network to the public internet through a secure WebSocket tunnel.

Think of it as a self-hosted alternative to ngrok or Cloudflare Tunnels - but you control everything.

## Why Octoporty?

**Privacy First** - Your traffic never passes through third-party servers. You control the Gateway, the Agent, and everything in between.

**No Subscription Fees** - Deploy once on your own infrastructure. No monthly costs for tunneling services.

**Full Customization** - Open source means you can modify, extend, and integrate with your existing systems.

## Architecture

Octoporty consists of two main components:

1. **Gateway** - Deployed on a server with a public IP address (e.g., a cloud VPS). It receives incoming HTTP/HTTPS requests and routes them through the tunnel.

2. **Agent** - Deployed inside your private network. It maintains a persistent WebSocket connection to the Gateway and forwards requests to your internal services.

The tunnel uses WebSocket with MessagePack binary serialization for efficient, compressed data transfer.

## Technology Stack

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
- Vite

**Infrastructure:**
- Docker (chiseled images)
- Caddy (reverse proxy + auto HTTPS)
- GitHub Container Registry

## Links

- [Official Website](https://octoporty.com)
- [GitHub Repository](https://github.com/aduggleby/octoporty)
- [Gateway Container](https://ghcr.io/aduggleby/octoporty-gateway)
- [Agent Container](https://ghcr.io/aduggleby/octoporty-agent)
