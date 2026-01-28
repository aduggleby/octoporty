#!/bin/bash
# update-gateway.sh
# Octoporty Gateway update script
# Version: 0.9.17
# Usage: curl -fsSL https://octoporty.com/update-gateway.sh | bash

set -e

INSTALL_DIR="/opt/octoporty/gateway"
SCRIPT_VERSION="0.9.17"
IMAGE="ghcr.io/aduggleby/octoporty-gateway:latest"

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

echo "  Gateway Update Script v$SCRIPT_VERSION"
echo "  https://octoporty.com"
echo ""

# Check if installation exists
if [ ! -d "$INSTALL_DIR" ]; then
    echo "Error: Gateway installation not found at $INSTALL_DIR"
    echo "Run the install script first: curl -fsSL https://octoporty.com/install-gateway.sh | bash"
    exit 1
fi

cd "$INSTALL_DIR"

# Check for docker-compose.yml
if [ ! -f "docker-compose.yml" ]; then
    echo "Error: docker-compose.yml not found in $INSTALL_DIR"
    exit 1
fi

echo "Checking for updates..."
echo ""

# Get current local image info
# Try to get version from label, fall back to image ID
CURRENT_VERSION=$(docker inspect "$IMAGE" --format='{{index .Config.Labels "org.opencontainers.image.version"}}' 2>/dev/null || echo "")
if [ -z "$CURRENT_VERSION" ] || [ "$CURRENT_VERSION" = "<no value>" ]; then
    CURRENT_VERSION="unknown"
fi
CURRENT_DIGEST=$(docker inspect "$IMAGE" --format='{{.Id}}' 2>/dev/null | cut -c8-19 || echo "not installed")
CURRENT_CREATED=$(docker inspect "$IMAGE" --format='{{.Created}}' 2>/dev/null | cut -c1-19 | tr 'T' ' ' || echo "unknown")

echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║  Current Installation                                         ║"
echo "╠═══════════════════════════════════════════════════════════════╣"
printf "║  Version:     %-47s║\n" "$CURRENT_VERSION"
printf "║  Image ID:    %-47s║\n" "$CURRENT_DIGEST"
printf "║  Built:       %-47s║\n" "$CURRENT_CREATED"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""

# Pull latest image to check for updates
echo "Fetching latest version from registry..."
docker pull "$IMAGE" --quiet > /dev/null 2>&1 || docker pull "$IMAGE"

# Get new image info
NEW_VERSION=$(docker inspect "$IMAGE" --format='{{index .Config.Labels "org.opencontainers.image.version"}}' 2>/dev/null || echo "")
if [ -z "$NEW_VERSION" ] || [ "$NEW_VERSION" = "<no value>" ]; then
    NEW_VERSION="unknown"
fi
NEW_DIGEST=$(docker inspect "$IMAGE" --format='{{.Id}}' 2>/dev/null | cut -c8-19 || echo "unknown")
NEW_CREATED=$(docker inspect "$IMAGE" --format='{{.Created}}' 2>/dev/null | cut -c1-19 | tr 'T' ' ' || echo "unknown")

echo ""
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║  Latest Available                                             ║"
echo "╠═══════════════════════════════════════════════════════════════╣"
printf "║  Version:     %-47s║\n" "$NEW_VERSION"
printf "║  Image ID:    %-47s║\n" "$NEW_DIGEST"
printf "║  Built:       %-47s║\n" "$NEW_CREATED"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""

# Compare digests (more reliable than version for detecting changes)
if [ "$CURRENT_DIGEST" = "$NEW_DIGEST" ]; then
    echo "✓ You are already running the latest version ($CURRENT_VERSION)."
    echo ""
    exit 0
fi

echo "╔═══════════════════════════════════════════════════════════════╗"
if [ "$CURRENT_VERSION" != "unknown" ] && [ "$NEW_VERSION" != "unknown" ]; then
    printf "║  Update Available: %-42s║\n" "$CURRENT_VERSION → $NEW_VERSION"
else
    echo "║  Update Available!                                            ║"
fi
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""
echo "This will restart the Gateway service, causing a brief interruption"
echo "to any active tunnel connections."
echo ""
read -p "Do you want to apply this update? [Y/n] " -n 1 -r < /dev/tty
echo ""

if [[ $REPLY =~ ^[Nn]$ ]]; then
    echo ""
    echo "Update cancelled. The new image has been downloaded but not applied."
    echo "Run this script again when ready to update."
    exit 0
fi

echo ""
echo "Applying update..."

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
    printf "║  Update Complete! Now running %-32s║\n" "v$NEW_VERSION"
    echo "╚═══════════════════════════════════════════════════════════════╝"
    echo ""
    docker compose ps
else
    echo ""
    echo "Warning: Services may not have started correctly."
    echo "Check logs with: cd $INSTALL_DIR && docker compose logs"
fi
