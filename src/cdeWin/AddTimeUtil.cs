using System;

namespace cdeWin
{
    public delegate DateTime AddTimeUnitFunc(DateTime dateTime, int units);

    public class AddTimeUtil
    {
        public static DateTime AddMinute(DateTime dateTime, int count)
        {
            return dateTime.AddMinutes(count);
        }

        public static DateTime AddHour(DateTime dateTime, int count)
        {
            return dateTime.AddHours(count);
        }

        public static DateTime AddDay(DateTime dateTime, int count)
        {
            return dateTime.AddDays(count);
        }

        public static DateTime AddMonth(DateTime dateTime, int count)
        {
            return dateTime.AddMonths(count);
        }

        public static DateTime AddYear(DateTime dateTime, int count)
        {
            return dateTime.AddYears(count);
        }
    }
}
