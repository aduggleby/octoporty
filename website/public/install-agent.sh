#!/bin/bash
# install-agent.sh
# Octoporty Agent installation script
# Version: 0.9.44
# Usage: curl -fsSL https://octoporty.com/install-agent.sh | bash

set -e

INSTALL_DIR="/opt/octoporty/agent"
COMPOSE_FILE="docker-compose.yml"
ENV_FILE=".env"
SCRIPT_VERSION="0.9.44"

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

echo "  Agent Installation Script v$SCRIPT_VERSION"
echo "  https://octoporty.com"
echo ""

echo "This script will:"
echo "  • Check and optionally install Docker and Docker Compose"
echo "  • Create installation directory at $INSTALL_DIR"
echo "  • Generate secure JWT secret and admin credentials"
echo "  • Create Docker Compose configuration"
echo "  • Pull the Octoporty Agent Docker image"
echo ""
read -p "Do you want to continue? [Y/n] " -n 1 -r < /dev/tty
echo ""
if [[ $REPLY =~ ^[Nn]$ ]]; then
    echo "Installation cancelled."
    exit 0
fi
echo ""

# Track if user wants to install all dependencies
INSTALL_ALL=false

# Prompt for dependency installation
# Returns 0 if user agrees, 1 if user declines
# Sets INSTALL_ALL=true if user chooses 'a' (always)
prompt_install() {
    local name="$1"

    if [ "$INSTALL_ALL" = true ]; then
        echo "Auto-installing $name..."
        return 0
    fi

    echo "$name is not installed."
    echo ""
    read -p "Would you like to install $name? [Y/n/a] (a=install all) " -n 1 -r < /dev/tty
    echo ""

    if [[ $REPLY =~ ^[Aa]$ ]]; then
        INSTALL_ALL=true
        return 0
    elif [[ $REPLY =~ ^[Nn]$ ]]; then
        return 1
    else
        return 0
    fi
}

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

# Install curl based on distribution
install_curl() {
    local distro=$(detect_distro)
    echo "Installing curl for $distro..."

    case "$distro" in
        ubuntu|debian|linuxmint|pop)
            sudo apt-get update
            sudo apt-get install -y curl
            ;;
        centos|rhel|rocky|almalinux)
            sudo yum install -y curl
            ;;
        fedora)
            sudo dnf install -y curl
            ;;
        arch|manjaro|endeavouros)
            sudo pacman -Sy --noconfirm curl
            ;;
        opensuse*|sles)
            sudo zypper install -y curl
            ;;
        *)
            echo "Unable to install curl automatically for $distro"
            echo "Please install curl manually and run this script again."
            exit 1
            ;;
    esac

    echo "curl installed successfully!"
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
    if prompt_install "curl"; then
        install_curl
    else
        echo "curl is required. Please install curl manually and run this script again."
        exit 1
    fi
fi

# Check for Docker
if ! command -v docker &> /dev/null; then
    if prompt_install "Docker"; then
        install_docker
    else
        echo "Docker is required. Please install Docker manually and run this script again."
        exit 1
    fi
fi

# Check for docker compose
if ! docker compose version &> /dev/null; then
    if prompt_install "Docker Compose plugin"; then
        install_docker
    else
        echo "Docker Compose is required. Please install it manually and run this script again."
        exit 1
    fi
fi

echo "Docker and Docker Compose are installed."
echo ""

# Create installation directory
echo "Creating installation directory: $INSTALL_DIR"
sudo mkdir -p "$INSTALL_DIR"
sudo chown $(id -u):$(id -g) "$INSTALL_DIR"
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
echo "╠═══════════════════════════════════════════════════════════════╣"
echo "║  Full installation docs: octoporty.com/installation           ║"
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
echo "  4. Create port mappings in the web UI to expose your services"
echo "     The Gateway will automatically configure Caddy routes."
echo ""
echo "Documentation: https://octoporty.com"
echo "GitHub: https://github.com/aduggleby/octoporty"
