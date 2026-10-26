using System.Collections.Generic;
using System.IO;

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
}
