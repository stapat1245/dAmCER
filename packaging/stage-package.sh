#!/usr/bin/env bash
# Stage the DAMX payload into a package root directory.
# Shared by the debian/rules build and standalone .deb assembly.
# Usage: stage-package.sh --publish-dir <gui-publish-dir> --destdir <root>
set -euo pipefail

PUBLISH_DIR=""
DESTDIR=""

while [ $# -gt 0 ]; do
    case "$1" in
        --publish-dir) PUBLISH_DIR="${2:?}"; shift 2 ;;
        --destdir) DESTDIR="${2:?}"; shift 2 ;;
        *) echo "stage-package.sh: unknown argument: $1" >&2; exit 2 ;;
    esac
done

[ -n "$PUBLISH_DIR" ] && [ -n "$DESTDIR" ] || {
    echo "usage: stage-package.sh --publish-dir <dir> --destdir <root>" >&2
    exit 2
}

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

install -d \
    "$DESTDIR/opt/damx/gui" \
    "$DESTDIR/opt/damx/daemon" \
    "$DESTDIR/usr/bin" \
    "$DESTDIR/usr/lib/damx" \
    "$DESTDIR/usr/lib/systemd/system" \
    "$DESTDIR/usr/share/applications" \
    "$DESTDIR/usr/share/icons/hicolor/500x500/apps" \
    "$DESTDIR/usr/share/pixmaps" \
    "$DESTDIR/usr/share/man/man1"

# ---------------------------------------------------------------- GUI
cp -R "$PUBLISH_DIR"/. "$DESTDIR/opt/damx/gui/"
rm -f "$DESTDIR/opt/damx/gui"/*.pdb

# dotnet publish marks managed libraries executable; only the apphost (and
# createdump) need +x, everything else should be 0644 for lintian/policy.
find "$DESTDIR/opt/damx/gui" -type f \
    \( -name '*.dll' -o -name '*.so' -o -name '*.json' -o -name '*.dat' \) \
    -exec chmod 0644 {} +
chmod 0755 "$DESTDIR/opt/damx/gui/DivAcerManagerMax"
[ -f "$DESTDIR/opt/damx/gui/createdump" ] && chmod 0755 "$DESTDIR/opt/damx/gui/createdump"

# ------------------------------------------------------------- daemon
# Pure python3 (stdlib only) so no PyInstaller binary is needed.
install -m 0755 "$REPO_ROOT"/DAMM-Daemon/DAMX-Daemon.py "$DESTDIR/opt/damx/daemon/"
install -m 0755 "$REPO_ROOT"/DAMM-Daemon/KeyboardMonitor.py "$DESTDIR/opt/damx/daemon/"
install -m 0755 "$REPO_ROOT"/DAMM-Daemon/PowerSourceDetection.py "$DESTDIR/opt/damx/daemon/"

# ------------------------------------------------------- launchers/CLI
install -m 0755 "$REPO_ROOT/packaging/damx-launcher" "$DESTDIR/usr/bin/DAMX"
ln -sf DAMX "$DESTDIR/usr/bin/damx"
install -m 0755 "$REPO_ROOT/packaging/damx-setup" "$DESTDIR/usr/bin/damx-setup"

# Nitro/PredatorSense button helper used by its systemd unit.
install -m 0755 "$REPO_ROOT/scripts/nitro-key-detection.sh" "$DESTDIR/usr/lib/damx/nitro-key-detection.sh"

# ------------------------------------------------------------ systemd
install -m 0644 "$REPO_ROOT/packaging/systemd/damx-daemon.service" "$DESTDIR/usr/lib/systemd/system/"
install -m 0644 "$REPO_ROOT/packaging/systemd/nitro-key-detection.service" "$DESTDIR/usr/lib/systemd/system/"

# ------------------------------------------------- desktop entry/icons
install -m 0644 "$REPO_ROOT/packaging/damx.desktop" "$DESTDIR/usr/share/applications/damx.desktop"
install -m 0644 "$REPO_ROOT/DivAcerManagerMax/icon.png" "$DESTDIR/usr/share/icons/hicolor/500x500/apps/damx.png"
install -m 0644 "$REPO_ROOT/DivAcerManagerMax/icon.png" "$DESTDIR/usr/share/pixmaps/damx.png"

# ------------------------------------------------------------- man pages
install -m 0644 "$REPO_ROOT/packaging/man/damx.1" "$DESTDIR/usr/share/man/man1/damx.1"
install -m 0644 "$REPO_ROOT/packaging/man/damx.1" "$DESTDIR/usr/share/man/man1/DAMX.1"
install -m 0644 "$REPO_ROOT/packaging/man/damx-setup.1" "$DESTDIR/usr/share/man/man1/damx-setup.1"

# Package files must be root-owned; succeeds under fakeroot/dpkg-buildpackage.
chown -R 0:0 "$DESTDIR" 2>/dev/null || true

echo "Staged DAMX payload into $DESTDIR"
