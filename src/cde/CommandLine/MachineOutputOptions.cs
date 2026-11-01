using System;
using cdeLib.Output;
using CommandLine;
using JetBrains.Annotations;

namespace cde.CommandLine;

/// <summary>The <c>--json</c> and <c>--csv</c> options of find, dupes and hash (#173).</summary>
public abstract class MachineOutputOptions
{
    [Option("json",
        HelpText = "Write the results to standard output as JSON, one object per line, instead of the usual text. " +
                   "Progress and log messages go to standard error. Cannot be used with --csv.")]
    public bool Json { get; [UsedImplicitly] set; }

    [Option("csv",
        HelpText = "Write the results to standard output as CSV with a header row, instead of the usual text. " +
                   "Progress and log messages go to standard error. Cannot be used with --json.")]
    public bool Csv { get; [UsedImplicitly] set; }

    /// <summary>
    /// The output format the options ask for. Throws <see cref="FormatException"/> (reported as <c>error: ...</c>) for a
    /// combination that makes no sense, so the command fails before it writes anything.
    /// </summary>
    public virtual OutputFormat ResolveFormat()
    {
        if (Json && Csv) throw new FormatException("--json and --csv cannot be used together; choose one.");
        return Json ? OutputFormat.Json : Csv ? OutputFormat.Csv : OutputFormat.Text;
    }
}
