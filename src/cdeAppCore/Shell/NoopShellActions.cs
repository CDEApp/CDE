using System.Collections.Generic;
using Serilog;

namespace cdeAppCore.Shell;

/// <summary>
/// Non-Windows (or unsupported-host) implementation of <see cref="IShellActions"/>. The shell verbs
/// are Windows-specific, so each operation is a logged no-op rather than a P/Invoke failure. The
/// configured command list is still exposed so a frontend renders the same menu shape.
/// </summary>
public sealed class NoopShellActions : IShellActions
{
    private readonly ILogger _logger;

    public NoopShellActions(IReadOnlyList<CustomCommandOptions> customCommands, ILogger logger = null)
    {
        CustomCommands = customCommands ?? [];
        _logger = logger;
    }

    public IReadOnlyList<CustomCommandOptions> CustomCommands { get; }

    public void Open(string fullPath) => Unsupported(nameof(Open), fullPath);
    public void Explore(string fullPath) => Unsupported(nameof(Explore), fullPath);
    public void ShowProperties(string fullPath) => Unsupported(nameof(ShowProperties), fullPath);

    public void RunCustomCommand(int commandIndex, string fullPath)
        => Unsupported(nameof(RunCustomCommand), fullPath);

    public void RunCustomCommand(CustomCommandOptions command, string fullPath)
        => Unsupported(nameof(RunCustomCommand), fullPath);

    private void Unsupported(string action, string fullPath)
        => _logger?.Information("Shell action {Action} is not supported on this host (path {Path})", action, fullPath);
}
