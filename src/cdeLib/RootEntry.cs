using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeLib.Infrastructure;
using ProtoBuf;

namespace cdeLib
{
    // TODO - RootEntry needs All the Flags.
    // TODO - maybe RootEntry derives from DirEntry ? collapse CE and DE maybe ?
    [DebuggerDisplay("Path = {Path}, Count = {Children.Count}")]
    [ProtoContract]
    public class RootEntry : DirEntry // CommonEntry
    {
        const string MatchAll = "*";

        // NO LONGER USED
        [Obsolete]
        [ProtoMember(1, IsRequired = true)]
        public string VolumeName { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public string Description { get; set; } // user entered description ?

        /// <summary>
        /// There are a standard set on C: drive in win7 do we care about them ? Hmmmmm filter em out ? or hold internal filter to filter em out ojn display optionally.
        /// </summary>
        [ProtoMember(3, IsRequired = true)]
        public IList<string> PathsWithUnauthorisedExceptions { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public string DefaultFileName { get; set; }

        [ProtoMember(5, IsRequired = true)]
        public string DriveLetterHint { get; set; }

        [ProtoMember(6, IsRequired = true)]
        public ulong AvailSpace { get; set; }

        [ProtoMember(7, IsRequired = true)]
        public ulong TotalSpace { get; set; }

        [ProtoMember(8, IsRequired = true)]
        public DateTime ScanStartUTC { get; set; }

        [ProtoMember(9, IsRequired = true)]
        public DateTime ScanEndUTC { get; set; }

        [ProtoMember(10, IsRequired = true)] // need to save for new data model.
        public int RootIndex;  // hackery with Entry and EntryStore

        [ProtoMember(11, IsRequired = true)] // hackery to not load old files ?
        public int Version= 2;

        public string ActualFileName { get; set; }

        public double ScanDurationMilliseconds => (ScanEndUTC - ScanStartUTC).TotalMilliseconds;

        public RootEntry()
            : base(true)
        {
            Children = new List<DirEntry>();
            PathsWithUnauthorisedExceptions = new List<string>();
            IConfiguration configuration = new Configuration();
            EntryCountThreshold = configuration.ProgressUpdateInterval;
            RootEntry = this;
        }

        public void PopulateRoot(string startPath)
        {
            startPath = GetRootEntry(startPath);
            ScanStartUTC = DateTime.UtcNow;
            RecurseTree(startPath);
            ScanEndUTC = DateTime.UtcNow;
            SetInMemoryFields();
        }

        public string GetRootEntry(string startPath)
        {
            startPath = CanonicalPath(startPath);
            if (!Directory.Exists(startPath))
            {
                throw new ArgumentException($"Cannot find path \"{startPath}\"");
            }

            DefaultFileName = GetDefaultFileName(startPath, out var deviceHint, out _);
            Path = startPath;
            DriveLetterHint = deviceHint;
            // VolumeName = volumeName;

            var pathRoot = System.IO.Path.GetPathRoot(startPath);
            var driveInfo = new DriveInfo(pathRoot);
            AvailSpace = (ulong) driveInfo.AvailableFreeSpace;
            TotalSpace = (ulong) driveInfo.TotalSize;
            return startPath;
        }

        public void SetInMemoryFields()
        {
            // protobuf does not retain DateKind.
            // So just handle it here
            ScanStartUTC = new DateTime(ScanStartUTC.Ticks, DateTimeKind.Utc);
            ScanEndUTC = new DateTime(ScanEndUTC.Ticks, DateTimeKind.Utc);
            FullPath = Path;
            SetCommonEntryFields();
            SetSummaryFields();
        }

        public string GetDefaultFileName(string scanPath, out string hint, out string volumeRoot)
        {
            string fileName;
            volumeRoot = GetDirectoryRoot(scanPath);
            // volumeName = GetVolumeName(volumeRoot);
            hint = GetDriverLetterHint(scanPath, volumeRoot);
            var filenameSafePath = SafeFileName(scanPath);
            if (IsUnc(scanPath))
            {
                fileName = $"{hint}-{filenameSafePath.Substring(2)}.cde";
            }
            else
            {
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (volumeRoot == scanPath)
                {
                    fileName = $"{hint}.cde";
                }
                else
                {
                    fileName = $"{hint}-{filenameSafePath}.cde";
                }
            }
            return fileName;
        }

        #region Methods virtual to assist testing.
        // TODO may not be needed with move to dotnetcore3.0 and System.IO
        public virtual string GetFullPath(string path)
        {
            return System.IO.Path.GetFullPath(path);
        }

        // UNCLEAR what this is here for? so commenting out for now.
        // public virtual bool IsPathRooted(string path)
        // {
        //     // return Filesystem.Path.IsPathRooted(path);
        //     return Path.;
        // }

        public virtual bool IsUnc(string path)
        {
            return System.IO.Path.IsPathFullyQualified(path) && path.StartsWith("\\\\");
        }

        public virtual string GetDirectoryRoot(string path)
        {
            return Directory.GetDirectoryRoot(path);
        }

        // VolumeName is a windows specific thing....
        // QUESTION: delete this field entirely
        // public virtual string GetVolumeName(string rootPath)
        // {
        //     var pathRoot = System.IO.Path.GetPathRoot(rootPath);
        //     // var driveInfo = new DriveInfo(pathRoot);
        //     return pathRoot;
        // }
        #endregion

        /// <summary>
        /// Return canoncial version of path.
        /// Ensure device id are upper case.
        /// if ends in a '\' and its not just a device eg G:\ then strip trailing \
        /// </summary>
        public string CanonicalPath(string path)
        {
            path = GetFullPath(path);  // Fully qualified path used to generate filename
            var volumeRoot = GetDirectoryRoot(path);
            if (IsUnc(path))
            {
                if (!System.IO.Path.EndsInDirectorySeparator(path))
                {
                    path += System.IO.Path.DirectorySeparatorChar;
                }
            }
            else
            {
                // if (path.EndsWith(Filesystem.Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture)) && volumeRoot != path)
                if (System.IO.Path.EndsInDirectorySeparator(path) && volumeRoot != path)
                {
                    path = path.TrimEnd(System.IO.Path.DirectorySeparatorChar);
                }
                path = char.ToUpper(path[0]) + path.Substring(1);
            }
            
            return path;
        }


        public string GetDriverLetterHint(string path, string volumeRoot)
        {
            var hint = IsUnc(path) ? "UNC" : volumeRoot.Substring(0, 1);
            return hint;
        }

        /// <summary>
        /// Safe for windows which is the least forgiving filesystem I currently believe.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string SafeFileName(string path)
        {
            return path.Replace('\\', '_')
                        .Replace('$', '_')
                        .Replace(':', '_');
        }

        /// <summary>
        /// This version calls itself so it can cache the folders and the node in its own stack.
        /// This improves performance.
        /// </summary>
        public void RecurseTree(string startPath)
        {
            var entryCount = 0;
            var dirs = new Stack<(CommonEntry, string)>();
            // startPath = Filesystem.Path.GetLongPath(startPath);
            dirs.Push((this, startPath));
            while (dirs.Count > 0)
            {
                var (commonEntry, directory) = dirs.Pop();
                var dirInfo = new DirectoryInfo(directory);
                var fsInfos = dirInfo.EnumerateFileSystemInfos(MatchAll, SearchOption.TopDirectoryOnly);
                foreach (var fsInfo in fsInfos)
                {
                    var dirEntry = new DirEntry(fsInfo);
                    commonEntry.Children.Add(dirEntry);
                    if (dirEntry.IsDirectory)
                    {
                        dirs.Push((dirEntry, fsInfo.FullName));
                    }
                    ++entryCount;
                    if (entryCount > EntryCountThreshold)
                    {
                        SimpleScanCountEvent?.Invoke();
                        entryCount = 0;
                    }
                    if (Hack.BreakConsoleFlag) { break; }
                }
                if (Hack.BreakConsoleFlag) { break; }
            }

            SimpleScanEndEvent?.Invoke();
        }

        public int EntryCountThreshold { get; set; }

        public Action SimpleScanCountEvent { get; set; }

        public Action SimpleScanEndEvent { get; set; }

        public Action<string, Exception> ExceptionEvent { get; set; }

        public void SaveRootEntry()
        {
            using(var newFs = File.Open(DefaultFileName, FileMode.Create))
            {
                Write(newFs);
            }
        }

        public void Write(Stream output)
        {
            Serializer.Serialize(output, this);
        }

        public static RootEntry Read(Stream input)
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

        public static List<RootEntry> LoadCurrentDirCache()
        {
	        return Load(GetCacheFileList(new[] {"./"}));
        }

		public static List<RootEntry> Load(IEnumerable<string> cdeList)
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

        public static RootEntry LoadDirCache(string file)
        {
            if (File.Exists(file))
            {
                try
                {
                    using (var fs = File.Open(file, FileMode.Open, FileAccess.Read))
                    {
                        var re = Read(fs);
                        if (re == null) return null;
                        re.ActualFileName = file;
                        re.SetInMemoryFields();
                        return re;
                    }
                }
                // ReSharper disable EmptyGeneralCatchClause
                catch (Exception) { }
				// ReSharper restore EmptyGeneralCatchClause
			}
            return null;
        }

        /// <summary>
        /// Set FullPath on all Directories.
        /// Set ParentCommonEntry on all Entries in tree with a parent.
        /// </summary>
        public void SetCommonEntryFields()
        {
            TraverseTreePair((p, d) =>
                {
                    if (d.IsDirectory)
                    {
                        d.FullPath = p.MakeFullPath(d);
                    }
                    d.ParentCommonEntry = p;
                    return true;
                });
        }

        /// <summary>
        /// Testing to see if this helps gc reuse mem on reload.
        /// </summary>
        public void ClearCommonEntryFields()
        {
            TraverseTreePair((p, d) =>
            {
                if (d.IsDirectory)
                {
                    d.FullPath = null;
                }
                d.ParentCommonEntry = null;
                return true;
            });
        }

        public void SortAllChildrenByPath()
        {
            Children.Sort((de1, de2) => de1.PathCompareWithDirTo(de2)); // Sort root entries first.
            IsDefaultSort = true;

            TraverseTreePair((p, d) =>
            {
                if (d.IsDirectory)
                {
                    if (d.Children != null && d.Children.Count > 1)
                    {
                        d.Children.Sort((de1, de2) => de1.PathCompareWithDirTo(de2));
                        d.IsDefaultSort = true;
                    }
                }
                return true;
            });
        }

		public int DescriptionCompareTo(RootEntry re, IConfigCdeLib config)
        {
            if (re == null)
            {
                return -1; // this before re
            }
            if (Description == null && re.Description != null)
            {
                return -1; // this before re
            }
            if (Description != null && re.Description == null)
            {
                return 1; // this after re
            }
			return config.CompareWithInfo(Description, re.Description);
        }

        /// <summary>
        /// This gets .cde files in current dir or one directory down.
        /// Use directory permissions to control who can load what .cde files one dir down if you like.
        /// </summary>
		public static IList<string> GetCacheFileList(IEnumerable<string> paths)
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
				    catch (Exception) { } // if cant list folders don't care.
				}
			}
			return cacheFilePaths;
		}

