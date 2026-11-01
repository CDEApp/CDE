---
sidebar_position: 2
title: Installation
---

# Installation

Download a release from the [releases page](https://github.com/CDEApp/CDE/releases).

| Platform | File |
| -------- | ---- |
| Windows (64-bit) | `cde-<version>-win-x64.zip` |
| Linux (64-bit) | `cde-<version>-linux-x64.tar.gz` |

Each file has a `.sha256` checksum beside it.

There is no installer. Extract the archive and run the programs. The `cde` and `cdeWin` executables are single files that you can copy anywhere.

## Windows

1. Before you extract the zip, right-click it, choose **Properties**, tick **Unblock**, then click **OK**.
2. Extract the zip.
3. Open a terminal in the `cde` folder and run `cde --help`.

:::warning The executables are not signed
Microsoft Defender SmartScreen shows "Windows protected your PC" with the publisher "Unknown publisher". Click **More info**, then **Run anyway**. Unblocking the zip first (step 1) stops the warning for every file.

If Smart App Control is on (Windows 11), Windows can block unsigned apps with no option to run them.

You can check the [release build pipeline](https://github.com/CDEApp/CDE/actions/workflows/release.yml) to see that the release is compiled in CI from the source.
:::

## Linux

See [Linux](guide/linux.md).

## Check the install

```bash
cde --help
cde --version
```

`cde --help` lists every command. `cde <command> --help` shows the options of one command.
