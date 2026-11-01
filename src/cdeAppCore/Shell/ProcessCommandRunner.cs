using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace cdeAppCore.Shell;

/// <summary><see cref="ICommandRunner"/> over <see cref="Process"/>, passing arguments through <c>ArgumentList</c>.</summary>
public sealed class ProcessCommandRunner : ICommandRunner
{
    private static readonly TimeSpan SpawnGrace = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    public CommandRunResult Run(string fileName, IReadOnlyList<string> arguments, bool wait)
    {
        var info = new ProcessStartInfo(fileName) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
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
            // Drain the pipes so the child never blocks on them, and reap it when it exits.
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => process.Dispose();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // A launcher that fails (no handler, bad override) exits non-zero almost at once; one still
            // running after the grace period has started its viewer, which counts as success.
            try
            {
                if (process.WaitForExit(SpawnGrace) && process.ExitCode != 0)
                {
                    return new CommandRunResult(false, $"{fileName} exited with code {process.ExitCode}");
                }
            }
            catch (InvalidOperationException)
            {
                // Already disposed by the Exited handler: it finished; treat as started.
            }

            return new CommandRunResult(true);
        }

        using (process)
        {
            _ = process.StandardOutput.ReadToEndAsync();
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
