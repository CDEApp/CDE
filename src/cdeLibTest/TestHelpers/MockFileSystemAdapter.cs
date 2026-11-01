using cdeLib.Infrastructure;

namespace cdeLibTest.TestHelpers;

/// <summary>
/// Mock file system adapter for testing that returns configurable values.
/// </summary>
public class MockFileSystemAdapter : IFileSystemAdapter
{
    private readonly string _root;
    private readonly bool _isUnc;
    private readonly string _fullPath;

    public MockFileSystemAdapter(
        string root = @"C:\",
        bool isUnc = false,
        string fullPath = @"C:\",
        bool isWindows = true
    )
    {
        _root = root;
        _isUnc = isUnc;
        _fullPath = fullPath;
        IsWindows = isWindows;
    }

    public bool IsWindows { get; }

    public string GetFullPath(string path)
    {
        return _fullPath;
    }

    public bool IsUnc(string path)
    {
        return _isUnc;
    }

    public string GetDirectoryRoot(string path)
    {
        return _root;
    }
}
