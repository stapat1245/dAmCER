#!/usr/bin/env bash
# Build an apt-installable Debian package.
#
#   ./packaging/build-deb.sh
#
# Output: dist/damx_<version>_amd64.deb (+ .changes/.buildinfo)
# Requires: dotnet SDK 9, dpkg-dev, debhelper, fakeroot.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Prefer a locally installed SDK (e.g. ~/.dotnet9) when dotnet is not on PATH.
if ! command -v dotnet >/dev/null 2>&1 && [ -x "$HOME/.dotnet9/dotnet" ]; then
    export PATH="$HOME/.dotnet9:$PATH"
fi

command -v dotnet >/dev/null 2>&1 || {
    echo "error: .NET SDK 9 required (https://learn.microsoft.com/dotnet/core/install/linux)" >&2
    exit 1
}

for tool in dpkg-buildpackage dpkg-deb; do
    command -v "$tool" >/dev/null 2>&1 || {
        echo "error: $tool missing. Install: sudo apt install dpkg-dev debhelper fakeroot" >&2
        exit 1
    }
done

# -d: the .NET SDK is expected on PATH (or in ~/.dotnet9) rather than as a
# distro package, so Build-Depends only lists debhelper.
dpkg-buildpackage -b -us -uc -d

mkdir -p dist
for artifact in ../damx_*.deb ../damx_*.buildinfo ../damx_*.changes; do
    [ -e "$artifact" ] && mv -f "$artifact" dist/
done

echo
echo "Built packages:"
ls -1 dist/ | grep -E '\.(deb|changes)$' || true

DEB_FILE="$(ls -1 dist/*.deb | head -n1)"
echo
echo "Install with:  sudo apt install ./${DEB_FILE}"
