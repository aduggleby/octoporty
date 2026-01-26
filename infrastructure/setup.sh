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
mkdir -p "${OCTOPORTY_DIR}/data"
chmod 755 "${OCTOPORTY_DIR}/data"
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
      - "17200:8080"
    environment:
      - Gateway__ApiKey=${API_KEY}
      - Gateway__CaddyAdminUrl=http://caddy:17202
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
      - "17280:80"
      - "17243:443"
      - "17202:2019"
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
	admin :17202
	email {$ACME_EMAIL:admin@example.com}
}

:17280 {
	respond /health "OK" 200
	respond "No tunnel configured for this host" 503
}

:17243 {
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

# Install auto-updater script and systemd units
echo "Installing Gateway auto-updater..."

# Create auto-updater script
cat > "${OCTOPORTY_DIR}/octoporty-updater.sh" << 'UPDATER_EOF'
#!/bin/bash
# octoporty-updater.sh
# Host watcher script that checks for Gateway update signal files.

set -euo pipefail

SIGNAL_FILE="${OCTOPORTY_SIGNAL_FILE:-/opt/octoporty/data/update-signal}"
COMPOSE_DIR="${OCTOPORTY_COMPOSE_DIR:-/opt/octoporty}"
LOG_TAG="octoporty-updater"

log() {
    logger -t "$LOG_TAG" "$1"
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

# Check if signal file exists
if [ ! -f "$SIGNAL_FILE" ]; then
    exit 0
fi

log "Update signal file found at $SIGNAL_FILE"

# Read signal file contents
if command -v jq &> /dev/null; then
    TARGET_VERSION=$(jq -r '.targetVersion // "latest"' "$SIGNAL_FILE" 2>/dev/null || echo "latest")
    CURRENT_VERSION=$(jq -r '.currentVersion // "unknown"' "$SIGNAL_FILE" 2>/dev/null || echo "unknown")
    REQUESTED_BY=$(jq -r '.requestedBy // "unknown"' "$SIGNAL_FILE" 2>/dev/null || echo "unknown")
else
    TARGET_VERSION="latest"
    CURRENT_VERSION="unknown"
    REQUESTED_BY="unknown"
fi

log "Update requested: $CURRENT_VERSION -> $TARGET_VERSION (by $REQUESTED_BY)"

# Remove signal file immediately
rm -f "$SIGNAL_FILE"
log "Signal file removed"

# Change to compose directory
cd "$COMPOSE_DIR"

# Pull and restart gateway
log "Pulling Gateway image..."
docker compose pull gateway 2>&1 | while read line; do log "  $line"; done

log "Restarting Gateway container..."
docker compose up -d gateway 2>&1 | while read line; do log "  $line"; done

log "Update process completed"
UPDATER_EOF

chmod +x "${OCTOPORTY_DIR}/octoporty-updater.sh"

# Create systemd service for auto-updater
cat > /etc/systemd/system/octoporty-updater.service << EOF
[Unit]
Description=Octoporty Gateway Auto-Updater
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
ExecStart=${OCTOPORTY_DIR}/octoporty-updater.sh
Environment=OCTOPORTY_SIGNAL_FILE=${OCTOPORTY_DIR}/data/update-signal
Environment=OCTOPORTY_COMPOSE_DIR=${OCTOPORTY_DIR}
StandardOutput=journal
StandardError=journal
SyslogIdentifier=octoporty-updater

[Install]
WantedBy=multi-user.target
EOF

# Create systemd timer for auto-updater
cat > /etc/systemd/system/octoporty-updater.timer << EOF
[Unit]
Description=Octoporty Gateway Auto-Updater Timer
After=docker.service

[Timer]
OnBootSec=30s
OnUnitActiveSec=30s
RandomizedDelaySec=5s
Unit=octoporty-updater.service
Persistent=true

[Install]
WantedBy=timers.target
EOF

# Enable and start the auto-updater timer
systemctl daemon-reload
systemctl enable octoporty-updater.timer
systemctl start octoporty-updater.timer
echo "Auto-updater timer started"

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
if curl -sf http://localhost:17200/health > /dev/null 2>&1; then
    echo ""
    echo "=== Setup Complete ==="
    echo ""
    echo "Octoporty Gateway is running!"
    echo ""
    echo "Gateway URL: ws://$(curl -s ifconfig.me):17200/tunnel"
    echo "API Key: Check .env file in ${OCTOPORTY_DIR}"
    echo ""
    echo "Useful commands:"
    echo "  View logs:      journalctl -u octoporty -f"
    echo "  Restart:        systemctl restart octoporty"
    echo "  Stop:           systemctl stop octoporty"
    echo "  Update:         cd ${OCTOPORTY_DIR} && docker compose pull && systemctl restart octoporty"
    echo ""
    echo "Auto-updater:"
    echo "  View timer:     systemctl status octoporty-updater.timer"
    echo "  View logs:      journalctl -u octoporty-updater -f"
    echo "  Manual trigger: systemctl start octoporty-updater"
    echo ""
else
    echo ""
    echo "Warning: Gateway health check failed. Check logs with: journalctl -u octoporty -f"
fi
