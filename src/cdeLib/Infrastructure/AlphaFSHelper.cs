
using System.IO;
using System.Linq;
using Directory = Alphaleonis.Win32.Filesystem.Directory;

namespace cdeLib.Infrastructure
{
    public static class AlphaFSHelper
    {
        // Bug in either AlphaFS or the Win32 either way here is a work around.
        public static string[] GetFilesWithExtension(string extension)
        {
            var pattern = "*." + extension;
            var files = Directory.GetFiles(".", pattern, SearchOption.TopDirectoryOnly);
            var checkedExtensionEndFiles = files.Where(val => val.EndsWith(extension)).ToArray();
            return checkedExtensionEndFiles;
        }
    }
}
