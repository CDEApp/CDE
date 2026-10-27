namespace cdeAppCore.Session;

/// <summary>Child path derivation shared by search rows and directory listings.</summary>
internal static class PathJoin
{
    /// <summary>
    /// <paramref name="parentPath"/> + separator + <paramref name="name"/>, with no separator when the
    /// parent is empty or already ends in one (a "C:\" root): the same rule the catalog full-path
    /// builders apply, so it equals the child's own full path.
    /// </summary>
    public static string Child(string parentPath, string name)
    {
        if (parentPath.Length > 0 && parentPath[^1] != '\\' && parentPath[^1] != '/')
            return string.Concat(parentPath, System.IO.Path.DirectorySeparatorChar.ToString(), name);
        return string.Concat(parentPath, name);
    }
}
