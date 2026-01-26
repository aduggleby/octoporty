#!/bin/bash
# octoporty-updater.sh
# Host watcher script that checks for Gateway update signal files.
# When a signal file is found, pulls the new Gateway image and restarts the container.
# Designed to be run by systemd timer every 30 seconds.

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
    REQUESTED_AT=$(jq -r '.requestedAt // "unknown"' "$SIGNAL_FILE" 2>/dev/null || echo "unknown")
else
    log "Warning: jq not installed, using default values"
    TARGET_VERSION="latest"
    CURRENT_VERSION="unknown"
    REQUESTED_BY="unknown"
    REQUESTED_AT="unknown"
fi

log "Update requested: $CURRENT_VERSION -> $TARGET_VERSION (by $REQUESTED_BY at $REQUESTED_AT)"

# Remove signal file immediately to prevent re-processing
rm -f "$SIGNAL_FILE"
log "Signal file removed"

# Change to compose directory
if [ ! -d "$COMPOSE_DIR" ]; then
    log "ERROR: Compose directory not found: $COMPOSE_DIR"
    exit 1
fi

cd "$COMPOSE_DIR"

# Verify docker-compose.yml exists
if [ ! -f "docker-compose.yml" ]; then
    log "ERROR: docker-compose.yml not found in $COMPOSE_DIR"
    exit 1
fi

# Pull the new gateway image
log "Pulling Gateway image..."
if docker compose pull gateway 2>&1 | while read line; do log "  $line"; done; then
    log "Gateway image pulled successfully"
else
    log "ERROR: Failed to pull Gateway image"
    exit 1
fi

# Restart the gateway service
log "Restarting Gateway container..."
if docker compose up -d gateway 2>&1 | while read line; do log "  $line"; done; then
    log "Gateway container restarted successfully"
else
    log "ERROR: Failed to restart Gateway container"
    exit 1
fi

# Verify gateway is healthy
log "Waiting for Gateway to become healthy..."
HEALTH_CHECK_RETRIES=30
HEALTH_CHECK_INTERVAL=2

for i in $(seq 1 $HEALTH_CHECK_RETRIES); do
    if docker compose exec -T gateway curl -sf http://localhost:8080/health > /dev/null 2>&1; then
        log "Gateway is healthy"
        break
    fi

    if [ $i -eq $HEALTH_CHECK_RETRIES ]; then
        log "WARNING: Gateway health check failed after $((HEALTH_CHECK_RETRIES * HEALTH_CHECK_INTERVAL)) seconds"
        log "Container may still be starting. Check logs with: docker compose logs gateway"
    else
        sleep $HEALTH_CHECK_INTERVAL
    fi
done

log "Update process completed"
