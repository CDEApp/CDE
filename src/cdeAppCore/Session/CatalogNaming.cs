using System;
using System.IO;
using System.Linq;
using cdeLib.Entities;

namespace cdeAppCore.Session;

/// <summary>Pure naming rules for new catalogs: cde's default file name, file-name validation, same-root matching.</summary>
public static class CatalogNaming
{
    public const string Extension = ".cdex";
    public const int MaxNameLength = 150;

    // Computed once. Always includes the control characters, whatever the platform reports.
    private static readonly char[] InvalidNameChars =
        Path.GetInvalidFileNameChars().Concat(Enumerable.Range(0, 32).Select(i => (char)i)).Distinct().ToArray();

    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    /// <summary>
    /// cde's default catalog name for a root, without extension: <c>{hint}-{volume}</c> for a volume root,
    /// <c>{hint}-{volume}-{safePath}</c> for a folder, <c>{hint}-{server_share_path}</c> for UNC. Null when the
    /// path can't be interpreted. Does not touch the file system beyond the volume-label lookup.
    /// </summary>
    public static string DefaultFileName(string rootPath)
    {
        var canonical = CanonicalRoot(rootPath);
        if (canonical == null) return null;
        try
        {
            var re = new RootEntry();
            var name = re.GetDefaultFileName(canonical, out _, out _);
            return name.EndsWith(".cde", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The root as cde canonicalises it when scanning (full path, trailing separator trimmed); null if invalid.</summary>
    public static string CanonicalRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) return null;
        try
        {
            return new RootEntry().CanonicalPath(rootPath.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException
                                       or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static bool SameRoot(string a, string b)
    {
        var ca = CanonicalRoot(a);
        var cb = CanonicalRoot(b);
        return ca != null && cb != null && string.Equals(ca, cb, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Trim, and drop a trailing <c>.cdex</c> the user typed (the extension is always appended).</summary>
    public static string NormalizeFileName(string name)
    {
        name = (name ?? "").Trim();
        return name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? name[..^Extension.Length].TrimEnd() : name;
    }

    /// <summary>Why a (normalised, extension-less) name can't be used as a file name, or null when it is fine.</summary>
    public static string ValidateFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Enter a file name.";
        if (name.Length > MaxNameLength) return $"The file name is too long (max {MaxNameLength} characters).";
        if (name.IndexOfAny(['\\', '/']) >= 0) return "The file name can't contain path separators (\\ or /).";
        if (name.IndexOfAny(InvalidNameChars) >= 0)
        {
            var bad = name.Where(c => Array.IndexOf(InvalidNameChars, c) >= 0).Distinct().ToArray();
            return "The file name can't contain: " + string.Join(" ", bad.Select(c => c < ' ' ? "control characters" : c.ToString()));
        }
        if (name.EndsWith('.') || name.EndsWith(' ')) return "The file name can't end with a dot or a space.";
        var stem = name.Split('.')[0].TrimEnd();
        if (ReservedDeviceNames.Contains(stem, StringComparer.OrdinalIgnoreCase))
            return $"{stem} is a reserved name.";
        return null;
    }
}
