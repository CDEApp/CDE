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
}
