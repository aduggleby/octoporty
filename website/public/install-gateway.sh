#!/bin/bash
# install-gateway.sh
# Octoporty Gateway installation script
# Version: 0.9.33
# Usage: curl -fsSL https://octoporty.com/install-gateway.sh | bash

set -e

INSTALL_DIR="/opt/octoporty/gateway"
COMPOSE_FILE="docker-compose.yml"
ENV_FILE=".env"
CADDYFILE="Caddyfile"
SCRIPT_VERSION="0.9.33"

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

echo "This script will:"
echo "  • Check and optionally install Docker and Docker Compose"
echo "  • Ask for your gateway domain (for Agent connections)"
echo "  • Create installation directory at $INSTALL_DIR"
echo "  • Generate a secure API key for Agent authentication"
echo "  • Configure Caddy with your gateway domain"
echo "  • Pull the Octoporty Gateway Docker image"
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

# Ask for gateway domain
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║  Gateway Domain Configuration                                 ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""
echo "The Gateway needs a domain name that Agents will connect to."
echo "This domain should point to this server's public IP address."
echo ""
echo "Example: gateway.yourdomain.com"
echo ""
read -p "Enter your gateway domain: " GATEWAY_DOMAIN < /dev/tty
echo ""

if [ -z "$GATEWAY_DOMAIN" ]; then
    echo "Error: Gateway domain is required."
    exit 1
fi

# Create installation directory
echo "Creating installation directory: $INSTALL_DIR"
sudo mkdir -p "$INSTALL_DIR"
sudo chown $(id -u):$(id -g) "$INSTALL_DIR"
cd "$INSTALL_DIR"

# Generate secure API key if not exists, or read existing one
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

    echo "Created $ENV_FILE"
else
    # Read existing API key
    API_KEY=$(grep GATEWAY_API_KEY "$ENV_FILE" | cut -d'=' -f2)
    echo "Using existing API key from $ENV_FILE"
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

# Create Caddyfile with the configured domain
echo "Creating Caddyfile for $GATEWAY_DOMAIN..."
cat > "$CADDYFILE" << EOF
{
    admin :2019
}

# Gateway WebSocket endpoint
# Agents connect to this domain to establish the tunnel
$GATEWAY_DOMAIN {
    reverse_proxy gateway:17200
}

# NOTE: Tunnel routes for your services are configured automatically
# via the Agent web UI. You do NOT need to manually add them here.
# The Gateway updates Caddy dynamically via the Admin API.
EOF
echo "Created $CADDYFILE"

# Pull images
echo ""
echo "Pulling Docker images..."
docker compose pull

echo ""
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║           Installation Complete!                              ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""

# Ask if user wants to start the gateway
read -p "Would you like to start the Gateway now? [Y/n] " -n 1 -r < /dev/tty
echo ""
if [[ ! $REPLY =~ ^[Nn]$ ]]; then
    echo "Starting Gateway..."
    docker compose up -d
    echo ""
    echo "Gateway is running!"
    echo ""
else
    echo "To start the Gateway later, run:"
    echo "  cd $INSTALL_DIR && docker compose up -d"
    echo ""
fi

echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║  Save these details for Agent installation                    ║"
echo "╠═══════════════════════════════════════════════════════════════╣"
echo "║                                                               ║"
echo "║  Gateway Domain:                                              ║"
printf "║    %-56s   ║\n" "$GATEWAY_DOMAIN"
echo "║                                                               ║"
echo "║  API Key:                                                     ║"
printf "║    %-56s   ║\n" "$API_KEY"
echo "║                                                               ║"
echo "╠═══════════════════════════════════════════════════════════════╣"
echo "║  Install Agent on your private network server:                ║"
echo "║                                                               ║"
echo "║    curl -fsSL https://octoporty.com/install-agent.sh | bash   ║"
echo "║                                                               ║"
echo "╠═══════════════════════════════════════════════════════════════╣"
echo "║  Full installation docs: octoporty.com/installation           ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""
echo "Note: Tunnel routes for your services are configured automatically"
echo "via the Agent web UI - no manual Caddy configuration needed."
echo ""
echo "Documentation: https://octoporty.com"
echo "GitHub: https://github.com/aduggleby/octoporty"
