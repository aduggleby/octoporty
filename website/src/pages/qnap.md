---
layout: ../layouts/ContentLayout.astro
title: QNAP Installation
description: How to install Octoporty Agent on QNAP NAS using Container Station or Docker Compose.
---

This guide covers installing Octoporty Agent on QNAP NAS. The Agent runs inside your QNAP and connects to your Gateway to expose services running on your NAS.

## Prerequisites

- QNAP QTS 5.0+ or QuTS hero
- Container Station installed (from App Center)
- A running Octoporty Gateway on a public server
- The API key from your Gateway installation

---

## Method 1: Container Station (Recommended)

Install Octoporty using the QNAP Container Station UI.

### Step 1: Open Container Station

1. Open **Container Station** from the main menu
2. If not installed, get it from **App Center**

### Step 2: Pull the Image

1. Go to **Images**
2. Click **Pull**
3. Enter: `ghcr.io/aduggleby/octoporty-agent:latest`
4. Click **Pull**

### Step 3: Create Container

1. Go to **Containers**
2. Click **Create**
3. Select `ghcr.io/aduggleby/octoporty-agent:latest`
4. Click **Next**

### Step 4: Basic Settings

- **Name:** `octoporty-agent`
- **Auto-start:** Enable

### Step 5: Environment Variables

In the **Environment** section, add:

| Variable | Value |
|----------|-------|
| `Agent__GatewayUrl` | `wss://gateway.yourdomain.com/tunnel` |
| `Agent__ApiKey` | Your Gateway API key (min 32 chars) |
| `Agent__JwtSecret` | Generate with `openssl rand -base64 32` |
| `Agent__Auth__PasswordHash` | SHA-512 crypt hash (generate with `openssl passwd -6 "password"`) |

### Step 6: Network Settings

In the **Network** section:

1. Select **NAT** or **Host** mode
2. Add port mapping:
   - **Host Port:** `17201`
   - **Container Port:** `17201`
   - **Protocol:** TCP

### Step 7: Storage Settings

In the **Shared Folders** section:

1. Click **Add**
2. **Host Path:** `/share/Container/octoporty`
3. **Container Path:** `/app/data`

First, create the folder:
1. Open **File Station**
2. Navigate to the Container shared folder
3. Create folder: `octoporty`

### Step 8: Create Container

1. Review settings
2. Click **Create**
3. The container will start automatically

### Step 9: Access Web UI

Access the Octoporty web UI at:

```
http://your-qnap-ip:17201
```

---

## Method 2: Docker Compose via SSH

For users who prefer command-line management.

### Step 1: Enable SSH

1. Go to **Control Panel** > **Network & File Services** > **Telnet / SSH**
2. Enable SSH

### Step 2: Connect via SSH

```bash
ssh admin@your-qnap-ip
```

### Step 3: Create Directories

```bash
mkdir -p /share/Container/octoporty/data
```

### Step 4: Create Docker Compose File

```bash
nano /share/Container/octoporty/docker-compose.yml
```

Add:

```yaml
services:
  agent:
    image: ghcr.io/aduggleby/octoporty-agent:latest
    container_name: octoporty-agent
    environment:
      - Agent__GatewayUrl=wss://gateway.yourdomain.com/tunnel
      - Agent__ApiKey=your-gateway-api-key
      - Agent__JwtSecret=your-jwt-secret-min-32-chars
      # Generate hash with: openssl passwd -6 "your-password"
      - Agent__Auth__PasswordHash=$6$rounds=5000$yoursalt$yourhash
    ports:
      - "17201:17201"
    volumes:
      - /share/Container/octoporty/data:/app/data
    restart: unless-stopped
    extra_hosts:
      - "host.docker.internal:host-gateway"
```

### Step 5: Start Container

```bash
cd /share/Container/octoporty
docker compose up -d
```

---

## Updating

### Via Container Station

1. Go to **Images**
2. Find `ghcr.io/aduggleby/octoporty-agent`
3. Click **Pull** to get the latest version
4. Go to **Containers**
5. Stop `octoporty-agent`
6. Delete the container
7. Recreate from the updated image

### Via SSH

```bash
cd /share/Container/octoporty
docker compose pull
docker compose down
docker compose up -d
```

---

## Troubleshooting

### Container won't start

1. Check Container Station logs for errors
2. Verify all environment variables are set
3. Ensure the data folder exists and is accessible

### Can't connect to Gateway

1. Verify the Gateway URL uses `wss://` protocol
2. Check the API key matches exactly
3. Ensure outbound connections are allowed

### Can't access QNAP services

1. Use **Host** network mode instead of NAT
2. Or use `host.docker.internal` to reference host services
3. Check QNAP firewall settings

### Permission errors

If the container can't write data:

```bash
chown -R 1000:1000 /share/Container/octoporty/data
```
