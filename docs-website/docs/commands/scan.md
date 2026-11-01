---
sidebar_position: 2
title: scan
---

# cde scan

Scan a folder or drive and write a catalog file.

```text
cde scan <path> [--desc <text>] [--follow-junctions] [--exclude <path>]... [-v]
```

| Argument or option | Description |
| ------------------ | ----------- |
| `<path>` | Required. The folder or drive to scan. |
| `--desc <text>` | A description to store in the catalog. |
| `--follow-junctions` | Go into directory junctions and symbolic links. **Not extensively tested.** |
| `--exclude <path>` | A folder to skip for this scan only. Repeat it for several folders. |
| `-v`, `--verbose` | After the scan, list the folders that could not be read (access denied or an I/O error). |

## What it does

`cde scan` creates or updates the catalog for the path. The file name comes from the path. See [Catalog files](../guide/catalog-files.md).

If a catalog for the path already exists, cde copies its hashes to the new catalog for every file whose path, name and size still match. Only the last modified time of each entry is stored.

```batch
cde scan C:\
cde scan E:\ --desc "Photo backup drive"
```

## Junctions and symbolic links

By default cde records directory junctions and symbolic links (reparse points) in the catalog, but does **not** go into them. This avoids scan loops, such as a junction that points back to a parent folder, and duplicated content.

```batch
cde scan C:\ --follow-junctions
```

## Skip folders

`--exclude` adds to the `AppConfig.Scan.ExcludePaths` setting and to the built-in excludes. It follows the same [rules](../reference/configuration.md#excluding-folders). Nothing is saved in the catalog or in the configuration.

```bash
cde scan /data --exclude /data/cache --exclude "/data/*/tmp"
```

| Pattern | Skips |
| ------- | ----- |
| `/data/cache` | That folder and everything under it. |
| `/data/*/tmp` | `tmp` inside any one folder under `/data`. `*` matches inside one path segment. There is no `**`. |
| `node_modules` | A folder of that name at any depth below the scan root. |
| `/tmp/*` | The contents of `/tmp`, but `/tmp` itself is still recorded. |

An excluded folder is kept as an empty entry. It is not reported as an error.

## Related

- [Configuration](../reference/configuration.md) for permanent excludes and progress updates.
- [`cde hash`](hash.md) to add hashes to the catalog.
