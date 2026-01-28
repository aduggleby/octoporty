#!/bin/bash
# update-agent.sh
# Octoporty Agent update script
# Version: 0.9.7
# Usage: curl -fsSL https://octoporty.com/update-agent.sh | bash

set -e

INSTALL_DIR="/opt/octoporty/agent"
SCRIPT_VERSION="0.9.7"

cat << 'EOF'

   ██████╗  ██████╗████████╗ ██████╗ ██████╗  ██████╗ ██████╗ ████████╗██╗   ██╗
  ██╔═══██╗██╔════╝╚══██╔══╝██╔═══██╗██╔══██╗██╔═══██╗██╔══██╗╚══██╔══╝╚██╗ ██╔╝
  ██║   ██║██║        ██║   ██║   ██║██████╔╝██║   ██║██████╔╝   ██║    ╚████╔╝
  ██║   ██║██║        ██║   ██║   ██║██╔═══╝ ██║   ██║██╔══██╗   ██║     ╚██╔╝
  ╚██████╔╝╚██████╗   ██║   ╚██████╔╝██║     ╚██████╔╝██║  ██║   ██║      ██║
   ╚═════╝  ╚═════╝   ╚═╝    ╚═════╝ ╚═╝      ╚═════╝ ╚═╝  ╚═╝   ╚═╝      ╚═╝

          ════════════════════════════════════════════════════
               Self-hosted reverse proxy tunneling solution
          ════════════════════════════════════════════════════

EOF

echo "  Agent Update Script v$SCRIPT_VERSION"
echo "  https://octoporty.com"
echo ""

# Check if installation exists
if [ ! -d "$INSTALL_DIR" ]; then
    echo "Error: Agent installation not found at $INSTALL_DIR"
    echo "Run the install script first: curl -fsSL https://octoporty.com/install-agent.sh | bash"
    exit 1
fi

cd "$INSTALL_DIR"

# Check for docker-compose.yml
if [ ! -f "docker-compose.yml" ]; then
    echo "Error: docker-compose.yml not found in $INSTALL_DIR"
    exit 1
fi

echo "Updating Octoporty Agent..."
echo ""

# Get current image digest
CURRENT_DIGEST=$(docker inspect ghcr.io/aduggleby/octoporty-agent:latest --format='{{.Id}}' 2>/dev/null || echo "none")

# Pull latest images
echo "Pulling latest images..."
docker compose pull

# Get new image digest
NEW_DIGEST=$(docker inspect ghcr.io/aduggleby/octoporty-agent:latest --format='{{.Id}}' 2>/dev/null || echo "none")

if [ "$CURRENT_DIGEST" = "$NEW_DIGEST" ]; then
    echo ""
    echo "Already running the latest version."
    exit 0
fi

echo ""
echo "New version available. Restarting services..."

# Restart services
docker compose down
docker compose up -d

# Wait for services to start
echo ""
echo "Waiting for services to start..."
sleep 5

# Check health
if docker compose ps | grep -q "running"; then
    echo ""
    echo "╔═══════════════════════════════════════════════════════════════╗"
    echo "║           Update Complete!                                    ║"
    echo "╚═══════════════════════════════════════════════════════════════╝"
    echo ""
    docker compose ps
    echo ""
    echo "Web UI available at: http://localhost:17201"
else
    echo ""
    echo "Warning: Services may not have started correctly."
    echo "Check logs with: cd $INSTALL_DIR && docker compose logs"
fi
