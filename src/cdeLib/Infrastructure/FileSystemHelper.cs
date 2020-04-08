using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace cdeLib.Infrastructure
{
    public static class FileSystemHelper
    {
        public static IEnumerable<string> GetFilesWithExtension(string path, string extension)
        {
            var pattern = "*." + extension;
            var di = new DirectoryInfo(path);
            var files = di.GetFiles(pattern, SearchOption.TopDirectoryOnly);
            return files.OrderByDescending(f => f.Length).Select(x => x.FullName);
        }
    }
}