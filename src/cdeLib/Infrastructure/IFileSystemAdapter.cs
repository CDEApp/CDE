namespace cdeLib.Infrastructure;

/// <summary>
/// Abstraction for file system path operations to enable testing.
/// </summary>
public interface IFileSystemAdapter
{
    string GetFullPath(string path);
    bool IsUnc(string path);
    string GetDirectoryRoot(string path);

    /// <summary>
    /// True on Windows. Catalog names differ by platform (a drive letter, volume label and UNC part on Windows; the
    /// scan path on Linux and macOS), so the platform comes from here and a test can pick either.
    /// </summary>
    bool IsWindows { get; }

    /// <summary>
    /// The label of the volume holding <paramref name="rootPath"/>; empty when it has none or the path has no root on
    /// this host (a Windows drive path on Linux or macOS).
    /// </summary>
    string GetVolumeLabel(string rootPath);
}
