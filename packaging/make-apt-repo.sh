#!/usr/bin/env bash
# Build a static, optionally signed apt repository from dist/*.deb.
# Point a web server (or GitHub Pages) at dist/apt and users can do:
#
#   sudo install -d /usr/share/keyrings
#   curl -fsSL https://<host>/apt/dists/stable/Release.gpg | \
#       sudo tee /usr/share/keyrings/damx.gpg >/dev/null
#   echo "deb [signed-by=/usr/share/keyrings/damx.gpg] https://<host>/apt stable main" | \
#       sudo tee /etc/apt/sources.list.d/damx.list
#   sudo apt update && sudo apt install damx
#
# Without GPG_KEY the repository is unsigned; use it with [trusted=yes].
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

SUITE="${SUITE:-stable}"
COMPONENT="${COMPONENT:-main}"
OUT="$REPO_ROOT/dist/apt"

command -v dpkg-scanpackages >/dev/null 2>&1 || {
    echo "error: dpkg-scanpackages not found (sudo apt install dpkg-dev)" >&2
    exit 1
}

shopt -s nullglob
DEBS=(dist/*.deb)
[ "${#DEBS[@]}" -gt 0 ] || { echo "error: no .deb files in dist/ (run packaging/build-deb.sh first)" >&2; exit 1; }

rm -rf "$OUT"
mkdir -p "$OUT/pool/$COMPONENT" "$OUT/dists/$SUITE/$COMPONENT/binary-amd64"
cp "${DEBS[@]}" "$OUT/pool/$COMPONENT/"

( cd "$OUT" && dpkg-scanpackages --multiversion "pool/$COMPONENT" \
    > "dists/$SUITE/$COMPONENT/binary-amd64/Packages" )
gzip -9kf "$OUT/dists/$SUITE/$COMPONENT/binary-amd64/Packages"

REL="$OUT/dists/$SUITE/Release"
{
    echo "Origin: DAMX"
    echo "Label: DAMX"
    echo "Suite: $SUITE"
    echo "Codename: $SUITE"
    echo "Architectures: amd64"
    echo "Components: $COMPONENT"
    echo "Description: Div Acer Manager Max apt repository"
    echo "Date: $(date -Ru)"
    echo "SHA256:"
    for f in "$OUT/dists/$SUITE/$COMPONENT/binary-amd64/Packages" \
             "$OUT/dists/$SUITE/$COMPONENT/binary-amd64/Packages.gz"; do
        printf ' %s %s %s\n' \
            "$(sha256sum "$f" | cut -d' ' -f1)" \
            "$(stat -c%s "$f")" \
            "${f#"$OUT/dists/$SUITE/"}"
    done
} > "$REL"

if [ -n "${GPG_KEY:-}" ]; then
    gpg --batch --yes --default-key "$GPG_KEY" --armor --detach-sign -o "$REL.gpg" "$REL"
    gpg --batch --yes --default-key "$GPG_KEY" --clearsign -o "$OUT/dists/$SUITE/InRelease" "$REL"
    echo "Repository signed with key $GPG_KEY"
else
    echo "Repository is UNSIGNED (set GPG_KEY to sign). Use [trusted=yes] when adding it."
fi

echo "apt repository ready: $OUT"
