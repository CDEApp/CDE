using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Spectre.Console;

namespace cde.ScanProgress;

public class ScanProgressConsole
{
    public static int ScanCount { get; set; }

    public static string CurrentFile { get; set; }

    public static bool ScanIsComplete { get; set; }

    private static readonly Queue<string> Messages = new();

    string NormalizeLength(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        return maxLength < 3
            ? value[..maxLength]
            : value[..(maxLength - 3)] + "...";
    }

    private static void WriteLogMessage(string message)
    {
        var width = AnsiConsole.Profile.Out.Width;
        // Pad raw message to console width to clear previous content
        var paddedMessage = $"LOG:{message}".PadRight(width);
        // Use \r to return to start of line (status update behavior)
        AnsiConsole.Markup($"\r[grey]{Markup.Escape(paddedMessage)}[/]");
    }

    public void Start(Task mainLoopTask, CancellationToken cancellationToken)
    {
        var sw = new Stopwatch();
        sw.Start();
        AnsiConsole.Status()
            .AutoRefresh(enabled: true)
            .Spinner(Spinner.Known.Default)
            .Start("Thinking...", ctx =>
            {
                while (!mainLoopTask.IsCompleted && !cancellationToken.IsCancellationRequested && !ScanIsComplete)
                {
                    ShowProgress(sw, ctx);
                }
            });
    }

    private long CalculateScansPerSecond(Stopwatch sw)
    {
        var elapsedSec = sw.ElapsedMilliseconds / 1000;
        if (elapsedSec < 1) elapsedSec = 1;
        return ScanCount / elapsedSec;
    }

    private void ShowProgress(Stopwatch sw, StatusContext ctx)
    {
        var defaultNumberFormat = new NumberFormatInfo();
        var scansPerSec = CalculateScansPerSecond(sw);

        var currentPath = string.IsNullOrEmpty(CurrentFile)
            ? string.Empty
            : Path.GetDirectoryName(CurrentFile) ?? CurrentFile;

        var scanCountText = ScanCount.ToString("N0", defaultNumberFormat);
        var scansPerSecText = scansPerSec.ToString("N0", defaultNumberFormat);
        var escapedPath = Markup.Escape(currentPath);

        // Normalize the path separately to ensure it doesn't get cut off mid-word
        var maxPathLength = AnsiConsole.Profile.Out.Width - 50; // Reserve space for the rest of the message
        if (maxPathLength > 0)
        {
            escapedPath = NormalizeLength(escapedPath, maxPathLength);
        }

        // Build message with markup - this won't be truncated so markup tags stay balanced
        var msg = $"Scanned [yellow]{scanCountText}[/] files Avg [yellow]{scansPerSecText}[/]/sec Dir [yellow]{escapedPath}[/]";

        try
        {
            ctx.Status(msg);
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
    }
}