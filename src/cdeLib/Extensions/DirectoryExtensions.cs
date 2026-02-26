using System.Runtime.InteropServices;

namespace cdeLib.Extensions;

/// <summary>
/// A compilation of the properties of folders and files in a file system.
/// </summary>
public struct FileSystemProperties
{
    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetDiskFreeSpaceEx(string lpDirectoryName, out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes, out ulong lpTotalNumberOfFreeBytes);

    private FileSystemProperties(long? totalBytes, long? freeBytes, long? availableBytes)
        : this()
    {
        TotalBytes = totalBytes;
        FreeBytes = freeBytes;
        AvailableBytes = availableBytes;
    }

    /// <summary>
    /// Gets the total number of bytes on the drive.
    /// </summary>
    public long? TotalBytes { get; private set; }

    /// <summary>
    /// Gets the number of bytes free on the drive.
    /// </summary>
    public long? FreeBytes { get; private set; }

    /// <summary>
    /// Gets the number of bytes available on the drive (counts disk quotas).
    /// </summary>
    public long? AvailableBytes { get; private set; }

    /// <summary>
    /// Gets the properties for this file system.
    /// </summary>
    /// <param name="volumeIdentifier">The path whose volume properties are to be queried.</param>
    /// <returns>A <see cref="FileSystemProperties"/> containing the properties for the specified file system.</returns>
    public static FileSystemProperties GetProperties(string volumeIdentifier)
    {
        if (GetDiskFreeSpaceEx(volumeIdentifier, out var available, out var total, out var free))
        {
            return new FileSystemProperties((long)total, (long)free, (long)available);
        }

        return new FileSystemProperties(null, null, null);
    }
}