#!/bin/bash
# install-updater.sh
# Installs the Octoporty Gateway auto-updater on existing installations.
# Run with: curl -fsSL https://octoporty.com/install-updater.sh | sudo bash

set -euo pipefail

OCTOPORTY_DIR="${OCTOPORTY_DIR:-/opt/octoporty}"

echo "=== Installing Octoporty Gateway Auto-Updater ==="

# Check if running as root
if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root (use sudo)"
   exit 1
fi

# Verify Octoporty directory exists
if [[ ! -d "$OCTOPORTY_DIR" ]]; then
    echo "Error: Octoporty directory not found at $OCTOPORTY_DIR"
    echo "Is Octoporty Gateway installed?"
    exit 1
fi

# Create data directory if it doesn't exist
mkdir -p "${OCTOPORTY_DIR}/data"
chmod 755 "${OCTOPORTY_DIR}/data"

# Install jq if not present
if ! command -v jq &> /dev/null; then
    echo "Installing jq..."
    apt-get update && apt-get install -y jq
fi

# Create auto-updater script
echo "Creating auto-updater script..."
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
echo "Creating systemd service..."
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
echo "Creating systemd timer..."
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
echo "Enabling and starting timer..."
systemctl daemon-reload
systemctl enable octoporty-updater.timer
systemctl start octoporty-updater.timer

echo ""
echo "=== Auto-Updater Installed ==="
echo ""
echo "The Gateway will now automatically update when you click 'Update Gateway' in the Agent UI."
echo ""
echo "Useful commands:"
echo "  View timer status:  systemctl status octoporty-updater.timer"
echo "  View update logs:   journalctl -u octoporty-updater -f"
echo "  Manual trigger:     systemctl start octoporty-updater"
echo ""
