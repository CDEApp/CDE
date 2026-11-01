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

    /// <summary>True when the command line asks for a machine format, so the console must be redirected before anything is written.</summary>
    public static bool IsRequested(string[] args) =>
        args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase) || a.Equals("--csv", StringComparison.OrdinalIgnoreCase));

    /// <summary>Sends <see cref="Console.Out"/> to standard error. Call before anything else writes to the console.</summary>
    public static void Redirect() => Console.SetOut(Console.Error);

    /// <summary>The writer for a machine format over standard output, or null for <see cref="OutputFormat.Text"/>. Dispose it to flush.</summary>
    public static MachineOutput Open(OutputFormat format, string[] columns, Stream stdout = null)
    {
        if (format == OutputFormat.Text) return null;
        var text = new StreamWriter(stdout ?? Console.OpenStandardOutput(), new UTF8Encoding(false), 64 * 1024) { NewLine = "\n" };
        return new MachineOutput(text, RecordWriter.Create(format, text, columns));
    }

    public void Dispose() => _stdout.Dispose();
}
