using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeLib.Infrastructure;
using ProtoBuf;
using Serilog;

namespace cdeLib
{
    public interface ICatalogRepository
    {
        RootEntry Read(Stream input);
        IList<RootEntry> Load(IEnumerable<string> cdeList);
        IList<RootEntry> LoadCurrentDirCache();

        /// <summary>
        /// This gets .cde files in current dir or one directory down.
        /// Use directory permissions to control who can load what .cde files one dir down if you like.
        /// </summary>
        IList<string> GetCacheFileList(IEnumerable<string> paths);

        RootEntry LoadDirCache(string file);
    }

    public class CatalogRepository : ICatalogRepository
    {
        public RootEntry Read(Stream input)
        {
            try
            {
                return Serializer.Deserialize<RootEntry>(input);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public IList<RootEntry> Load(IEnumerable<string> cdeList)
        {
            var results = new ConcurrentBag<RootEntry>();
            Parallel.ForEach(cdeList, file =>
            {
                var newRootEntry = LoadDirCache(file);
                if (newRootEntry != null)
                {
                    results.Add(newRootEntry);
                }

                Console.WriteLine($"{file} read..");
            });
            return results.ToList();
        }

        public IList<RootEntry> LoadCurrentDirCache()
        {
            return Load(GetCacheFileList(new[] {"./"}));
        }

        /// <summary>
        /// This gets .cde files in current dir or one directory down.
        /// Use directory permissions to control who can load what .cde files one dir down if you like.
        /// </summary>
        public IList<string> GetCacheFileList(IEnumerable<string> paths)
        {
            var cacheFilePaths = new List<string>();
            foreach (var path in paths)
            {
                cacheFilePaths.AddRange(GetCdeFiles(path));

                foreach (var childPath in Directory.GetDirectories(path))
                {
                    try
                    {
                        cacheFilePaths.AddRange(GetCdeFiles(childPath));
                    }
                    // ReSharper disable once EmptyGeneralCatchClause
                    catch
                    {
                    } // if cant list folders don't care.
                }
            }

            return cacheFilePaths;
        }

        private static IEnumerable<string> GetCdeFiles(string path)
        {
            return FileSystemHelper.GetFilesWithExtension(path, "cde");
        }


        public RootEntry LoadDirCache(string file)
        {
            if (!File.Exists(file)) return null;
            try
            {
                using var fileStream = File.OpenRead(file);
                var rootEntry = Read(fileStream);
                if (rootEntry == null) return null;
                rootEntry.ActualFileName = file;
                rootEntry.SetInMemoryFields();
                return rootEntry;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error Reading file");
                // ignored
                return null;
            }
        }
    }
}