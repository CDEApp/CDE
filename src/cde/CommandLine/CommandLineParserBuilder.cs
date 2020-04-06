using System;
using System.Globalization;
using CommandLine;

namespace cde.CommandLine
{
    public static class CommandLineParserBuilder
    {
        public static Parser Build()
        {
            return new Parser(cfg =>
            {
                cfg.CaseSensitive = false;
                cfg.AutoHelp = true;
                cfg.AutoVersion = true;
                cfg.ParsingCulture = CultureInfo.InvariantCulture;
                cfg.HelpWriter = Console.Error;
                try
                {
                    cfg.MaximumDisplayWidth = Console.WindowWidth;
                    if (cfg.MaximumDisplayWidth >= 1)
                        return;
                    cfg.MaximumDisplayWidth = 80;
                }
                catch
                {
                    cfg.MaximumDisplayWidth = 80;
                }
            });
        }
    }
}