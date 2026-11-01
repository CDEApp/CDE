namespace cdeAppCore.Shell;

public enum ShellStatus
{
    Ok,
    /// <summary>This host has no way to perform the action (maps to HTTP 501).</summary>
    Unsupported,
    /// <summary>The action was attempted and did not work (maps to HTTP 422).</summary>
    Failed
}

/// <summary>Outcome of a shell action: a status plus, unless <see cref="ShellStatus.Ok"/>, the reason.</summary>
public readonly record struct ShellResult(ShellStatus Status, string Message = null)
{
    public bool IsOk => Status == ShellStatus.Ok;

    public static ShellResult Ok { get; } = new(ShellStatus.Ok);
    public static ShellResult Unsupported(string message) => new(ShellStatus.Unsupported, message);
    public static ShellResult Failed(string message) => new(ShellStatus.Failed, message);
}
