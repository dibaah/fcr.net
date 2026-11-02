#!/usr/bin/env bash
# Phase 3 deployment: publish FCR.Web, install to /opt/fcr-web, restart the
# systemd unit. Run from the repo root (or set SRC). Requires: dotnet SDK on the
# build host, sudo on the target.
set -euo pipefail

SRC="${SRC:-$(cd "$(dirname "$0")/../.." && pwd)}"
INSTALL_DIR="${INSTALL_DIR:-/opt/fcr-web}"
DATA_DIR="${DATA_DIR:-/var/lib/fcr/data}"
SERVICE="${SERVICE:-fcr-web}"

echo "==> Publishing FCR.Web (framework-dependent, linux-x64)"
(cd "$SRC/dotnet" && dotnet publish src/FCR.Web -c Release -o "$SRC/dotnet/out/fcr-web")

echo "==> Safety backup of the live database"
if [ -f "$DATA_DIR/fcr_database.db" ]; then
    mkdir -p "$DATA_DIR/backups"
    cp "$DATA_DIR/fcr_database.db" \
       "$DATA_DIR/backups/fcr_database_predeploy_$(date +%Y%m%d_%H%M%S).db"
fi

echo "==> Installing to $INSTALL_DIR (previous build kept at $INSTALL_DIR.prev)"
sudo mkdir -p "$INSTALL_DIR"
if [ -d "$INSTALL_DIR" ] && [ -f "$INSTALL_DIR/FCR.Web" ]; then
    sudo rm -rf "$INSTALL_DIR.prev"
    sudo cp -a "$INSTALL_DIR" "$INSTALL_DIR.prev"
fi
sudo cp -a "$SRC/dotnet/out/fcr-web/." "$INSTALL_DIR/"

echo "==> Installing systemd unit + data dirs"
sudo mkdir -p "$DATA_DIR" /var/lib/fcr/backups /var/lib/fcr/attachments
sudo cp "$SRC/dotnet/deploy/fcr-web.service" /etc/systemd/system/fcr-web.service
sudo systemctl daemon-reload
sudo systemctl enable "$SERVICE"
sudo systemctl restart "$SERVICE"

echo "==> Health check"
sleep 3
curl -fsS http://localhost:8080/health/ready && echo " OK" \
    || { echo " FAILED — check: journalctl -u $SERVICE -n 50"; exit 1; }

echo "Deployed. Rollback: docs/deployment.md § Rollback"
