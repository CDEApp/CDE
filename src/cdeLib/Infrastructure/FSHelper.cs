using System.Collections.Generic;
using System.IO;

namespace cdeLib.Infrastructure
{
    public static class FSHelper
    {
        public static IList<string> GetFilesWithExtension(string path, string extension)
        {
            var pattern = "*." + extension;
            return Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly);
        }
    }
}
