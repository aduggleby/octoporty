#!/bin/bash
set -euo pipefail

# Octoporty Hetzner Cloud Setup Script
# This script sets up the Gateway and Caddy on a fresh Hetzner Cloud server
#
# Prerequisites:
# - Fresh Ubuntu 24.04 LTS server
# - SSH access with root privileges
# - Domain pointing to the server's IP address
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/YOUR_ORG/octoporty/main/infrastructure/setup.sh | bash
#   or
#   ./setup.sh

OCTOPORTY_DIR="/opt/octoporty"
COMPOSE_VERSION="2.27.0"

echo "=== Octoporty Gateway Setup ==="
echo ""

# Check if running as root
if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root"
   exit 1
fi

# Update system
echo "Updating system packages..."
apt-get update && apt-get upgrade -y

# Install dependencies
echo "Installing dependencies..."
apt-get install -y \
    ca-certificates \
    curl \
    gnupg \
    lsb-release \
    jq \
    unzip

# Install Docker if not present
if ! command -v docker &> /dev/null; then
    echo "Installing Docker..."
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker
fi

# Install Docker Compose plugin if not present
if ! docker compose version &> /dev/null; then
    echo "Installing Docker Compose..."
    mkdir -p /usr/local/lib/docker/cli-plugins
    curl -SL "https://github.com/docker/compose/releases/download/v${COMPOSE_VERSION}/docker-compose-linux-x86_64" \
        -o /usr/local/lib/docker/cli-plugins/docker-compose
    chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
fi

# Create directory structure
echo "Creating directory structure..."
mkdir -p "${OCTOPORTY_DIR}"
cd "${OCTOPORTY_DIR}"

# Prompt for configuration
echo ""
echo "=== Configuration ==="

if [[ ! -f .env ]]; then
    read -p "Enter your ACME email for Let's Encrypt: " ACME_EMAIL
    read -p "Enter your API key (leave empty to generate): " API_KEY

    if [[ -z "$API_KEY" ]]; then
        API_KEY=$(openssl rand -hex 32)
        echo "Generated API key: ${API_KEY}"
        echo "Save this key - you'll need it for Agent configuration!"
    fi

    # Create .env file
    cat > .env << EOF
# Octoporty Configuration
API_KEY=${API_KEY}
ACME_EMAIL=${ACME_EMAIL}
VERSION=latest

# GitHub Container Registry (update with your org)
GITHUB_REPOSITORY=your-org/octoporty
EOF

    echo ".env file created"
else
    echo "Using existing .env file"
fi

# Download docker-compose.yml
echo "Downloading docker-compose.yml..."
cat > docker-compose.yml << 'COMPOSE_EOF'
services:
  gateway:
    image: ghcr.io/${GITHUB_REPOSITORY:-octoporty}/gateway:${VERSION:-latest}
    ports:
      - "5000:8080"
    environment:
      - Gateway__ApiKey=${API_KEY}
      - Gateway__CaddyAdminUrl=http://caddy:2019
      - Logging__LogLevel=Information
    depends_on:
      - caddy
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s

  caddy:
    image: caddy:2-alpine
    ports:
      - "80:80"
      - "443:443"
      - "2019:2019"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    restart: unless-stopped

volumes:
  caddy_data:
  caddy_config:

networks:
  default:
    name: octoporty
COMPOSE_EOF

# Download Caddyfile
echo "Downloading Caddyfile..."
cat > Caddyfile << 'CADDY_EOF'
{
	admin :2019
	email {$ACME_EMAIL:admin@example.com}
}

:80 {
	respond /health "OK" 200
	respond "No tunnel configured for this host" 503
}

:443 {
	tls {
		on_demand
	}
	respond /health "OK" 200
	respond "No tunnel configured for this host" 503
}
CADDY_EOF

# Create systemd service
echo "Creating systemd service..."
cat > /etc/systemd/system/octoporty.service << EOF
[Unit]
Description=Octoporty Gateway
After=docker.service
Requires=docker.service

[Service]
Type=simple
WorkingDirectory=${OCTOPORTY_DIR}
ExecStart=/usr/bin/docker compose up
ExecStop=/usr/bin/docker compose down
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable octoporty

# Pull images and start
echo ""
echo "Pulling Docker images..."
docker compose pull || echo "Warning: Could not pull images. They will be pulled on first start."

echo ""
echo "Starting Octoporty Gateway..."
systemctl start octoporty

# Wait for services to start
echo "Waiting for services to start..."
sleep 10

# Health check
if curl -sf http://localhost:5000/health > /dev/null 2>&1; then
    echo ""
    echo "=== Setup Complete ==="
    echo ""
    echo "Octoporty Gateway is running!"
    echo ""
    echo "Gateway URL: ws://$(curl -s ifconfig.me):5000/tunnel"
    echo "API Key: Check .env file in ${OCTOPORTY_DIR}"
    echo ""
    echo "Useful commands:"
    echo "  View logs:      journalctl -u octoporty -f"
    echo "  Restart:        systemctl restart octoporty"
    echo "  Stop:           systemctl stop octoporty"
    echo "  Update:         cd ${OCTOPORTY_DIR} && docker compose pull && systemctl restart octoporty"
    echo ""
else
    echo ""
    echo "Warning: Gateway health check failed. Check logs with: journalctl -u octoporty -f"
fi
