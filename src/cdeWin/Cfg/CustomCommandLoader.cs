using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace cdeWin.Cfg;

/// <summary>
/// Loads the configured <see cref="CustomCommandOptions"/> list. If no <c>CustomCommands</c> section
/// is present, the legacy single <c>ExplorerAlt</c> setting is migrated into the list so an existing
/// "Explore Alt" tool is not lost.
/// </summary>
public static class CustomCommandLoader
{
    public static IReadOnlyList<CustomCommandOptions> Load(IConfiguration configuration)
    {
        var commands = configuration?.GetSection("CustomCommands").Get<List<CustomCommandOptions>>()
                       ?? new List<CustomCommandOptions>();

        if (commands.Count == 0 && configuration != null)
        {
            // Migrate the previous single Explore Alt setting into the custom command list.
            var alt = new ExplorerAltOptions();
            configuration.GetSection("ExplorerAlt").Bind(alt);
            if (!string.IsNullOrEmpty(alt.Path))
            {
                commands.Add(new CustomCommandOptions
                {
                    Label = "Explore Alt",
                    Command = alt.Path,
                    Arguments = alt.Arguments
                });
            }
        }

        return commands;
    }
}
