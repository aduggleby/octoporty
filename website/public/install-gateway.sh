#!/bin/bash
# install-gateway.sh
# Octoporty Gateway installation script
# Version: 0.9.0
# Usage: curl -fsSL https://octoporty.com/install-gateway.sh | bash

set -e

INSTALL_DIR="/opt/octoporty/gateway"
COMPOSE_FILE="docker-compose.yml"
ENV_FILE=".env"
CADDYFILE="Caddyfile"
SCRIPT_VERSION="0.9.0"

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

echo "  Gateway Installation Script v$SCRIPT_VERSION"
echo "  https://octoporty.com"
echo ""

# Detect Linux distribution
detect_distro() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        DISTRO=$ID
        DISTRO_FAMILY=$ID_LIKE
    elif [ -f /etc/redhat-release ]; then
        DISTRO="rhel"
    elif [ -f /etc/debian_version ]; then
        DISTRO="debian"
    else
        DISTRO="unknown"
    fi
    echo "$DISTRO"
}

# Install Docker based on distribution
install_docker() {
    local distro=$(detect_distro)
    echo "Detected distribution: $distro"
    echo ""

    case "$distro" in
        ubuntu|debian|linuxmint|pop)
            echo "Installing Docker for Debian/Ubuntu..."
            sudo apt-get update
            sudo apt-get install -y ca-certificates curl gnupg
            sudo install -m 0755 -d /etc/apt/keyrings
            curl -fsSL https://download.docker.com/linux/$distro/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
            sudo chmod a+r /etc/apt/keyrings/docker.gpg
            echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/$distro $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
            sudo apt-get update
            sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
            ;;
        centos|rhel|rocky|almalinux)
            echo "Installing Docker for RHEL/CentOS..."
            sudo yum install -y yum-utils
            sudo yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
            sudo yum install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
            ;;
        fedora)
            echo "Installing Docker for Fedora..."
            sudo dnf -y install dnf-plugins-core
            sudo dnf config-manager --add-repo https://download.docker.com/linux/fedora/docker-ce.repo
            sudo dnf install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
            ;;
        arch|manjaro|endeavouros)
            echo "Installing Docker for Arch Linux..."
            sudo pacman -Sy --noconfirm docker docker-compose
            ;;
        opensuse*|sles)
            echo "Installing Docker for openSUSE..."
            sudo zypper install -y docker docker-compose
            ;;
        *)
            echo "Unsupported distribution: $distro"
            echo "Attempting to use the official Docker install script..."
            curl -fsSL https://get.docker.com | sudo sh
            ;;
    esac

    # Start and enable Docker service
    sudo systemctl start docker
    sudo systemctl enable docker

    # Add current user to docker group
    if [ -n "$SUDO_USER" ]; then
        sudo usermod -aG docker "$SUDO_USER"
        echo ""
        echo "Added $SUDO_USER to the docker group."
        echo "You may need to log out and back in for this to take effect."
    fi

    echo ""
    echo "Docker installed successfully!"
}

# Check for curl
if ! command -v curl &> /dev/null; then
    echo "Error: curl is required but not installed."
    echo "Please install curl first:"
    echo "  Ubuntu/Debian: sudo apt-get install curl"
    echo "  CentOS/RHEL:   sudo yum install curl"
    echo "  Fedora:        sudo dnf install curl"
    echo "  Arch:          sudo pacman -S curl"
    exit 1
fi

# Check for Docker
if ! command -v docker &> /dev/null; then
    echo "Docker is not installed."
    echo ""
    read -p "Would you like to install Docker? [Y/n] " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Nn]$ ]]; then
        echo "Docker is required. Please install Docker manually and run this script again."
        exit 1
    fi
    install_docker
fi

# Check for docker compose
if ! docker compose version &> /dev/null; then
    echo "Docker Compose plugin is not installed."
    echo ""
    read -p "Would you like to install Docker with Compose plugin? [Y/n] " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Nn]$ ]]; then
        echo "Docker Compose is required. Please install it manually and run this script again."
        exit 1
    fi
    install_docker
fi

echo "Docker and Docker Compose are installed."
echo ""

# Create installation directory
echo "Creating installation directory: $INSTALL_DIR"
sudo mkdir -p "$INSTALL_DIR"
sudo chown $(id -u):$(id -g) "$INSTALL_DIR"
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
