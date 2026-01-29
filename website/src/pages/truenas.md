---
layout: ../layouts/ContentLayout.astro
title: TrueNAS Installation
description: How to install Octoporty Agent on TrueNAS Scale using the web UI or Docker Compose.
---

This guide covers installing Octoporty Agent on TrueNAS Scale. The Agent runs inside your TrueNAS server and connects to your Gateway to expose services running on your NAS.

## Prerequisites

- TrueNAS Scale (Dragonfish or newer)
- A running Octoporty Gateway on a public server
- The API key from your Gateway installation

---

## Method 1: TrueNAS Web UI (Recommended)

Install Octoporty using the TrueNAS Apps interface - no command line required.

### Step 1: Create Storage Dataset

1. Go to **Datasets** in the left sidebar
2. Select your pool and click **Add Dataset**
3. Name it `octoporty`
4. Select the **Apps** preset (this sets the correct permissions for containers)
5. Click **Save**

### Step 2: Install Custom App

1. Go to **Apps** in the left sidebar
2. Click **Discover Apps**
3. Click **Custom App** in the top right corner

### Step 3: Configure Application Name

- **Application Name:** `octoporty`

### Step 4: Configure Container Image

- **Image Repository:** `ghcr.io/aduggleby/octoporty-agent`
- **Image Tag:** `latest`
- **Image Pull Policy:** `Always`

### Step 5: Configure Environment Variables

Click **Add** for each environment variable:

| Name | Value |
|------|-------|
| `Agent__GatewayUrl` | `wss://gateway.yourdomain.com/tunnel` |
| `Agent__ApiKey` | Your Gateway API key (min 32 chars) |
| `Agent__JwtSecret` | Generate with `openssl rand -base64 32` |
| `Agent__Auth__Username` | `admin` |
| `Agent__Auth__Password` | Your secure password |

### Step 6: Configure Networking

1. Scroll to **Networking**
2. Click **Add** under **Ports**
3. Configure the port:
   - **Container Port:** `17201`
   - **Node Port:** `17201`
   - **Protocol:** `TCP`

4. Enable **Host Network** if you need to access services on the TrueNAS host (recommended)

### Step 7: Configure Storage

1. Scroll to **Storage**
2. Click **Add** under **Host Path Volumes**
3. Configure the volume:
   - **Host Path:** `/mnt/your-pool/octoporty` (the dataset you created)
   - **Mount Path:** `/app/data`

### Step 8: Deploy

1. Scroll to the bottom
2. Click **Install**
3. Wait for the app to deploy (check the **Installed** tab)

### Step 9: Access Web UI

Once deployed, access the Octoporty web UI at:

```
http://your-truenas-ip:17201
```

Log in with the username and password you configured.

### Updating via TrueNAS UI

1. Go to **Apps** > **Installed**
2. Find **octoporty** in the list
3. Click the three dots menu (⋮)
4. Click **Update** if an update is available

Or to force a re-pull of the latest image:

1. Click **Edit**
2. Change **Image Pull Policy** to `Always`
3. Click **Save**
4. Stop and Start the app

---

## Method 2: Docker Compose (Alternative)

For users who prefer command-line management.

### Step 1: Create App Directories

SSH into your TrueNAS server or use **System** > **Shell**:

```bash
mkdir -p /mnt/pool/apps/octoporty/data
```

Replace `/mnt/pool` with your actual pool path.

### Step 2: Create Docker Compose File

```bash
nano /mnt/pool/apps/octoporty/docker-compose.yml
```

Add the following:

```yaml
services:
  agent:
    image: ghcr.io/aduggleby/octoporty-agent:latest
    container_name: octoporty-agent
    environment:
      - Agent__GatewayUrl=${AGENT_GATEWAY_URL}
      - Agent__ApiKey=${AGENT_API_KEY}
      - Agent__JwtSecret=${AGENT_JWT_SECRET}
      - Agent__Auth__Username=${AGENT_USERNAME}
      - Agent__Auth__Password=${AGENT_PASSWORD}
    ports:
      - "17201:17201"
    volumes:
      - /mnt/pool/apps/octoporty/data:/app/data
    restart: unless-stopped
    extra_hosts:
      - "host.docker.internal:host-gateway"
```

### Step 3: Create Environment File

```bash
nano /mnt/pool/apps/octoporty/.env
```

Add your settings:

```bash
AGENT_GATEWAY_URL=wss://gateway.yourdomain.com/tunnel
AGENT_API_KEY=your-gateway-api-key-here
AGENT_JWT_SECRET=your-jwt-secret-min-32-chars
AGENT_USERNAME=admin
AGENT_PASSWORD=your-secure-password
```

### Step 4: Start the Agent

```bash
cd /mnt/pool/apps/octoporty
docker compose up -d
```

### Updating via Docker Compose

```bash
cd /mnt/pool/apps/octoporty
docker compose pull
docker compose down
docker compose up -d
```

---

## Troubleshooting

### Agent won't connect to Gateway

1. Verify the Gateway URL uses `wss://` (not `ws://`)
2. Check the API key matches your Gateway exactly
3. Ensure your Gateway is accessible from TrueNAS

View logs in TrueNAS UI:
1. Go to **Apps** > **Installed**
2. Click on **octoporty**
3. Click **Logs**

### Can't access internal services

1. If using Host Network mode, use the TrueNAS IP address
2. If not using Host Network, ensure `host.docker.internal` resolves
3. Verify the target service is running and accessible

### App won't start

1. Check that the storage dataset exists and is accessible
2. Verify all environment variables are set correctly
3. Check for typos in the Gateway URL

### Permission errors

If the app can't write to storage, you need to ensure the container runs as a user that has write access to the data directory.

**Option 1: Set Security Context (Recommended)**

In the TrueNAS app configuration:
1. Go to **Resources and Devices** > **Security Context**
2. Set **User ID** to `568` (the TrueNAS apps user)
3. Ensure your dataset is owned by the apps user

**Option 2: Fix directory ownership**

```bash
chown -R apps:apps /mnt/pool/octoporty
```

Note: The `/app/data` directory is created at runtime and inherits the container's UID, so the simplest solution is to run the container as a user that already has write access to the mounted volume.