		private static IEnumerable<string> GetCdeFiles(string path)
		{
			return FSHelper.GetFilesWithExtension(path, "cde");
		}

        #region List of UAE paths on a known win7 volume - probably decent example
        #pragma warning disable 169
        /*
        // ReSharper disable InconsistentNaming
        private static List<string> knownUAEpattern = new List<string>(100)
            {
                @"%windows%\System32\LogFiles\WMI\RtBackup",
                @"%windows%\CSC\v2.0.6",
                @"%userprofilepath%\%user%\Templates",
                @"%userprofilepath%\%user%\Start Menu",
                @"%userprofilepath%\%user%\SendTo",
                @"%userprofilepath%\%user%\Recent",
                @"%userprofilepath%\%user%\PrintHood",
                @"%userprofilepath%\%user%\NetHood",
                @"%userprofilepath%\%user%\My Documents",
                @"%userprofilepath%\%user%\Local Settings",
                @"%userprofilepath%\%user%\Documents\My Videos",
                @"%userprofilepath%\%user%\Documents\My Pictures",
                @"%userprofilepath%\%user%\Documents\My Music",
                @"%userprofilepath%\%user%\Cookies",
                @"%userprofilepath%\%user%\Application Data",
                @"%userprofilepath%\%user%\AppData\Local\Temporary Internet Files",
                @"%userprofilepath%\%user%\AppData\Local\History",
                @"%userprofilepath%\%user%\AppData\Local\Application Data",
                @"%userprofilepath%\%user%\NetHood",
                @"%userprofilepath%\Default User",

                // @"C:\Users\All Users\" is same as @"C:\ProgramData\"
                @"%AllUsersProfile%\Templates", // yet another profile root variant.

                @"C:\ProgramData\Templates",
                @"C:\ProgramData\Start Menu",
                @"C:\ProgramData\Favorites",
                @"C:\ProgramData\Documents",
                @"C:\ProgramData\Desktop",
                @"C:\ProgramData\Application Data",


                @"C:\System Volume Information",
                @"C:\Documents and Settings",                                                   
            };

        private static List<string> knownUAE = new List<string>(100)
            {
                @"C:\Windows\System32\LogFiles\WMI\RtBackup",
                @"C:\Windows\CSC\v2.0.6",
                @"C:\Users\robtest\Templates",
                @"C:\Users\robtest\Start Menu",
                @"C:\Users\robtest\SendTo",
                @"C:\Users\robtest\Recent",
                @"C:\Users\robtest\PrintHood",
                @"C:\Users\robtest\NetHood",
                @"C:\Users\robtest\My Documents",
                @"C:\Users\robtest\Local Settings",
                @"C:\Users\robtest\Documents\My Videos",
                @"C:\Users\robtest\Documents\My Pictures",
                @"C:\Users\robtest\Documents\My Music",
                @"C:\Users\robtest\Cookies",
                @"C:\Users\robtest\Application Data",
                @"C:\Users\robtest\AppData\Local\Temporary Internet Files",
                @"C:\Users\robtest\AppData\Local\History",
                @"C:\Users\robtest\AppData\Local\Application Data",
                @"C:\Users\rluiten\Templates",
                @"C:\Users\rluiten\Start Menu",
                @"C:\Users\rluiten\SendTo",
                @"C:\Users\rluiten\Recent",
                @"C:\Users\rluiten\PrintHood",
                @"C:\Users\rluiten\NetHood",
                @"C:\Users\rluiten\My Documents",
                @"C:\Users\rluiten\Local Settings",
                @"C:\Users\rluiten\Application Data",
                @"C:\Users\rluiten\AppData\Local\Temporary Internet Files",
                @"C:\Users\rluiten\AppData\Local\History",
                @"C:\Users\Public\Documents\My Videos",
                @"C:\Users\Public\Documents\My Pictures",
                @"C:\Users\Public\Documents\My Music",
                @"C:\Users\Default User",
                @"C:\Users\Default\Templates",
                @"C:\Users\Default\Start Menu",
                @"C:\Users\Default\SendTo",
                @"C:\Users\Default\Recent",
                @"C:\Users\Default\PrintHood",
                @"C:\Users\Default\NetHood",
                @"C:\Users\Default\My Documents",
                @"C:\Users\Default\Local Settings",
                @"C:\Users\Default\Documents\My Videos",
                @"C:\Users\Default\Documents\My Pictures",
                @"C:\Users\Default\Documents\My Music",
                @"C:\Users\Default\Cookies",
                @"C:\Users\Default\Application Data",
                @"C:\Users\Default\AppData\Local\Temporary Internet Files",
                @"C:\Users\Default\AppData\Local\History",
                @"C:\Users\Default\AppData\Local\Application Data",
                @"C:\Users\All Users\Templates",
                @"C:\Users\All Users\Start Menu",
                @"C:\Users\All Users\Favorites",
                @"C:\Users\All Users\Documents",
                @"C:\Users\All Users\Desktop",
                @"C:\Users\All Users\Application Data",
                @"C:\System Volume Information",
                @"C:\ProgramData\Templates",
                @"C:\ProgramData\Start Menu",
                @"C:\ProgramData\Favorites",
                @"C:\ProgramData\Documents",
                @"C:\ProgramData\Desktop",
                @"C:\ProgramData\Application Data",
                @"C:\Documents and Settings",
            };
        // ReSharper restore InconsistentNaming
        */
        #pragma warning restore 169
        #endregion
    }
}