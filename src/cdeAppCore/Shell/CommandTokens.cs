using System.Collections.Generic;
using System.IO;
using System.Text;

namespace cdeAppCore.Shell;

/// <summary>
/// Substitutes path tokens in a custom command's arguments template. Replacement is literal and the
/// caller's template controls quoting (e.g. <c>"{path}"</c> for paths containing spaces). The token
/// map is the single extension point for adding further tokens later.
/// </summary>
public static class CommandTokens
{
    public static string Substitute(string template, string fullPath)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var tokens = new Dictionary<string, string>
        {
            ["{path}"] = fullPath,
            ["{dir}"] = Path.GetDirectoryName(fullPath) ?? "",
            ["{filename}"] = Path.GetFileName(fullPath)
        };

        var result = template;
        foreach (var (token, value) in tokens)
        {
            result = result.Replace(token, value);
        }

        return result;
    }

    /// <summary>
    /// Splits an arguments template into argv elements (whitespace separated; single or double quotes group
    /// words and are removed), then substitutes the tokens into each element. Tokens are replaced after the
    /// split, so a path containing spaces or quotes stays one intact argument.
    /// </summary>
    public static List<string> SplitAndSubstitute(string template, string fullPath)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(template))
        {
            return result;
        }

        var current = new StringBuilder();
        var inWord = false;
        var quote = '\0';
        foreach (var c in template)
        {
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                else current.Append(c);
            }
            else if (c is '"' or '\'')
            {
                quote = c;
                inWord = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (inWord)
                {
                    result.Add(Substitute(current.ToString(), fullPath));
                    current.Clear();
                    inWord = false;
                }
            }
            else
            {
                current.Append(c);
                inWord = true;
            }
        }

        if (inWord)
        {
            result.Add(Substitute(current.ToString(), fullPath));
        }

        return result;
    }
}
