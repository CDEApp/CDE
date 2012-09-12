using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using cdeLib.Infrastructure;
using ProtoBuf;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileMode = Alphaleonis.Win32.Filesystem.FileMode;
using FileAcces = Alphaleonis.Win32.Filesystem.FileAccess;
using Filesystem = Alphaleonis.Win32.Filesystem;
using Volume = Alphaleonis.Win32.Filesystem.Volume;

namespace cdeLib
{
    // 
    // TODO - RootEntry needs All the Flags.
    // TODO - maybe RootEntry derives from DirEntry ? collapse CE and DE maybe ?
    // 

    [DebuggerDisplay("Path = {Path}, Count = {Children.Count}")]
    [ProtoContract]
    public class RootEntry : CommonEntry
    {
        private readonly IConfiguration _configuration;
        const string MatchAll = "*";

        public uint DirCount { get; set; }

        public uint FileCount { get; set; }

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

        public string ActualFileName { get; set; }

        public double ScanDurationMilliseconds
        {
            get { return (ScanEndUTC - ScanStartUTC).TotalMilliseconds; }
        }

        public RootEntry ()
        {
            Children = new List<DirEntry>();
            PathsWithUnauthorisedExceptions = new List<string>();
            _configuration = new Configuration();
            EntryCountThreshold = _configuration.ProgressUpdateInterval;
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
                throw new ArgumentException(string.Format("Cannot find path \"{0}\"", startPath));
            }
            string deviceHint;
            string volumeName;
            string volRoot;
            DefaultFileName = GetDefaultFileName(startPath, out deviceHint, out volRoot, out volumeName);

            Path = startPath;
            DriveLetterHint = deviceHint;
            VolumeName = volumeName;

            var dsi = Volume.GetDiskFreeSpace(Path);
            AvailSpace = dsi.FreeBytesAvailable;
            TotalSpace = dsi.TotalNumberOfBytes;
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

        public void SetSummaryFields()
        {
            var dirStats = new DirStats();
            SetSummaryFields(dirStats);
            DirCount = dirStats.DirCount;
            FileCount = dirStats.FileCount;
        }

        public string GetDefaultFileName(string scanPath, out string hint, out string volumeRoot, out string volumeName)
        {
            string fileName;

            volumeRoot = GetDirectoryRoot(scanPath);
            volumeName = GetVolumeName(volumeRoot);

            hint = GetDriverLetterHint(scanPath, volumeRoot);

            var filenameSafePath = SafeFileName(scanPath);
            if (IsUnc(scanPath))
            {
                fileName = string.Format("{0}-{1}.cde", hint, filenameSafePath.Substring(2));
            }
            else
            {
                if (volumeRoot == scanPath)
                {
                    fileName = string.Format("{0}-{1}.cde", hint, volumeName);
                }
                else
                {
                    fileName = string.Format("{0}-{1}-{2}.cde", hint, volumeName, filenameSafePath);
                }
            }
            return fileName;
        }

        #region Methods virtual to assist testing.
        public virtual string GetFullPath(string path)
        {
            return AlphaFSHelper.GetFullPath(path);
        }

        public virtual bool IsPathRooted(string path)
        {
            return Filesystem.Path.IsPathRooted(path);
        }

        public virtual bool IsUnc(string path)
        {
            return Filesystem.Path.IsUnc(path);
        }

        public virtual string GetDirectoryRoot(string path)
        {
            return AlphaFSHelper.GetDirectoryRoot(path);
        }

        public virtual string GetVolumeName(string rootPath)
        {
            var alphasFSroot = Directory.GetDirectoryRoot(rootPath);    // hacky hacky 
            return Volume.GetVolumeInformation(alphasFSroot).Name;
        }
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
                if (!path.EndsWith(Filesystem.Path.DirectorySeparatorChar))
                {
                    path = path + Filesystem.Path.DirectorySeparatorChar;
                }
            }
            else
            {
                if (path.EndsWith(Filesystem.Path.DirectorySeparatorChar) && volumeRoot != path)
                {
                    path = path.TrimEnd(Filesystem.Path.DirectorySeparatorChar.ToCharArray());
                }
                path = char.ToUpper(path[0]) + path.Substring(1);
            }
            
