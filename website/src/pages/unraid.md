---
layout: ../layouts/ContentLayout.astro
title: Unraid Installation
description: How to install Octoporty Agent on Unraid using the Docker template or command line.
---

This guide covers installing Octoporty Agent on Unraid. The Agent runs inside your Unraid server and connects to your Gateway to expose services running on your NAS.

## Prerequisites

- Unraid 6.9 or newer
- Docker enabled (Settings > Docker > Enable Docker: Yes)
- A running Octoporty Gateway on a public server
- The API key from your Gateway installation

---

## Method 1: Docker Template (Recommended)

Install Octoporty using the Unraid Docker UI.

### Step 1: Add Container

1. Go to the **Docker** tab
2. Click **Add Container**
3. Toggle **Advanced View** in the top right

### Step 2: Basic Configuration

| Field | Value |
|-------|-------|
| **Name** | `octoporty-agent` |
| **Repository** | `ghcr.io/aduggleby/octoporty-agent:latest` |
| **Network Type** | `bridge` (or `host` if accessing host services) |

### Step 3: Add Port Mapping

Click **Add another Path, Port, Variable, Label or Device**

| Config Type | Value |
|-------------|-------|
| **Type** | Port |
| **Container Port** | `17201` |
| **Host Port** | `17201` |
| **Connection Type** | TCP |

### Step 4: Add Environment Variables

Click **Add another Path, Port, Variable, Label or Device** for each:

| Name | Key | Value |
|------|-----|-------|
| Gateway URL | `Agent__GatewayUrl` | `wss://gateway.yourdomain.com/tunnel` |
| API Key | `Agent__ApiKey` | Your Gateway API key |
| JWT Secret | `Agent__JwtSecret` | Generate: `openssl rand -base64 32` |
| Password Hash | `Agent__Auth__PasswordHash` | SHA-512 crypt hash (generate with `openssl passwd -6 "password"`) |

### Step 5: Add App Data Path

Click **Add another Path, Port, Variable, Label or Device**

| Config Type | Value |
|-------------|-------|
| **Type** | Path |
| **Container Path** | `/app/data` |
| **Host Path** | `/mnt/user/appdata/octoporty` |

### Step 6: Apply

1. Click **Apply**
2. The container will download and start

### Step 7: Access Web UI

Access the Octoporty web UI at:

```
http://your-unraid-ip:17201
```

---

## Method 2: Docker Compose

For users who prefer docker-compose management.

### Step 1: Create App Directory

Using the Unraid terminal or SSH:

```bash
mkdir -p /mnt/user/appdata/octoporty/data
```

### Step 2: Create Compose File

Create `/mnt/user/appdata/octoporty/docker-compose.yml`:

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
      - /mnt/user/appdata/octoporty/data:/app/data
    restart: unless-stopped
    extra_hosts:
      - "host.docker.internal:host-gateway"
```

### Step 3: Start Container

```bash
cd /mnt/user/appdata/octoporty
docker compose up -d
```

---

## Method 3: Community Applications (Optional)

If Octoporty is available in Community Applications:

1. Go to the **Apps** tab
2. Search for `octoporty`
3. Click **Install**
4. Fill in the configuration fields
5. Click **Apply**

---

## Updating

### Via Docker UI

1. Go to **Docker** tab
2. Click the Octoporty container icon
3. Click **Check for Updates**
4. If available, click **Update**

### Via Command Line

```bash
docker pull ghcr.io/aduggleby/octoporty-agent:latest
docker stop octoporty-agent
docker rm octoporty-agent
# Re-run your docker run command or docker compose up -d
```

### Via Docker Compose

```bash
cd /mnt/user/appdata/octoporty
docker compose pull
docker compose down
docker compose up -d
```

---

## Troubleshooting

### Container won't start

1. Check the container logs: Docker tab > click container > Logs
2. Verify all environment variables are set correctly
3. Ensure the appdata path exists and is writable

### Can't connect to Gateway

1. Verify the Gateway URL uses `wss://` protocol
2. Check the API key matches your Gateway exactly
3. Test connectivity: `ping gateway.yourdomain.com`

### Can't access Unraid services

1. Try using `host` network mode instead of `bridge`
2. Or use `host.docker.internal` as the hostname for services
3. Verify the target service port is accessible

### Permission issues

```bash
chown -R nobody:users /mnt/user/appdata/octoporty
chmod -R 755 /mnt/user/appdata/octoporty
```
