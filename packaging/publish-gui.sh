#!/usr/bin/env bash
# Publish the Avalonia GUI as a self-contained linux-x64 build.
# Usage: publish-gui.sh <output-dir>
set -euo pipefail

OUT="${1:?usage: publish-gui.sh <output-dir>}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

DOTNET_BIN="${DOTNET:-}"
if [ -z "$DOTNET_BIN" ]; then
    DOTNET_BIN="$(command -v dotnet || true)"
fi
if [ -z "$DOTNET_BIN" ] && [ -x "$HOME/.dotnet9/dotnet" ]; then
    DOTNET_BIN="$HOME/.dotnet9/dotnet"
fi
if [ -z "$DOTNET_BIN" ]; then
    echo "error: .NET SDK 9 not found. Install it or set DOTNET=/path/to/dotnet" >&2
    exit 1
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

rm -rf "$OUT"
"$DOTNET_BIN" publish "$REPO_ROOT/DivAcerManagerMax/DivAcerManagerMax.csproj" \
    -c Release \
    -f net9.0 \
    -r linux-x64 \
    --self-contained true \
    -o "$OUT"

test -x "$OUT/DivAcerManagerMax" || {
    echo "error: publish did not produce $OUT/DivAcerManagerMax" >&2
    exit 1
}

echo "GUI published to $OUT"
