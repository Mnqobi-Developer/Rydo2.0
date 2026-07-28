#!/usr/bin/env bash
set -euo pipefail

MODE="${1:-start}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$ROOT_DIR"

export APP_VARIANT="${APP_VARIANT:-development}"
export EXPO_PUBLIC_APP_ENV="${EXPO_PUBLIC_APP_ENV:-development}"
export EXPO_PUBLIC_API_BASE_URL="${EXPO_PUBLIC_API_BASE_URL:-http://127.0.0.1:5190}"

show_usage() {
  cat <<'USAGE'
usage: ./script/build_and_run.sh [mode]

Modes:
  start, run        Start the RYDO Passenger development client
  --ios, ios        Start Expo and open iOS
  --android, android
                    Start Expo and open Android
  --web, web        Start Expo for web
  --dev-client, dev-client
                    Start Expo in development-client mode
  --tunnel, tunnel  Start Expo using tunnel transport
  --export-web, export-web
                    Export the web build locally
  --doctor, doctor  Run Expo diagnostics
  --help, help      Show this help
USAGE
}

resolve_expo_cmd() {
  if [[ -n "${EXPO_CLI:-}" ]]; then
    # shellcheck disable=SC2206
    EXPO_CMD=(${EXPO_CLI})
  else
    EXPO_CMD=(npx expo)
  fi
}

resolve_expo_cmd

case "$MODE" in
  start|run|--dev-client|dev-client)
    exec "${EXPO_CMD[@]}" start --dev-client
    ;;
  --ios|ios)
    exec "${EXPO_CMD[@]}" start --dev-client --ios
    ;;
  --android|android)
    exec "${EXPO_CMD[@]}" start --dev-client --android
    ;;
  --web|web)
    exec "${EXPO_CMD[@]}" start --web
    ;;
  --tunnel|tunnel)
    exec "${EXPO_CMD[@]}" start --dev-client --tunnel
    ;;
  --export-web|export-web)
    exec "${EXPO_CMD[@]}" export --platform web
    ;;
  --doctor|doctor)
    exec npx expo-doctor
    ;;
  --help|help)
    show_usage
    ;;
  *)
    show_usage >&2
    exit 2
    ;;
esac
