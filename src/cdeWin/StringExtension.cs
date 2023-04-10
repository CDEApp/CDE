using System;

namespace cdeWin;

public static class StringExtension
{
    private static readonly string[] Suffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB" };

    public static string ToHRString(this long val)
    {
        if (val == 0)
        {
            return "0";
        }

        var place = Convert.ToInt32(Math.Floor(Math.Log(val, 1024)));
        var num = Math.Round(val / Math.Pow(1024, place), 1);
        return num + " " + Suffix[place];
    }
}