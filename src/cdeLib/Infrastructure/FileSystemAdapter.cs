using System.IO;

namespace cdeLib.Infrastructure;

/// <summary>
/// Default implementation of IFileSystemAdapter that wraps System.IO methods.
/// </summary>
public class FileSystemAdapter : IFileSystemAdapter
{
    public string GetFullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    public bool IsUnc(string path)
    {
        return Path.IsPathFullyQualified(path) && path.StartsWith("\\\\");
    }

    public string GetDirectoryRoot(string path)
    {
        return Directory.GetDirectoryRoot(path);
    }
}
