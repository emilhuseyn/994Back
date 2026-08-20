#!/usr/bin/env bash
#
# Deploy the Code994 .NET API to this VPS.
#   pulls the latest code → republishes → restarts the systemd service →
#   waits for /health to return 200 (so it never races nginx → 502).
#
# Usage (on the VPS, as root):
#   bash /opt/994Back/deploy.sh
#
# Safe to keep in the repo: the whole body runs inside main(), which bash
# parses into memory before executing — so the mid-run `git pull` can't corrupt
# the running script even if this file itself changed in that same pull.
#
set -euo pipefail

# ---- config (adjust here if paths ever change) ----
REPO_DIR="/opt/994Back"
PUBLISH_DIR="/var/www/code994-api"
SERVICE="code994-api"
PROJECT="src/ECommerce.API/ECommerce.API.csproj"
HEALTH_URL="http://localhost:5080/health"
HEALTH_TIMEOUT=90   # seconds to wait for the app to come up (migrations + seed)

main() {
  cd "$REPO_DIR"

  echo "==> [1/5] git pull"
  git pull --ff-only

  echo "==> [2/5] stopping $SERVICE (release file locks)"
  sudo systemctl stop "$SERVICE"

  echo "==> [3/5] dotnet publish -> $PUBLISH_DIR"
  dotnet publish "$PROJECT" -c Release -o "$PUBLISH_DIR" \
    /p:DebugType=None /p:DebugSymbols=false

  echo "==> [4/5] starting $SERVICE"
  sudo systemctl start "$SERVICE"

  echo "==> [5/5] waiting for health (up to ${HEALTH_TIMEOUT}s)"
  for i in $(seq 1 "$HEALTH_TIMEOUT"); do
    if [ "$(curl -s -o /dev/null -w '%{http_code}' "$HEALTH_URL" || true)" = "200" ]; then
      echo ""
      echo "OK — deploy succeeded, healthy after ${i}s."
      sudo systemctl is-active "$SERVICE"
      exit 0
    fi
    sleep 1
  done

  echo ""
  echo "FAILED — /health did not return 200 within ${HEALTH_TIMEOUT}s. Last logs:"
  sudo journalctl -u "$SERVICE" -n 40 --no-pager
  exit 1
}

main "$@"
