---
sidebar_position: 6
title: Linux
---

# Linux

A release has `cde-<version>-linux-x64.tar.gz` (with a `.sha256` file) for 64-bit Linux. It holds:

- `cde/`: the `cde` command line program and its `appsettings.json`;
- `cde-tauri/`: the `cde-tauri` application and its `cdeApi` helper.

There is no `cdeWin`, because it is a Windows Forms application. There are no installers: extract the archive and run the programs.

```bash
sha256sum -c cde-2.1.0-linux-x64.tar.gz.sha256
tar -xzf cde-2.1.0-linux-x64.tar.gz
cd cde-2.1.0-linux-x64/cde
./cde --help
```

## Notes

- `cde` finds its settings itself, so you can run it from any folder. On the first run it writes a default `appsettings.json` to `~/.config/cde/` (or `$XDG_CONFIG_HOME/cde/`). Set `CDE_CONFIG_DIR` to use another folder. See [Configuration](../reference/configuration.md).
- `cde-tauri` needs the system libraries for its web view. On Debian and Ubuntu:

  ```bash
  sudo apt install libwebkit2gtk-4.1-0 libayatana-appindicator3-1
  ```

  Other distributions have packages with similar names (WebKitGTK 4.1).
- The archive is built on Ubuntu 22.04, so you need a glibc at least as new as that one.
- Catalog files are named after the scan path. `cde scan /mnt/data` makes `mnt_data.cdex`. See [Catalog files](catalog-files.md).
- `cde scan` skips some system folders by default. See [Configuration](../reference/configuration.md#default-excludes-on-linux).

:::caution Known limit
In `cde-tauri` on Linux, **Open, Explore and Properties do nothing**, and neither do custom shell commands.
:::
