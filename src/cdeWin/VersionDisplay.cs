using System;
using System.Linq;

namespace cdeWin;

public static class VersionDisplay
{
    private const int ShortShaLength = 7;
    private const int FullShaMinLength = 40;

    /// <summary>
    /// Shortens any full git commit hash (40+ hex chars, as appended by the SDK's SourceRevisionId)
    /// in the SemVer build metadata (<c>+...</c>) to a 7 char short hash. Other metadata identifiers,
    /// and versions without metadata, are returned unchanged.
    /// </summary>
    public static string ShortenBuildMetadata(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return string.Empty;
        }

        var plus = version.IndexOf('+');
        if (plus < 0)
        {
            return version;
        }

        var identifiers = version[(plus + 1)..].Split('.');
        for (var i = 0; i < identifiers.Length; i++)
        {
            if (IsFullSha(identifiers[i]))
            {
                identifiers[i] = identifiers[i][..ShortShaLength];
            }
        }

        return string.Concat(version.AsSpan(0, plus + 1), string.Join('.', identifiers));
    }

    private static bool IsFullSha(string identifier) =>
        identifier.Length >= FullShaMinLength && identifier.All(char.IsAsciiHexDigit);
}
