using System;

namespace cdeLib;

/// <summary>
/// Unspecified values as per format are returned as 1 for Month and Day and Zero for other fields.
/// </summary>
public class DateTimePartialParameter
{
    private readonly string _format = "<YYYY>-<Month>-<DD>T<HH>:<MM>:<SS>";

    // a Parsing Expression Grammar might be a better way to do this.... PEG
    // - http://en.wikipedia.org/wiki/Parsing_expression_grammar

    private readonly int _year;
    private readonly int _month = 1; // 1-12
    private readonly int _dayOfMonth = 1; // 1-31
    private readonly int _hour; // 0-23
    private readonly int _minute; // 0-59
    private readonly int _second; // 0-59
    private readonly Exception _e;

    public DateTimePartialParameter(string str)
    {
        var splitOnDash = str.Split('-');

        int.TryParse(splitOnDash[0], out var year);
        if (year < 1000) // this is not 4 digits, its only value eg  4 digits 0982 is 4 digits.
        {
            _e = new ArgumentException(
                $"Require Year parameter be a 4 Digit Year <YYYY> as part of format '{_format}'");
            return;
        }
        _year = year;

        if (splitOnDash.Length > 1) // may have month specified
        {
            if (splitOnDash[1].Length == 0) // just a '-' is allowed with no value. just set year.
            {
                return;
            }

            if (!int.TryParse(splitOnDash[1], out var month))
            {
                //month = Convert.ToDateTime("01-" + p[1] + "-2000").Month;
                if (DateTime.TryParse("01-" + splitOnDash[1] + "-" + _year, out var tmp))
                {
                    month = tmp.Month;
                }
            }
            if (month == 0 || month > 12)
            {
                _e = new ArgumentException(
                    $"Require valid integer 1-12 or Month name for Month as part of format '{_format}'");
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

            // If 'T' is not used as separator between Date and Time then error
            if (SeparatorIsNotValid(splitOnDash[2], 'T'))
            {
                _e = new ArgumentException(
                    $"The separator between Date and Time must be 'T' as part of format '{_format}'");
                return;
            }

            var splitOnT = splitOnDash[2].Split('T');

            int dayOfMonth = 0;
            if (int.TryParse(splitOnT[0], out var unValidatedDayOfMonth))
            {
                // check for valid day of month
                if (DateTime.TryParse(_year + "-" + _month + "-" + unValidatedDayOfMonth, out var tmpDateTime))
                {
                    dayOfMonth = tmpDateTime.Day;
                }
            }

            if (dayOfMonth == 0 || dayOfMonth > 31)
            {
                _e = new ArgumentException(
                    $"Require valid Day of Month integer range 1-31 for Day <DD> as part of format '{_format}'");
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

    private static bool SeparatorIsNotValid(string str, char validSeparator)
    {
        var badSeparator = false;
        foreach (var c in str)
        {
            if (c >= '0' && c <= '9')
            {
                continue;
            }
            if (c != validSeparator)
            {
                badSeparator = true;
            }
            break;
        }
        return badSeparator;
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