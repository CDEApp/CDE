﻿using System.Collections.Generic;
using System.IO;

namespace cdeLib.Infrastructure
{
    public static class FileSystemHelper
    {
        public static IEnumerable<string> GetFilesWithExtension(string path, string extension)
        {
            var pattern = "*." + extension;
            return Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly);
        }
    }
}