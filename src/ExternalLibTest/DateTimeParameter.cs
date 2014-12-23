using System;
using System.Diagnostics.CodeAnalysis;

namespace ExternalLibTest
{
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
    [SuppressMessage("ReSharper", "ConvertToConstant.Local")]
    public class DateTimeParameter
    {
        // "YYYY-Month-DDTHH:MM:SS" example

        private int _year;
        private int _month = 1; // 1-12
        private int _dayOfMonth = 1; // 1-31
        private int _hour = 0; // 0-23
        private int _minute = 0; // 0-59
        private int _second = 0; // 0-59
        private Exception _e;

        public DateTimeParameter(string str)
        {
            var splitOnDash = str.Split('-');

            int year;
            int.TryParse(splitOnDash[0], out year);
            if (year < 1000)
            {
                _e = new ArgumentException("Require Year parameter be a 4 Digit Year as part of format \"YYYY-Month-DDTHH:MM:SS\"");
                return;
            }
            _year = year;

            if (splitOnDash.Length > 1) // may have month specified
            {
                if (splitOnDash[1].Length == 0) // just a - is allowed and ignore. just return year.
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
                    _e = new ArgumentException("Require Month parameter as digits 1-12 or month name as part of format \"YYYY-Month-DDTHH:MM:SS\"");
                    return;
                }
                _month = month;
            }

            string[] splitOnT;

            if (splitOnDash.Length > 2) // may have dayOfMonth specified
            {
                if (splitOnDash[2].Length == 0) // just a - is allowed and ignore. just return year and month
                {
                    return;
                }

                splitOnT = splitOnDash[2].Split('T');

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
                    _e = new ArgumentException("Require Day parameter as digits 1-31 and valid for month as part of format \"YYYY-Month-DDTHH:MM:SS\"");
                    return;
                }
                _dayOfMonth = dayOfMonth;
            }
            // hand over processing to TimeParameter ?
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