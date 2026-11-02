using System;
using System.IO;
using System.Linq;
using System.Text;
using cdeLib.Output;

namespace cde;

/// <summary>
/// Standard output as a stream of <see cref="IRecordWriter"/> records for <c>--json</c> / <c>--csv</c> (#173). The records
/// go to the real standard output as UTF-8 with <c>\n</c> line ends; <see cref="Redirect"/> sends everything else the
/// program writes to the console (progress, Spectre displays, logs, status lines) to standard error, so standard output
/// holds only data.
/// </summary>
public sealed class MachineOutput : IDisposable
{
    private readonly StreamWriter _stdout;

    private MachineOutput(StreamWriter stdout, IRecordWriter writer)
    {
        _stdout = stdout;
        Writer = writer;
    }

    public IRecordWriter Writer { get; }

    // The verbs that take --json / --csv.
    private static readonly string[] Verbs = ["find", "findpath", "grep", "greppath", "everything", "dupes", "hash"];

    /// <summary>
    /// True when the command line asks for a machine format, so the console must be redirected before anything is written:
    /// the first argument that is not an option is a verb that supports it, and <c>--json</c> / <c>--csv</c> is among the
    /// arguments before any <c>--</c> terminator (what follows is data, not options).
    /// </summary>
    public static bool IsRequested(string[] args)
    {
        string verb = null;
        var flag = false;
        foreach (var a in args)
        {
            if (string.Equals(a, "--", StringComparison.Ordinal)) break;
            if (a.Equals("--json", StringComparison.OrdinalIgnoreCase) || a.Equals("--csv", StringComparison.OrdinalIgnoreCase)) flag = true;
            else if (verb == null && !a.StartsWith('-')) verb = a;
        }

        return flag && verb != null && Verbs.Contains(verb, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Sends <see cref="Console.Out"/> to standard error. Call before anything else writes to the console.</summary>
    public static void Redirect() => Console.SetOut(Console.Error);

    /// <summary>The writer for a machine format over standard output, or null for <see cref="OutputFormat.Text"/>. Dispose it to flush.</summary>
    public static MachineOutput Open(OutputFormat format, string[] columns, Stream stdout = null)
    {
        if (format == OutputFormat.Text) return null;
        var text = new StreamWriter(stdout ?? Console.OpenStandardOutput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 64 * 1024) { NewLine = "\n" };
        return new MachineOutput(text, RecordWriter.Create(format, text, columns));
    }

    public void Dispose() => _stdout.Dispose();
}
