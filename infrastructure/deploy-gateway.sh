#!/bin/bash
# deploy-gateway.sh
# Deploys Octoporty Gateway + Caddy to a remote server.
# Usage: ./deploy-gateway.sh <server-ip> [api-key]
#
# Prerequisites:
#   - SSH access to server with key authentication
#   - Docker and Docker Compose installed on server

set -euo pipefail

SERVER_IP="${1:?Usage: ./deploy-gateway.sh <server-ip> [api-key]}"
API_KEY="${2:-$(openssl rand -hex 32)}"

SSH_USER="root"
SSH_OPTS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null"
REMOTE_DIR="/opt/octoporty"

echo "=== Octoporty Gateway Deployment ==="
echo "Server: ${SERVER_IP}"
echo "API Key: ${API_KEY}"
echo ""

# Check SSH connectivity
echo "[1/6] Testing SSH connection..."
if ! ssh $SSH_OPTS "${SSH_USER}@${SERVER_IP}" "echo 'SSH OK'" 2>/dev/null; then
    echo "ERROR: Cannot connect to server via SSH"
    exit 1
fi

# Check Docker and Docker Compose installation
echo "[2/6] Checking Docker and Docker Compose..."
DOCKER_STATUS=$(ssh $SSH_OPTS "${SSH_USER}@${SERVER_IP}" << 'REMOTE'
MISSING=""
if ! command -v docker &> /dev/null; then
    MISSING="docker"
fi
if ! docker compose version &> /dev/null 2>&1; then
    if [ -n "$MISSING" ]; then
        MISSING="$MISSING docker-compose"
    else
        MISSING="docker-compose"
    fi
fi
echo "$MISSING"
REMOTE
)

if [ -n "$DOCKER_STATUS" ]; then
    echo "Missing on server: $DOCKER_STATUS"
    read -p "Do you want to install Docker and Docker Compose? [y/N] " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "ERROR: Docker and Docker Compose are required to deploy."
        echo "Please install them manually and re-run this script."
        exit 1
    fi

    echo "Installing Docker..."
    ssh $SSH_OPTS "${SSH_USER}@${SERVER_IP}" << 'REMOTE'
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker
REMOTE
fi

# Verify installation
echo "Verifying Docker installation..."
ssh $SSH_OPTS "${SSH_USER}@${SERVER_IP}" << 'REMOTE'
docker --version
docker compose version
REMOTE

# Create deployment directory
echo "[3/6] Creating deployment directory..."
ssh $SSH_OPTS "${SSH_USER}@${SERVER_IP}" "mkdir -p ${REMOTE_DIR} && mkdir -p ${REMOTE_DIR}/data && chmod 755 ${REMOTE_DIR}/data"

# Copy files
echo "[4/6] Copying deployment files..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="${SCRIPT_DIR}/../src"

# Create tar of necessary files
tar -czf /tmp/octoporty-gateway.tar.gz \
    -C "${SRC_DIR}" \
    Octoporty.Gateway \
    Octoporty.Shared

scp $SSH_OPTS /tmp/octoporty-gateway.tar.gz "${SSH_USER}@${SERVER_IP}:${REMOTE_DIR}/"
scp $SSH_OPTS "${SCRIPT_DIR}/docker-compose.gateway.yml" "${SSH_USER}@${SERVER_IP}:${REMOTE_DIR}/"
scp $SSH_OPTS "${SCRIPT_DIR}/Caddyfile" "${SSH_USER}@${SERVER_IP}:${REMOTE_DIR}/"
scp $SSH_OPTS "${SCRIPT_DIR}/octoporty-updater.sh" "${SSH_USER}@${SERVER_IP}:${REMOTE_DIR}/"
scp $SSH_OPTS "${SCRIPT_DIR}/octoporty-updater.service" "${SSH_USER}@${SERVER_IP}:/etc/systemd/system/"
scp $SSH_OPTS "${SCRIPT_DIR}/octoporty-updater.timer" "${SSH_USER}@${SERVER_IP}:/etc/systemd/system/"

# Extract and setup
echo "[5/6] Setting up on server..."
ssh $SSH_OPTS "${SSH_USER}@${SERVER_IP}" << REMOTE
cd ${REMOTE_DIR}
mkdir -p src
tar -xzf octoporty-gateway.tar.gz -C src
rm octoporty-gateway.tar.gz

# Create .env file
cat > .env << ENV
GATEWAY_API_KEY=${API_KEY}
ACME_EMAIL=admin@example.com
ENV

# Rename compose file
mv docker-compose.gateway.yml docker-compose.yml 2>/dev/null || true

# Setup auto-updater
chmod +x octoporty-updater.sh
systemctl daemon-reload
systemctl enable octoporty-updater.timer
systemctl start octoporty-updater.timer
REMOTE

# Build and start
echo "[6/6] Building and starting services..."
ssh $SSH_OPTS "${SSH_USER}@${SERVER_IP}" << REMOTE
cd ${REMOTE_DIR}
docker compose build
docker compose up -d
docker compose ps
REMOTE

echo ""
echo "=== Deployment Complete ==="
echo ""
echo "Gateway is running at: http://${SERVER_IP}:17200"
echo "Caddy is running at: http://${SERVER_IP}:80"
echo ""
echo "Test endpoints:"
echo "  Health check:  curl http://${SERVER_IP}:17200/health"
echo "  Tunnel status: curl http://${SERVER_IP}:17200/test/tunnel"
echo ""
echo "Agent configuration:"
echo "  Agent__GatewayUrl=ws://${SERVER_IP}:17200/tunnel"
echo "  Agent__ApiKey=${API_KEY}"
echo ""
echo "Auto-updater is enabled. View status with:"
echo "  ssh ${SSH_USER}@${SERVER_IP} systemctl status octoporty-updater.timer"
echo ""
