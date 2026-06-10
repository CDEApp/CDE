using System;

namespace cdeLib;

/// <summary>
/// Unspecified values per format are returned as zero if no error.
/// </summary>
public class TimePartialParameter
{
    // "HH:MM:SS"; example
    private const string Format = "<HH>:<MM>:<SS>";

    public int Hour
    {
        get
        {
            ThrowExceptionIfSet();
            return field;
        }
        private set;
    }

    public int Minute
    {
        get
        {
            ThrowExceptionIfSet();
            return field;
        }
        private set;
    }

    public int Second
    {
        get
        {
            ThrowExceptionIfSet();
            return field;
        }
        private set;
    }

    private readonly Exception _e;

    private void ThrowExceptionIfSet()
    {
        if (_e != null)
        {
            throw _e;
        }
    }

    public TimePartialParameter(string str, string activeFormat = Format)
    {
        var activeFormat1 = activeFormat;
        var splitOnColon = str.Split(':');
        int.TryParse(splitOnColon[0], out var hour);
        if (hour is 0 or > 23)
        {
            _e = new ArgumentException(
                $"Require valid Integer 1-23 for Hour <HH> as part of format '{activeFormat1}'");
            return;
        }
        Hour = hour;

        if (splitOnColon.Length > 1) // may have an hour specified
        {
            if (splitOnColon[1].Length == 0) // A ':' is allowed with no value. Set the hour value.
            {
                return;
            }

            int.TryParse(splitOnColon[1], out var minute);
            if (minute is 0 or > 59)
            {
                _e = new ArgumentException(
                    $"Require valid integer 1-59 or for Minute <MM> as part of format '{activeFormat1}'");
                return;
            }
            Minute = minute;
        }

        if (splitOnColon.Length > 2) // may have second specified
        {
            if (splitOnColon[2].Length == 0) // A ':' is allowed with no value. Set hour and minute value.
            {
                return;
            }

            int.TryParse(splitOnColon[2], out var second);
            if (second is 0 or > 59)
            {
                _e = new ArgumentException(
                    $"Require valid integer 1-59 or for Second <SS> as part of format '{activeFormat1}'");
                return;
            }
            Second = second;
        }
    }
}