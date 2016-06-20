using System.Collections.Generic;
using System.IO;
using Directory = Alphaleonis.Win32.Filesystem.Directory;

namespace cdeLib.Infrastructure
{
    public static class AlphaFSHelper
    {
        public static IList<string> GetFilesWithExtension(string path, string extension)
        {
            var pattern = "*." + extension;
            return Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly);
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
