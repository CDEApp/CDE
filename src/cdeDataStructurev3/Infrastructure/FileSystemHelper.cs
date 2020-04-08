using System.Collections.Generic;
using System.IO;

namespace cdeDataStructure3.Infrastructure
{
    public static class FileSystemHelper
    {
        public static IList<string> GetFilesWithExtension(string path, string extension)
        {
            var pattern = "*." + extension;
            return Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly);
        }
    }
}
