using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Spectre.Console;

namespace cde.ScanProgress;

public class ScanProgressConsole
{
    public static int ScanCount { get; set; }

    public static string CurrentFile { get; set; }

    public static bool ScanIsComplete { get; set; } = false;

    private static readonly Queue<string> Messages = new();

    string NormalizeLength(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static void WriteLogMessage(string message)
    {
        AnsiConsole.MarkupLine($"[grey]LOG:[/]{Markup.Escape(message)}[grey]...[/]");
    }

    public void Start(Task mainLoopTask, CancellationToken cancellationToken)
    {
        const int increment = 10000;
        const int updateIntervalMs = 500;
        var sw = new Stopwatch();
        sw.Start();
        AnsiConsole.Status()
            .AutoRefresh(enabled: true)
            .Spinner(Spinner.Known.Default)
            .Start("Thinking...", ctx =>
            {
                var reportCounter = increment;
                while (!mainLoopTask.IsCompleted && !cancellationToken.IsCancellationRequested && !ScanIsComplete)
                {
                    reportCounter = ShowProgress(reportCounter, sw, increment, ctx, updateIntervalMs);
                }
            });
    }

    private int ShowProgress(int reportCounter, Stopwatch sw, int increment, StatusContext ctx, int updateIntervalMs)
    {
        var defaultNumberFormat = new NumberFormatInfo();
        var msg = NormalizeLength($"Scanned {ScanCount.ToString("N0", defaultNumberFormat)} files. At: {CurrentFile}",
            AnsiConsole.Profile.Out.Width - 6);
        if (ScanCount > reportCounter)
        {
            var elapsedSec = sw.ElapsedMilliseconds / 1000;
            if (elapsedSec < 1) elapsedSec = 1;
            var scansPerSec = ScanCount / elapsedSec;
            Messages.Enqueue(
                $"Scanned {ScanCount.ToString("N0", defaultNumberFormat)} files. Avg {scansPerSec.ToString("N0", defaultNumberFormat)}/sec");
            reportCounter += increment;
        }

        try
        {
            ctx.Status(Markup.Escape(msg));
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error writing Status");
        }

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
        return reportCounter;
    }
}