using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace cdeAppCore.Shell;

/// <summary><see cref="ICommandRunner"/> over <see cref="Process"/>, passing arguments through <c>ArgumentList</c>.</summary>
public sealed class ProcessCommandRunner : ICommandRunner
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    public CommandRunResult Run(string fileName, IReadOnlyList<string> arguments, bool wait)
    {
        var info = new ProcessStartInfo(fileName) { UseShellExecute = false, RedirectStandardError = wait };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(info);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new CommandRunResult(false, $"could not start {fileName}: {ex.Message}");
        }

        if (process is null)
        {
            return new CommandRunResult(false, $"could not start {fileName}");
        }

        if (!wait)
        {
            // Reap the child when it exits so a long-running viewer does not linger as a zombie.
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => process.Dispose();
            return new CommandRunResult(true);
        }

        using (process)
        {
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(WaitTimeout))
            {
                try { process.Kill(); } catch (InvalidOperationException) { /* already gone */ }
                return new CommandRunResult(false, $"{fileName} timed out");
            }

            if (process.ExitCode == 0)
            {
                return new CommandRunResult(true);
            }

            var detail = stderr.Wait(WaitTimeout) ? stderr.Result.Trim() : "";
            return new CommandRunResult(false, $"{fileName} exited with code {process.ExitCode}" + (detail.Length > 0 ? $": {detail}" : ""));
        }
    }
}
