# Div Acer Manager Max

Linux control utility for Acer laptops, built on the
[Linuwu Sense](https://github.com/0x7375646F/Linuwu-Sense) drivers. Replicates
and extends Acer NitroSense / PredatorSense with performance profiles, fan
control, battery management, keyboard backlight control, LCD override and live
hardware monitoring.

> Project is under passive development.

## Features

- Performance profiles: Eco, Quiet, Balanced, Performance, Turbo. Availability follows AC/battery state.
- Custom profiles: save, apply and delete full configuration sets. Stored in `~/.config/DivAcerManagerMax/profiles.json`.
- Fan control: auto, max, manual CPU/GPU speeds, and custom fan curves with safety reset on exit.
- Live monitoring: CPU/GPU temperature, usage, frequency, power, GPU VRAM, fan RPM, battery health/cycles, live graphs.
- Battery: charge limiter (80%), calibration cycle, USB Power Delivery.
- Keyboard: per-zone RGB, presets, dynamic effects, backlight timeout.
- System: LCD override, boot animation/sound toggle.
- Diagnostics: daemon and service state, sensor sources, log tail, copyable report, restart controls.
- Compatibility page: per-feature support matrix; the UI hides unsupported features.
- System tray, in-app notifications, modern dark UI.

The daemon auto-detects feature support, uses around 10 MB RAM, runs
independently of the GUI and supports recursive restart for recovery.

## Requirements

- Ubuntu 25+ or Debian equivalent (standalone installer also available)
- Kernel 6.13+
- Linuwu Sense kernel drivers (installed in the steps below)

## Installation

### Debian package (recommended)

Download `damx_<version>_amd64.deb` from
[Releases](https://github.com/PXDiv/Div-Acer-Manager-Max/releases), then:

```bash
sudo apt install ./damx_1.1.0_amd64.deb
sudo damx-setup --drivers   # compile and install the kernel drivers
sudo damx-setup --nitro     # optional: enable the Nitro/PredatorSense button
damx-setup                  # show daemon, driver and button status
```

The GUI is available as `damx` / `DAMX` and in the application menu. Kernel
drivers are built against your running kernel, which is why they are a separate
step (`damx-setup --drivers`).

### Standalone installer

1. Download the latest release and extract it.
2. Make the script executable: `chmod +x setup.sh`
3. Run it: `./setup.sh`
4. Choose an option: `1` install, `2` install without drivers, `3` uninstall, `4` reinstall/update.
5. Reboot.

### Build the package yourself

```bash
./packaging/build-deb.sh
```

Requirements: .NET SDK 9, `dpkg-dev`, `debhelper`, `fakeroot`. Output:
`dist/damx_<version>_amd64.deb`. See [packaging/README.md](packaging/README.md)
for details and for hosting your own apt repository.

## Nitro / PredatorSense button

Both Nitro (N key) and Predator machines use the same EC button. It sends
scancode `0xf5`, which the kernel maps to keycode `425` on most models; some
models remap it to `prog1` (`148`), so setup captures the code from an actual
press instead of assuming one.

Confirmed: Nitro ANV16S-41 and Predator PHN16S-71 (both `425`).

keyd/kmonad users: remappers grab the keyboard exclusively, so the detection
service never sees the button. Bind it in the remapper config instead (after
keyd it typically appears as `f16` / `XF86Launch7`).

## Compatibility

Supported models are listed in [Compatibility.md](Compatibility.md). DAMX works
on most Acer devices even when not listed; if your model is missing, filing an
issue with the model details helps others.

## Troubleshooting

- Logs: `/var/log/DAMX_Daemon_Log.log`
- `UNKNOWN` laptop type: restart first. If it persists, the drivers likely
  failed to build (check kernel headers).
- Check [FAQ.md](FAQ.md) before opening an issue.
- Report bugs or request features via
  [Issues](https://github.com/PXDiv/Div-Acer-Manager-Max/issues).

## Credits

Built on the [Linuwu Sense](https://github.com/0x7375646F/Linuwu-Sense) drivers.

## License

GNU General Public License v3.0. See [LICENSE](LICENSE).
