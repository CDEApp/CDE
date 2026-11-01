using System.Collections.Generic;
using Serilog;

namespace cdeAppCore.Shell;

/// <summary>
/// Implementation of <see cref="IShellActions"/> for hosts with no shell support. Each operation is a logged
/// <see cref="ShellStatus.Unsupported"/> result rather than a P/Invoke failure. The configured command list is
/// still exposed so a frontend renders the same menu shape.
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

    public ShellResult Open(string fullPath) => Unsupported(nameof(Open), fullPath);
    public ShellResult Explore(string fullPath) => Unsupported(nameof(Explore), fullPath);
    public ShellResult ShowProperties(string fullPath) => Unsupported(nameof(ShowProperties), fullPath);

    public ShellResult RunCustomCommand(int commandIndex, string fullPath)
        => Unsupported(nameof(RunCustomCommand), fullPath);

    public ShellResult RunCustomCommand(CustomCommandOptions command, string fullPath)
        => Unsupported(nameof(RunCustomCommand), fullPath);

    private ShellResult Unsupported(string action, string fullPath)
    {
        _logger?.Information("Shell action {Action} is not supported on this host (path {Path})", action, fullPath);
        return ShellResult.Unsupported($"{action} is not supported on this operating system");
    }
}
