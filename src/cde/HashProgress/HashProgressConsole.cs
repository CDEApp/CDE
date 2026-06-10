using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Spectre.Console;

namespace cde.HashProgress;

public class HashProgressConsole
{
    public static long FilesProcessed { get; set; }

    public static long FilesToHash { get; set; }

    public static string Phase { get; set; }

    public static bool HashIsComplete { get; set; }

    private static readonly ConcurrentQueue<string> Messages = new();

    private static void WriteLogMessage(string message)
    {
        var width = AnsiConsole.Profile.Out.Width;
        // Pad raw message to console width to clear previous content
        var paddedMessage = $"LOG:{message}".PadRight(width);
        // Use \r to return to start of line (status update behavior)
        AnsiConsole.Markup($"\r[grey]{Markup.Escape(paddedMessage)}[/]\n");
    }

    /// <summary>
    /// Enqueue a message to be displayed in the console progress UI
    /// </summary>
    public static void EnqueueMessage(string message)
    {
        Messages.Enqueue(message);
    }

    public void Start(Task mainLoopTask, CancellationToken cancellationToken)
    {
        var sw = new Stopwatch();
        sw.Start();
        AnsiConsole.Status()
            .AutoRefresh(enabled: true)
            .Spinner(Spinner.Known.Default)
            .Start("Hashing...", ctx =>
            {
                while (!mainLoopTask.IsCompleted && !cancellationToken.IsCancellationRequested)
                {
                    ShowProgress(sw, ctx);
                    // Throttle the refresh loop so it doesn't busy-spin a core while hashing.
                    Thread.Sleep(50);
                }

                // Flush any remaining messages after the task completes
                FlushMessages();
            });
    }

    private static void FlushMessages()
    {
        while (Messages.TryDequeue(out var msg))
        {
            if (!string.IsNullOrEmpty(msg))
            {
                WriteLogMessage(msg);
            }
        }
    }

    private static long CalculateFilesPerSecond(Stopwatch sw)
    {
        var elapsedSec = sw.ElapsedMilliseconds / 1000;
        if (elapsedSec < 1) elapsedSec = 1;
        return FilesProcessed / elapsedSec;
    }

    private void ShowProgress(Stopwatch sw, StatusContext ctx)
    {
        var defaultNumberFormat = new NumberFormatInfo();
        var filesPerSec = CalculateFilesPerSecond(sw);

        // Clamp to the total: FilesProcessed is cumulative across hash passes, so guard against
        // any accounting edge case rendering more than the total or over 100%.
        var processed = FilesToHash > 0 && FilesProcessed > FilesToHash ? FilesToHash : FilesProcessed;
        var processedText = processed.ToString("N0", defaultNumberFormat);
        var toHashText = FilesToHash.ToString("N0", defaultNumberFormat);
        var perSecText = filesPerSec.ToString("N0", defaultNumberFormat);
        var percent = FilesToHash > 0 ? 100.0 * processed / FilesToHash : 0.0;
        var phase = string.IsNullOrEmpty(Phase) ? "Hashing" : Phase;

        var msg =
            $"[yellow]{Markup.Escape(phase)}[/] [yellow]{processedText}[/] of [yellow]{toHashText}[/] ([yellow]{percent:F1}%[/]) Avg [yellow]{perSecText}[/]/sec";

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
            Messages.TryDequeue(out var queued);
            if (string.IsNullOrEmpty(queued))
            {
                dequeueMessages = false;
            }
            else
            {
                WriteLogMessage(queued);
            }
        }
    }
}
