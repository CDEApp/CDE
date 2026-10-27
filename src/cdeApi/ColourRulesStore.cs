using System;
using System.IO;

namespace cdeApi;

/// <summary>
/// Persists the user's directory-entry colour rules as plain text in a single user-scoped file
/// (<c>%APPDATA%\cde\colour-rules.txt</c>, next to <c>ui-state.json</c>). The text is stored exactly as
/// typed (invalid lines included — the frontend skips them when styling). This class is also the single
/// source of the default rules text: <see cref="Read"/> creates the file from it when missing, and
/// <c>GET /colour-rules/defaults</c> serves it to the frontend's "Reset to defaults".
/// </summary>
public sealed class ColourRulesStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public ColourRulesStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cde", "colour-rules.txt"))
    {
    }

    public ColourRulesStore(string path) => _path = path;

    /// <summary>Upper bound on the rules text accepted by PUT (characters).</summary>
    public const int MaxLength = 1_000_000;

    public const string DefaultText =
        "# CDE colour rules\n" +
        "#\n" +
        "# One rule per line:  pattern  style\n" +
        "#   pattern  a JavaScript regex, case-insensitive, no slashes or flags. It ends at the first\n" +
        "#            whitespace (write a space as \\s). It is matched against the entry NAME only, for\n" +
        "#            files and folders alike. The reserved pattern [folder] matches every directory.\n" +
        "#   style    a bare colour (same as color: <value>), or CSS declarations separated by semicolons.\n" +
        "#            Allowed properties: color, background-color, font-weight, font-style,\n" +
        "#            text-decoration, opacity. Other properties are flagged and ignored.\n" +
        "#            CSS variables such as var(--dir) are allowed.\n" +
        "# The FIRST matching line wins, top to bottom. Lines starting with # are comments.\n" +
        "# Invalid lines are skipped; the valid lines keep working. Changes apply as you type.\n" +
        "#\n" +
        "# WARNING: pathological regexes (for example (a+)+$) can slow or freeze the app.\n" +
        "\n" +
        "[folder]  var(--dir)\n" +
        "\n" +
        "# Examples (remove the leading # to enable):\n" +
        "# \\.sys$               red\n" +
        "# \\.(jpg|png|gif)$     #2a9d8f\n" +
        "# ^~\\$|\\.tmp$          color: grey; font-style: italic\n";

    /// <summary>The rules text. Creates the file from <see cref="DefaultText"/> when it is missing.</summary>
    public string Read()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                WriteLocked(DefaultText);
                return DefaultText;
            }

            return File.ReadAllText(_path);
        }
    }

    /// <summary>Persist the rules text exactly as supplied.</summary>
    public void Save(string text)
    {
        lock (_gate)
        {
            WriteLocked(text);
        }
    }

    private void WriteLocked(string text)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        // Write a temp file then rename over the original, so a sidecar killed mid-autosave leaves
        // the old rules rather than an empty or partial file.
        var temp = _path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, _path, overwrite: true);
    }
}