            return path;
        }


        public string GetDriverLetterHint(string path, string volumeRoot)
        {
            string hint;
            if (IsUnc(path))
            {
                hint = "UNC";
            }
            else
            {
                hint = volumeRoot.Substring(0, 1);
            }
            return hint;
        }

        public static string SafeFileName(string path)
        {
            return path.Replace('\\', '_')
                        .Replace('$', '_')
                        .Replace(':', '_');
        }

        /// <summary>
        /// This version recurses itself so it can cache the folders and the node in its own stack.
        /// This improves performance.
        /// </summary>
        public void RecurseTree(string startPath)
        {
            var entryCount = 0;
            var dirs = new Stack<Tuple<CommonEntry,string>>();
            startPath = new PathInfo(startPath).GetLongPath();
            dirs.Push(Tuple.Create((CommonEntry)this, startPath));
            while (dirs.Count > 0)
            {
                var t = dirs.Pop();
                var commonEntry = t.Item1;
                var directory = t.Item2;
                var fsEntries = Directory.GetFullFileSystemEntries
                    (null, directory, MatchAll, SearchOption.TopDirectoryOnly, false, exceptionHandler, null);
                foreach (var fsEntry in fsEntries)
                {
                    var dirEntry = new DirEntry(fsEntry);
                    commonEntry.Children.Add(dirEntry);
                    if (fsEntry.IsDirectory)
                    {
                        dirs.Push(Tuple.Create((CommonEntry)dirEntry, fsEntry.FullPath));
                    }
                    ++entryCount;
                    if (entryCount > EntryCountThreshold)
                    {
                        if (SimpleScanCountEvent != null)
                        {
                            SimpleScanCountEvent();
                        }
                        entryCount = 0;
                    }
                }
            }
            if (SimpleScanEndEvent != null)
            {
                SimpleScanEndEvent();
            }
        }

        public int EntryCountThreshold { get; set; }

        public Action SimpleScanCountEvent { get; set; }

        public Action SimpleScanEndEvent { get; set; }

        private EnumerationExceptionDecision exceptionHandler(string path, Exception e)
        {
            if (e.GetType().Equals(typeof(UnauthorizedAccessException)))
            {
                PathsWithUnauthorisedExceptions.Add(path);
                return EnumerationExceptionDecision.Skip;
            }

            if (ExceptionEvent != null)
            {
                ExceptionEvent(path, e);
            }
            return EnumerationExceptionDecision.Abort;
        }

        public Action<string, Exception> ExceptionEvent { get; set; }

        //public CommonEntry FindDir(string basePath, string entryPath)
        //{
        //    var relativePath = entryPath.GetRelativePath(basePath);
        //    if (relativePath == null)
        //    {
        //        throw new ArgumentException("Error entryPath must be logically under basePath.");
        //    }

        //    if (relativePath == string.Empty)
        //    {
        //        return this;
        //    }

        //    return FindClosestParentDir(relativePath);
        //}

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
                var rootEntry = Serializer.Deserialize<RootEntry>(input);
                return rootEntry;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static List<RootEntry> LoadCurrentDirCache()
        {
	        return Load(GetCacheFileList(new[] {"."}));
        }

		public static List<RootEntry> Load(IEnumerable<string> cdeList)
		{
			return cdeList.Select(LoadDirCache).ToList();
		}

        public static RootEntry LoadDirCache(string file)
        {
            if (File.Exists(file))
            {
                try
                {
                    using (var fs = File.Open(file, FileMode.Open, FileAcces.Read))
                    {
                        var re = Read(fs);
                        if (re != null)
                        {
                            re.ActualFileName = file;
                            re.SetInMemoryFields();
                        }
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

		public static IList<string> GetCacheFileList(IEnumerable<string> paths)
		{
			var cacheFilePaths = new List<string>();
			foreach (var path in paths)
			{
				cacheFilePaths.AddRange(GetCdeFiles(path));

				var childDirs = Directory.GetDirectories(path);
				foreach (var childPath in childDirs)
				{
					cacheFilePaths.AddRange(GetCdeFiles(childPath));
				}
			}
			return cacheFilePaths;
		}

		private static IEnumerable<string> GetCdeFiles(string path)
		{
			return AlphaFSHelper.GetFilesWithExtension(path, "cde");
		}

        #region List of UAE paths on a known win7 volume - probably decent example
        #pragma warning disable 169
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
        #pragma warning restore 169
        #endregion
    }
}