using System;
using System.Text.RegularExpressions;

namespace cdeLib.Infrastructure;

public static class RegexHelper
{
    public static string GetRegexErrorMessage(string testPattern)
    {
        if (testPattern?.Trim().Length > 0)
        {
            try
            {
                _ = Regex.Match("", testPattern);
            }
            catch (ArgumentException ae)
            {
                return $"Bad Regex: {ae.Message}.";
            }
        }
        else
        {
            return "Bad Regex: Pattern is Null or Empty.";
        }
        return string.Empty;
    }
}