
using System.IO;
using System.Linq;
using Directory = Alphaleonis.Win32.Filesystem.Directory;

namespace cdeLib.Infrastructure
{
    public static class AlphaFSHelper
    {
        // Bug in either AlphaFS or the Win32 either way here is a work around.
        public static string[] GetFilesWithExtension(string path, string extension)
        {
            var pattern = "*." + extension;
            var files = Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly);
            var checkedExtensionEndFiles = files.Where(val => val.EndsWith(extension)).ToArray();
            return checkedExtensionEndFiles;
        }

        // BUG in AlphaFS. Path.FullGetPath()
        private const int PathLengthToAvoidAlphaFsLib = 200;
        public static string GetFullPath(string path)
        {
            if (path.Length < PathLengthToAvoidAlphaFsLib)  // arbitrary number to use the system io version.
            {
                return System.IO.Path.GetFullPath(path);
            }
            return Path.GetFullPath(path);
        }

        // BUG in AlphaFS. Directory.GetDirectoryRoot()
        public static string GetDirectoryRoot(string path)
        {
            if (path.Length < PathLengthToAvoidAlphaFsLib)  // arbitrary number to use the system io version.
            {
                return System.IO.Directory.GetDirectoryRoot(path);
            }
            return Directory.GetDirectoryRoot(path);
        }
    }
}
