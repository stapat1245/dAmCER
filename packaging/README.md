# DAMX packaging

Builds an apt-installable Debian package for Ubuntu / Debian.

## Build the .deb

```bash
./packaging/build-deb.sh
```

Output: `dist/damx_<version>_amd64.deb` (plus `.changes`/`.buildinfo`).

Requirements: .NET SDK 9, `dpkg-dev`, `debhelper`, `fakeroot`. If `dotnet` is
not on `PATH` but lives in `~/.dotnet9`, the script picks it up automatically.

Note: `dpkg-buildpackage` runs `dh_clean`, which removes generated junk from
the source tree (`__pycache__/`, `*~` backup files). The repo currently tracks
a few such files; they will show up as deleted in `git status` after a build
and can be restored with `git checkout -- <path>` (or dropped for good).

## Install it

```bash
sudo apt install ./dist/damx_1.1.0_amd64.deb
sudo damx-setup --drivers   # build + install the Linuwu-Sense kernel drivers
sudo damx-setup --nitro     # optional: Nitro/PredatorSense button launcher
damx-setup                  # show status
```

The GUI is also available as `damx` / `DAMX` and in the application menu.

Kernel drivers are not part of the package: they must be compiled and signed
against the running kernel, which `damx-setup --drivers` does for you.

## Optional: host your own apt repository

Serve `dist/*.deb` as an apt repository (GitHub Pages, nginx, anything):

```bash
GPG_KEY=<your-key-id> ./packaging/make-apt-repo.sh    # GPG_KEY optional
```

Then users add it once:

```bash
curl -fsSL https://<host>/dists/stable/Release.gpg | \
    sudo tee /usr/share/keyrings/damx.gpg >/dev/null
echo "deb [signed-by=/usr/share/keyrings/damx.gpg] https://<host> stable main" | \
    sudo tee /etc/apt/sources.list.d/damx.list
sudo apt update && sudo apt install damx
```

For an unsigned repository use `[trusted=yes]` instead of `signed-by=...`.

## Package layout

| Path | Content |
| --- | --- |
| `/opt/damx/gui/` | Self-contained Avalonia GUI |
| `/opt/damx/daemon/` | Python daemon (`damx-daemon.service`) |
| `/usr/bin/damx`, `/usr/bin/DAMX` | CLI launchers |
| `/usr/bin/damx-setup` | Driver / Nitro button helper |
| `/usr/lib/damx/nitro-key-detection.sh` | Nitro button monitor |
| `/usr/lib/systemd/system/*.service` | systemd units |
| `/usr/share/applications/damx.desktop` | Desktop entry |

`/opt/damx` matches the layout of the standalone installer on purpose, so both
installation methods can share paths (and migrate cleanly: the package moves
legacy `/etc/systemd/system` units aside in `postinst`).

## Uninstalling

```bash
sudo apt remove damx          # keeps /etc config
sudo apt purge damx           # also removes Nitro button config
```

Kernel modules installed by `damx-setup --drivers` live outside the package;
remove them from the Linuwu-Sense source tree (`sudo make -C /usr/src/damx-linuwu-sense uninstall`)
if needed.
