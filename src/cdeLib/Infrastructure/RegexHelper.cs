using System;
using System.Text.RegularExpressions;

namespace cdeLib.Infrastructure;

public static class RegexHelper
{
    /// <summary>
    /// Empty when the pattern compiles. A null or empty pattern is not an error: search treats it as no
    /// name filter, the same as in the other modes.
    /// </summary>
    public static string GetRegexErrorMessage(string testPattern)
    {
        if (string.IsNullOrEmpty(testPattern)) return string.Empty;
        try
        {
            _ = Regex.Match("", testPattern, RegexOptions.None, BoundedRegex.MatchTimeout);
            return string.Empty;
        }
        catch (ArgumentException ae)
        {
            return $"Bad Regex: {ae.Message}.";
        }
    }
}