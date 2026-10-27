namespace cdeApi;

/// <summary>Sidecar settings read from configuration (<c>Cde:*</c>).</summary>
public sealed class ApiOptions
{
    /// <summary>
    /// <c>Cde:ConfigPath</c>: the folder catalogs load from at startup and on a reload without <c>?path</c>.
    /// Defaults to the current directory.
    /// </summary>
    public string ConfigPath { get; set; } = ".";
}
