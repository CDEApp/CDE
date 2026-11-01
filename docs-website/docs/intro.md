---
slug: /
sidebar_position: 1
title: Introduction
---

# cde: Catalog Directory Entries

**cde** captures the directory entries of your drives into catalog files. You can then search those catalogs, find duplicate files, and browse what was on a disk, even when the disk is not connected.

A catalog records names, sizes and last modified times, and can also hold file hashes. Searching a catalog is fast and does not touch the original drive.

cde is inspired by Cathy, an older tool that could only hold 65,535 entries. cde has no such limit.

## What is in the box

| Program | What it is |
| ------- | ---------- |
| `cde` | The command line program. It scans, searches, hashes and finds duplicates. |
| `cdeWin` | A Windows Forms application to search, sort and browse catalogs. |
| `cde-tauri` | A second desktop application (Windows and Linux) with the same search features. |

See [Applications](guide/applications.md) for the details of each one.

## What you can do

- **Scan** a drive or folder into a catalog: [`cde scan`](commands/scan.md).
- **Search** every catalog at once, by name, regular expression or [Everything-style query](commands/everything.md).
- **Find duplicate files** across all your catalogs: [`cde hash`](commands/hash.md) then [`cde dupes`](commands/dupes.md).
- **Keep working offline.** A catalog of an external drive stays searchable when the drive is in a drawer.

## Where to start

1. [Install cde](installation.md).
2. Follow the [Quick start](guide/quick-start.md).
3. Look up any option in the [Command reference](commands/index.md).

:::note
This site documents the current `develop` branch. If a command is missing from your copy, check that you have the latest [release](https://github.com/CDEApp/CDE/releases).
:::
