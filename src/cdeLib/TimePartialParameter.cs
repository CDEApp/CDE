using System;
using System.Diagnostics.CodeAnalysis;

namespace cdeLib
{
    /// <summary>
    /// Unspecified values per format are returned as zero if no error.
    /// </summary>
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
    [SuppressMessage("ReSharper", "ConvertToConstant.Local")]
    public class TimePartialParameter
    {
        // "HH:MM:SS"; example
        private static string _format = "<HH>:<MM>:<SS>";
        private string _activeFormat;

        private int _hour;  // 0 - 23
        public int Hour
        {
            get
            {
                ThrowExceptionIfSet();
                return _hour;
            }
        }

        private int _minute;  // 0 - 59
        public int Minute
        {
            get
            {
                ThrowExceptionIfSet();
                return _minute;
            }
        }

        private int _second;  // 0 - 59
        public int Second
        {
            get
            {
                ThrowExceptionIfSet();
                return _second;
            }
        }

        private readonly Exception _e;

        private void ThrowExceptionIfSet()
        {
            if (_e != null)
            {
                throw _e;
            }
        }

        public TimePartialParameter(string str) : this(str, _format) { }

        public TimePartialParameter(string str, string activeFormat)
        {
            _activeFormat = activeFormat;
            var splitOnColon = str.Split(':');
            int hour;
            int.TryParse(splitOnColon[0], out hour);
            if (hour == 0 || hour > 23)
            {
                _e = new ArgumentException(
                    $"Require valid Integer 1-23 for Hour <HH> as part of format '{_activeFormat}'");
                return;
            }
            _hour = hour;

            if (splitOnColon.Length > 1) // may have hour specified
            {
                if (splitOnColon[1].Length == 0) // just a ':' is allowed with no value. just set hour value.
                {
                    return;
                }
                int minute;
                int.TryParse(splitOnColon[1], out minute);
                if (minute == 0 || minute > 59)
                {
                    _e = new ArgumentException(
                        $"Require valid integer 1-59 or for Minute <MM> as part of format '{_activeFormat}'");
                    return;
                }
                _minute = minute;
            }

            if (splitOnColon.Length > 2) // may have second specified
            {
                if (splitOnColon[2].Length == 0) // just a ':' is allowed with no value. just set hour and minute value.
                {
                    return;
                }
                int second;
                int.TryParse(splitOnColon[2], out second);
                if (second == 0 || second > 59)
                {
                    _e = new ArgumentException(
                        $"Require valid integer 1-59 or for Second <SS> as part of format '{_activeFormat}'");
                    return;
                }
                _second = second;
            }
        }
    }
}