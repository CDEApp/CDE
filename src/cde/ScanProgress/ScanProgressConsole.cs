using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace cde.ScanProgress;

public class ScanProgressConsole
{
    public static int ScanCount { get; set; }
    public static string CurrentFile { get; set; }
    public static bool ScanIsComplete { get; set; } = false;

    public static readonly Queue<string> Messages = new();

    string NormalizeLength(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static void WriteLogMessage(string message)
    {
        AnsiConsole.MarkupLine($"[grey]LOG:[/] {message}[grey]...[/]");
    }

    public void Start(Task mainLoopTask, CancellationToken cancellationToken)
    {
        const int increment = 10000;
        const int updateIntervalMs = 250;
        var sw = new Stopwatch();
        sw.Start();
        AnsiConsole.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Default)
            .Start("Thinking...", ctx =>
            {
                var reportCounter = increment;
                while (!mainLoopTask.IsCompleted && !cancellationToken.IsCancellationRequested && !ScanIsComplete)
                {
                    var msg = NormalizeLength($"Scanned {ScanCount} files. At: {CurrentFile}", AnsiConsole.Profile.Out.Width - 6);
                    if (ScanCount > reportCounter)
                    {
                        var elapsedSec = sw.ElapsedMilliseconds / 1000;
                        var scansPerSec = ScanCount / elapsedSec;
                        Messages.Enqueue($"Scanned {ScanCount} messages. Avg {scansPerSec:0}/sec");
                        reportCounter += increment;
                    }

                    ctx.Status(msg);
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));
                    
                    var dequeueMessages = true;
                    while (dequeueMessages)
                    {
                        Messages.TryDequeue(out msg);
                        if (string.IsNullOrEmpty(msg))
                        {
                            dequeueMessages = false;
                        }
                        else
                        {
                            WriteLogMessage(msg);
                        }
                    }
                    Thread.Sleep(updateIntervalMs);
                }
            });
    }
}