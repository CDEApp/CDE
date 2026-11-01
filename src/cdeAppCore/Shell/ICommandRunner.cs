using System.Collections.Generic;

namespace cdeAppCore.Shell;

/// <summary>Result of launching an external program. <see cref="Error"/> is set when it did not start or did not succeed.</summary>
public readonly record struct CommandRunResult(bool Success, string Error = null);

/// <summary>
/// Launches an external program with an exact argv (no shell string, so paths need no quoting). It is
/// injectable so the Linux and macOS shell actions can be tested without a desktop session.
/// </summary>
public interface ICommandRunner
{
    /// <summary>
    /// Start <paramref name="fileName"/>. With <paramref name="wait"/> false the program is spawned and not awaited
    /// (success means it started). With true it is awaited up to a short timeout and success means exit code 0.
    /// </summary>
    CommandRunResult Run(string fileName, IReadOnlyList<string> arguments, bool wait);
}
