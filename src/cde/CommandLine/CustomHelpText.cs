using System;
using CommandLine;
using CommandLine.Text;

namespace cde.CommandLine;

public class CustomHelpText
{
    /// <summary>
    /// Custom Help Text which is essential the default, but we strip the extra line space between verbs.
    /// </summary>
    /// <param name="result"></param>
    /// <typeparam name="T"></typeparam>
    public static void DisplayHelp<T>(ParserResult<T> result)
    {
        var helpText = HelpText.AutoBuild(result, h =>
        {
            h.AdditionalNewLineAfterOption = false;
            return HelpText.DefaultParsingErrorsHandler(result, h);
        }, e => e, verbsIndex: true); // verbIndex
        Console.WriteLine(helpText);
    }
}