namespace cdeLib.Infrastructure;

/// <summary>
/// Abstraction for file system path operations to enable testing.
/// </summary>
public interface IFileSystemAdapter
{
    string GetFullPath(string path);
    bool IsUnc(string path);
    string GetDirectoryRoot(string path);
}
