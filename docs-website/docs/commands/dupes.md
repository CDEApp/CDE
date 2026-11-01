---
sidebar_position: 6
title: dupes
---

# cde dupes

List duplicate files in all loaded catalogs.

```text
cde dupes
```

It takes no options.

`cde dupes` does no hashing. It compares the hashes that are already in the catalogs, so it is fast. If no hashing has been done, it finds no duplicates. Run [`cde hash`](hash.md) first.

```batch
cde hash
cde dupes
```

:::caution
`cde dupes` does not look at the file system. If files changed after the last `cde hash`, the report may not be useful. Hash again before you act on it.
:::

See [Finding duplicate files](../guide/finding-duplicates.md).
