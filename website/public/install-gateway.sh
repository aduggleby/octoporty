#!/bin/bash
# install-gateway.sh
# Octoporty Gateway installation script
# Usage: curl -fsSL https://octoporty.com/install-gateway.sh | bash

set -e

INSTALL_DIR="/opt/octoporty/gateway"
COMPOSE_FILE="docker-compose.yml"
ENV_FILE=".env"
CADDYFILE="Caddyfile"

echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║           Octoporty Gateway Installation                      ║"
echo "║           https://octoporty.com                               ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""

# Check for required commands
for cmd in docker curl; do
    if ! command -v $cmd &> /dev/null; then
        echo "Error: $cmd is required but not installed."
        exit 1
    fi
done

# Check for docker compose
if ! docker compose version &> /dev/null; then
    echo "Error: docker compose is required but not installed."
    exit 1
fi

# Create installation directory
echo "Creating installation directory: $INSTALL_DIR"
sudo mkdir -p "$INSTALL_DIR"
cd "$INSTALL_DIR"

# Generate secure API key if not exists
if [ ! -f "$ENV_FILE" ]; then
    echo "Generating secure API key..."
    API_KEY=$(openssl rand -base64 32 | tr -d '/+=' | head -c 32)

    cat > "$ENV_FILE" << EOF
# Octoporty Gateway Configuration
# Generated on $(date)

# API Key for Agent authentication (min 32 chars)
GATEWAY_API_KEY=$API_KEY

# Caddy Admin API URL (internal)
CADDY_ADMIN_URL=http://caddy:2019
EOF

    echo "Created $ENV_FILE with generated API key"
    echo ""
    echo "IMPORTANT: Save this API key - you'll need it for the Agent installation:"
    echo "  API_KEY=$API_KEY"
    echo ""
fi

# Create docker-compose.yml
echo "Creating Docker Compose configuration..."
cat > "$COMPOSE_FILE" << 'EOF'
services:
  gateway:
    image: ghcr.io/aduggleby/octoporty-gateway:latest
    container_name: octoporty-gateway
    environment:
      - Gateway__ApiKey=${GATEWAY_API_KEY}
      - Gateway__CaddyAdminUrl=${CADDY_ADMIN_URL:-http://caddy:2019}
    ports:
      - "17200:17200"
    restart: unless-stopped
    depends_on:
      - caddy

  caddy:
    image: caddy:2-alpine
    container_name: octoporty-caddy
    ports:
      - "80:80"
      - "443:443"
      - "17202:2019"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    restart: unless-stopped

volumes:
  caddy_data:
  caddy_config:
EOF

# Create Caddyfile if not exists
if [ ! -f "$CADDYFILE" ]; then
    echo "Creating Caddyfile..."
    cat > "$CADDYFILE" << 'EOF'
{
    admin :2019
}

# Add your domain configurations here
# Example:
# gateway.yourdomain.com {
#     reverse_proxy gateway:17200
# }

# Wildcard for tunneled services
# *.tunnel.yourdomain.com {
#     reverse_proxy gateway:17200
# }
EOF
    echo "Created $CADDYFILE - edit this file to configure your domains"
fi

# Pull images
echo ""
echo "Pulling Docker images..."
docker compose pull

echo ""
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║           Installation Complete!                              ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""
echo "Next steps:"
echo "  1. Edit $INSTALL_DIR/$CADDYFILE to configure your domains"
echo "  2. Edit $INSTALL_DIR/$ENV_FILE if needed"
echo "  3. Start the Gateway:"
echo "     cd $INSTALL_DIR && docker compose up -d"
echo ""
echo "  4. Install the Agent on your private network server"
echo "     using the same API key from $ENV_FILE"
echo ""
echo "Documentation: https://octoporty.com"
echo "GitHub: https://github.com/aduggleby/octoporty"
