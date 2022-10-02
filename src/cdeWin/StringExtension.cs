using System;

namespace cdeWin;

public static class StringExtension
{
    public static string ToHRString(this long val)
    {
        if (val == 0)
        {
            return "0";
        }
        string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB" };
        var place = Convert.ToInt32(Math.Floor(Math.Log(val, 1024)));
        var num = Math.Round(val / Math.Pow(1024, place), 1);
        return num + " " + suf[place];
    }
}