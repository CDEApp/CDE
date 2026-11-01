using System;
using System.Collections.Generic;
using Serilog;

namespace cdeAppCore.Shell;

/// <summary>
/// Shared behaviour of the Linux and macOS <see cref="IShellActions"/>: user overrides for the three verbs,
/// custom commands run with a proper argv, and every outcome reported as a <see cref="ShellResult"/>.
/// </summary>
public abstract class UnixShellActions : IShellActions
{
    protected UnixShellActions(
        IReadOnlyList<CustomCommandOptions> customCommands, ICommandRunner runner, ShellCommandOverrides overrides, ILogger logger)
    {
        CustomCommands = customCommands ?? [];
        Runner = runner;
        Overrides = overrides ?? new ShellCommandOverrides();
        Logger = logger;
    }

    protected ICommandRunner Runner { get; }
    protected ShellCommandOverrides Overrides { get; }
    protected ILogger Logger { get; }

    public IReadOnlyList<CustomCommandOptions> CustomCommands { get; }

    public ShellResult Open(string fullPath) => WithOverride(Overrides.OpenCommand, fullPath, DefaultOpen);
    public ShellResult Explore(string fullPath) => WithOverride(Overrides.ExploreCommand, fullPath, DefaultExplore);
    public ShellResult ShowProperties(string fullPath) => WithOverride(Overrides.PropertiesCommand, fullPath, DefaultProperties);

    public ShellResult RunCustomCommand(int commandIndex, string fullPath)
    {
        if (commandIndex < 0 || commandIndex >= CustomCommands.Count)
        {
            return ShellResult.Failed($"no custom command {commandIndex}");
        }

        return RunCustomCommand(CustomCommands[commandIndex], fullPath);
    }

    public ShellResult RunCustomCommand(CustomCommandOptions command, string fullPath)
    {
        if (command == null || string.IsNullOrEmpty(command.Command))
        {
            return ShellResult.Failed("the custom command has no program set");
        }

        var arguments = CommandTokens.SplitAndSubstitute(command.Arguments, fullPath);
        return Report(command.Command, Runner.Run(command.Command, arguments, wait: false));
    }

    protected abstract ShellResult DefaultOpen(string fullPath);
    protected abstract ShellResult DefaultExplore(string fullPath);
    protected abstract ShellResult DefaultProperties(string fullPath);

    protected ShellResult Run(string fileName, IReadOnlyList<string> arguments, bool wait) =>
        Report(fileName, Runner.Run(fileName, arguments, wait));

    private ShellResult WithOverride(CustomCommandOptions command, string fullPath, Func<string, ShellResult> builtIn) =>
        command is { Command.Length: > 0 } ? RunCustomCommand(command, fullPath) : builtIn(fullPath);

    private ShellResult Report(string fileName, CommandRunResult result)
    {
        if (result.Success)
        {
            return ShellResult.Ok;
        }

        Logger?.Warning("Shell command {Command} failed: {Error}", fileName, result.Error);
        return ShellResult.Failed(result.Error ?? $"{fileName} failed");
    }
}
