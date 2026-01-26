#!/bin/bash
# install-agent.sh
# Octoporty Agent installation script
# Usage: curl -fsSL https://octoporty.com/install-agent.sh | bash

set -e

INSTALL_DIR="/opt/octoporty/agent"
COMPOSE_FILE="docker-compose.yml"
ENV_FILE=".env"

echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║           Octoporty Agent Installation                        ║"
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

# Generate secure secrets if .env not exists
if [ ! -f "$ENV_FILE" ]; then
    echo "Generating secure secrets..."
    JWT_SECRET=$(openssl rand -base64 32 | tr -d '/+=' | head -c 32)
    ADMIN_PASSWORD=$(openssl rand -base64 16 | tr -d '/+=' | head -c 16)

    cat > "$ENV_FILE" << EOF
# Octoporty Agent Configuration
# Generated on $(date)

# Gateway WebSocket URL (use wss:// for production)
AGENT_GATEWAY_URL=wss://gateway.yourdomain.com/tunnel

# API Key - must match the Gateway's API key
AGENT_API_KEY=REPLACE_WITH_GATEWAY_API_KEY

# JWT Secret for web UI authentication (min 32 chars)
AGENT_JWT_SECRET=$JWT_SECRET

# Web UI credentials
AGENT_USERNAME=admin
AGENT_PASSWORD=$ADMIN_PASSWORD
EOF

    echo "Created $ENV_FILE"
    echo ""
    echo "IMPORTANT: You must edit $ENV_FILE to set:"
    echo "  - AGENT_GATEWAY_URL: Your Gateway's WebSocket URL"
    echo "  - AGENT_API_KEY: The API key from your Gateway installation"
    echo ""
    echo "Generated credentials for web UI:"
    echo "  Username: admin"
    echo "  Password: $ADMIN_PASSWORD"
    echo ""
fi

# Create docker-compose.yml
echo "Creating Docker Compose configuration..."
cat > "$COMPOSE_FILE" << 'EOF'
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
      - agent_data:/app/data
    restart: unless-stopped
    # Uncomment to access host network services
    # extra_hosts:
    #   - "host.docker.internal:host-gateway"

volumes:
  agent_data:
EOF

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
echo "  1. Edit $INSTALL_DIR/$ENV_FILE to configure:"
echo "     - AGENT_GATEWAY_URL (your Gateway's WebSocket URL)"
echo "     - AGENT_API_KEY (must match Gateway's API key)"
echo ""
echo "  2. Start the Agent:"
echo "     cd $INSTALL_DIR && docker compose up -d"
echo ""
echo "  3. Access the web UI at http://localhost:17201"
echo "     (or replace localhost with this server's IP)"
echo ""
echo "Documentation: https://octoporty.com"
echo "GitHub: https://github.com/aduggleby/octoporty"
