---
layout: ../layouts/ContentLayout.astro
title: Synology DSM Installation
description: How to install Octoporty Agent on Synology NAS using Container Manager or Docker Compose.
---

This guide covers installing Octoporty Agent on Synology DSM 7.0+. The Agent runs inside your Synology NAS and connects to your Gateway to expose services running on your NAS.

## Prerequisites

- Synology DSM 7.0 or newer
- Container Manager package installed (from Package Center)
- A running Octoporty Gateway on a public server
- The API key from your Gateway installation

---

## Method 1: Container Manager (Recommended)

Install Octoporty using the Synology Container Manager UI.

### Step 1: Download the Image

1. Open **Container Manager** from the main menu
2. Go to **Registry**
3. Search for `ghcr.io/aduggleby/octoporty-agent`
4. Select the image and click **Download**
5. Choose the `latest` tag

### Step 2: Create a Folder for Data

1. Open **File Station**
2. Navigate to your preferred shared folder (e.g., `docker`)
3. Create a new folder: `octoporty`

### Step 3: Create the Container

1. Go to **Container Manager** > **Container**
2. Click **Create**
3. Select **ghcr.io/aduggleby/octoporty-agent:latest**
4. Click **Next**

### Step 4: General Settings

- **Container Name:** `octoporty-agent`
- **Enable auto-restart:** Yes

### Step 5: Advanced Settings - Environment

Click **Advanced Settings**, then the **Environment** tab. Add these variables:

| Variable | Value |
|----------|-------|
| `Agent__GatewayUrl` | `wss://gateway.yourdomain.com/tunnel` |
| `Agent__ApiKey` | Your Gateway API key (min 32 chars) |
| `Agent__JwtSecret` | Generate with `openssl rand -base64 32` |
| `Agent__Auth__Username` | `admin` |
| `Agent__Auth__Password` | Your secure password |

### Step 6: Port Settings

In the **Port Settings** tab:

| Local Port | Container Port | Protocol |
|------------|----------------|----------|
| `17201` | `17201` | TCP |

### Step 7: Volume Settings

In the **Volume** tab, click **Add Folder**:

| Folder | Mount Path |
|--------|------------|
| `/docker/octoporty` | `/app/data` |

### Step 8: Network (Optional)

If you need to access services on the Synology host:

1. Go to the **Network** tab
2. Enable **Use the same network as Docker host**

### Step 9: Apply and Run

1. Click **Apply**
2. Review the summary and click **Done**
3. The container will start automatically

### Step 10: Access Web UI

Access the Octoporty web UI at:

```
http://your-synology-ip:17201
```

---

## Method 2: Docker Compose via SSH

For users who prefer command-line management.

### Step 1: Enable SSH

1. Go to **Control Panel** > **Terminal & SNMP**
2. Enable SSH service

### Step 2: Connect via SSH

```bash
ssh admin@your-synology-ip
```

### Step 3: Create Directories

```bash
sudo mkdir -p /volume1/docker/octoporty/data
```

### Step 4: Create Docker Compose File

```bash
sudo nano /volume1/docker/octoporty/docker-compose.yml
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
      - Agent__Auth__Username=admin
      - Agent__Auth__Password=your-secure-password
    ports:
      - "17201:17201"
    volumes:
      - /volume1/docker/octoporty/data:/app/data
    restart: unless-stopped
    network_mode: host  # Optional: for accessing host services
```

### Step 5: Start the Container

```bash
cd /volume1/docker/octoporty
sudo docker compose up -d
```

---

## Updating

### Via Container Manager

1. Go to **Container Manager** > **Registry**
2. Search for `ghcr.io/aduggleby/octoporty-agent`
3. Click **Download** to pull the latest image
4. Go to **Container** > stop `octoporty-agent`
5. Select the container > **Action** > **Reset**
6. Start the container

### Via SSH

```bash
cd /volume1/docker/octoporty
sudo docker compose pull
sudo docker compose down
sudo docker compose up -d
```

---

## Troubleshooting

### Container won't start

1. Check Container Manager logs for errors
2. Verify all environment variables are set
3. Ensure the data folder has correct permissions

### Can't connect to Gateway

1. Verify the Gateway URL uses `wss://` protocol
2. Check the API key matches exactly
3. Ensure your Synology can reach the Gateway (check firewall)

### Permission errors

If the container can't write to the data folder:

```bash
sudo chown -R 1000:1000 /volume1/docker/octoporty/data
```
