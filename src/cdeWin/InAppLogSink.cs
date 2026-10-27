using System;
using System.Collections.Generic;
using System.Text;
using Serilog.Core;
using Serilog.Events;

namespace cdeWin;

/// <summary>
/// Serilog sink feeding the in-app Log tab. WinForms free: buffers formatted lines (capped to the
/// newest <c>maxLines</c>) until a subscriber drains them. Emit never blocks on the UI; it only
/// takes a short lock, and signals the subscriber at most once per drain. The subscriber returns
/// whether it scheduled a drain; if it did not (or threw), the next event signals it again.
/// </summary>
public sealed class InAppLogSink : ILogEventSink
{
    public const int DefaultMaxLines = 5000;

    private readonly object _lock = new();
    private readonly int _maxLines;
    private readonly Queue<string> _pending = new();
    private Func<bool> _onLinesAvailable;
    private bool _notified;

    public InAppLogSink(int maxLines = DefaultMaxLines)
    {
        if (maxLines < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLines));
        }

        _maxLines = maxLines;
    }

    public int MaxLines => _maxLines;

    public void Emit(LogEvent logEvent)
    {
        var line = Format(logEvent);
        Func<bool> notify = null;
        lock (_lock)
        {
            _pending.Enqueue(line);
            while (_pending.Count > _maxLines)
            {
                _pending.Dequeue();
            }

            if (_onLinesAvailable != null && !_notified)
            {
                _notified = true;
                notify = _onLinesAvailable;
            }
        }

        if (notify != null)
        {
            Notify(notify);
        }
    }

    /// <summary>
    /// Subscribe; if lines were buffered before attach, the callback fires immediately. The callback
    /// returns true if it scheduled a <see cref="Drain"/>.
    /// </summary>
    public void Attach(Func<bool> onLinesAvailable)
    {
        ArgumentNullException.ThrowIfNull(onLinesAvailable);
        bool notifyNow;
        lock (_lock)
        {
            _onLinesAvailable = onLinesAvailable;
            notifyNow = _pending.Count > 0;
            _notified = notifyNow;
        }

        if (notifyNow)
        {
            Notify(onLinesAvailable);
        }
    }

    // Invoked outside the lock; subscriber must be quick and non-blocking (e.g. BeginInvoke).
    private void Notify(Func<bool> notify)
    {
        bool scheduled;
        try
        {
            scheduled = notify();
        }
        catch
        {
            // Logging must never throw into the caller.
            scheduled = false;
        }

        if (!scheduled)
        {
            // No drain is coming, so re-arm; otherwise the flag stays set and the tab stops updating.
            lock (_lock)
            {
                _notified = false;
            }
        }
    }

    public void Detach()
    {
        lock (_lock)
        {
            _onLinesAvailable = null;
            _notified = false;
        }
    }

    /// <summary>Returns and clears the buffered lines; re-arms the notification.</summary>
    public string[] Drain()
    {
        lock (_lock)
        {
            var lines = _pending.ToArray();
            _pending.Clear();
            _notified = false;
            return lines;
        }
    }

    internal static string Format(LogEvent e)
    {
        var sb = new StringBuilder(128);
        sb.Append(e.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(' ')
            .Append(LevelCode(e.Level))
            .Append(' ')
            .Append(e.RenderMessage());
        if (e.Exception != null)
        {
            sb.Append(Environment.NewLine).Append(e.Exception);
        }

        return sb.ToString();
    }

    private static string LevelCode(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        LogEventLevel.Fatal => "FTL",
        _ => "???"
    };
}
