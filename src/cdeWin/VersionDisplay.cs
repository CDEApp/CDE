namespace cdeWin;

public static class VersionDisplay
{
    private const int ShortShaLength = 7;

    /// <summary>
    /// Shortens SemVer build metadata (<c>+&lt;sha&gt;</c>, as emitted by MinVer) to a 7 char short hash.
    /// Versions without metadata, or with metadata of 7 chars or less, are returned unchanged.
    /// </summary>
    public static string ShortenBuildMetadata(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return string.Empty;
        }

        var plus = version.IndexOf('+');
        if (plus < 0 || version.Length - plus - 1 <= ShortShaLength)
        {
            return version;
        }

        return version[..(plus + 1 + ShortShaLength)];
    }
}
