using System;

namespace cde
{
    public static class Help
    {
        public static void ShowHelp()
        {
            Console.WriteLine(Program.Version);
            Console.WriteLine("Usage: cde --version");
            Console.WriteLine("       display version.");
            Console.WriteLine("Usage: cde --scan <path>");
            Console.WriteLine("       scans path and creates a cache file.");
            Console.WriteLine("       copies hashes from old cache file to new one if old found.");
            Console.WriteLine("Usage: cde --find <string>");
            Console.WriteLine("       uses all cache files available searches for <string>");
            Console.WriteLine("       as substring of on file name.");
            Console.WriteLine("Usage: cde --findpath <string>");
            Console.WriteLine("       uses all cache files available searches for <string>");
            Console.WriteLine("       as regex match on full path to file name.");
            Console.WriteLine("Usage: cde --grep <regex>");
            Console.WriteLine("       uses all cache files available searches for <regex>");
            Console.WriteLine("       as regex match on file name.");
            Console.WriteLine("Usage: cde --greppath <regex>");
            Console.WriteLine("       uses all cache files available searches for <regex>");
            Console.WriteLine("       as regex match on full path to file name.");
            Console.WriteLine("Usage: cde --hash ");
            Console.WriteLine("       Calculate hash (MD5) for all entries in cache file");
            Console.WriteLine("Usage: cde --dupes ");
            Console.WriteLine("       Show duplicates. Must of already run --hash first to compute file hashes");
            Console.WriteLine("Usage: cde --repl");
            Console.WriteLine("       Enter readline mode - trying it out not useful yet...");
            Console.WriteLine("Usage: cde --replGreppath <regex>");
            Console.WriteLine("Usage: cde --replGrep <regex>");
            Console.WriteLine("Usage: cde --replFind <regex>");
            Console.WriteLine("       read-eval-print loops version of the 3 find options.");
            Console.WriteLine("       This one is repl it doesnt exit unless you press enter with no search term.");
            Console.WriteLine("Usage: cde --populousfolders <minimumcount>");
            Console.WriteLine("       output folders containing more than <minimumcount> entires.");
        }
    }
}