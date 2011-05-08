using Alphaleonis.Win32.Filesystem;

namespace cdeLib.Infrastructure
{
    public class FileSystemHelper
    {
        private const int PathLengthToAvoidAlphaFsLib = 200;

        /// <summary>
        /// Same method as on RootEntry, just dont feel safe using RootEntry as the owner. ????
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetDirectoryRoot(string path)
        {
            // BUG in AlphaFS. Directory.GetDirectoryRoot()
            if (path.Length < PathLengthToAvoidAlphaFsLib)  // arbitrary number to use the system io version.
            {
                return System.IO.Directory.GetDirectoryRoot(path);
            }
            return Directory.GetDirectoryRoot(path);
        }
    }
}