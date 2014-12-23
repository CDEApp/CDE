using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ExternalLibTest
{
    /// <summary>
    /// Unspecified values as per format are returned as 1 for Month and Day and Zero for other fields.
    /// </summary>
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
    [SuppressMessage("ReSharper", "ConvertToConstant.Local")]
    public class DateTimePartialParameter
    {
        private string _format = "<YYYY>-<Month>-<DD>T<HH>:<MM>:<SS>";

        // a Parsing Expression Grammar mgith be a better way to do this.... PEG
        // - http://en.wikipedia.org/wiki/Parsing_expression_grammar

        private int _year;
        private int _month = 1; // 1-12
        private int _dayOfMonth = 1; // 1-31
        private int _hour = 0; // 0-23
        private int _minute = 0; // 0-59
        private int _second = 0; // 0-59
        private Exception _e;

        public DateTimePartialParameter(string str)
        {
            var splitOnDash = str.Split('-');

            int year;
            int.TryParse(splitOnDash[0], out year);
            if (year < 1000)
            {
                _e = new ArgumentException(string.Format("Require Year parameter be a 4 Digit Year <YYYY> as part of format '{0}'", _format));
                return;
            }
            _year = year;

            if (splitOnDash.Length > 1) // may have month specified
            {
                if (splitOnDash[1].Length == 0) // just a '-' is allowed with no value. just set year.
                {
                    return;
                }
                int month;
                if (!int.TryParse(splitOnDash[1], out month))
                {
                    //month = Convert.ToDateTime("01-" + p[1] + "-2000").Month;
                    DateTime tmp;
                    if (DateTime.TryParse("01-" + splitOnDash[1] + "-" + _year, out tmp))
                    {
                        month = tmp.Month;
                    }
                }
                if (month == 0 || month > 12)
                {
                    _e = new ArgumentException(string.Format("Require valid integer 1-12 or Month name for Month as part of format '{0}'", _format));
                    return;
                }
                _month = month;
            }


            if (splitOnDash.Length > 2) // may have dayOfMonth specified
            {
                if (splitOnDash[2].Length == 0) // just a '-' is allowed with no value. just set year and month
                {
                    return;
                }

                // If 'T' is not used as seperator between Date and Time then error
                if (SeperatorIsNotValid(splitOnDash[2], 'T'))
                {
                    _e = new ArgumentException(string.Format("The seperator between Date and Time must be 'T' as part of format '{0}'", _format));
                    return;
                }

                var splitOnT = splitOnDash[2].Split('T');

                int dayOfMonth = 0;
                int unValidatedDayOfMonth;
                if (int.TryParse(splitOnT[0], out unValidatedDayOfMonth))
                {
                    // check for valid day of month
                    DateTime tmpDateTime;
                    if (DateTime.TryParse(unValidatedDayOfMonth + "-" + _month + "-" + _year, out tmpDateTime))
                    {
                        dayOfMonth = tmpDateTime.Day;
                    }
                }
                
                if (dayOfMonth == 0 || dayOfMonth > 31)
                {
                    _e = new ArgumentException(string.Format("Require valid Day of Month integer range 1-31 for Day <DD> as part of format '{0}'", _format));
                    return;
                }
                _dayOfMonth = dayOfMonth;

                if (splitOnT.Length > 1 && splitOnT[1].Length > 0)
                {
                    var t = new TimePartialParameter(splitOnT[1], _format);
                    _hour = t.Hour;
                    _minute = t.Minute;
                    _second = t.Second;
                }
            }
        }

        private static bool SeperatorIsNotValid(string str, char validSeperator)
        {
            var badSeperator = false;
            foreach (var c in str)
            {
                if (c >= '0' && c <= '9')
                {
                    continue;
                }
                if (c != validSeperator)
                {
                    badSeperator = true;
                }
                break;
            }
            return badSeperator;
        }

        public DateTime GetDate()
        {
            if (_e != null)
            {
                throw _e;
            }
            return new DateTime(_year, _month, _dayOfMonth, _hour, _minute, _second, DateTimeKind.Unspecified);
        }
    }
}