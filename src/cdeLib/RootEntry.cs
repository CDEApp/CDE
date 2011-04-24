#define MSGZIP
//#define GZIP
//#define BZIP2
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Alphaleonis.Win32.Filesystem;
//using ICSharpCode.SharpZipLib.BZip2;
//using ICSharpCode.SharpZipLib.GZip;
using ProtoBuf;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileMode = Alphaleonis.Win32.Filesystem.FileMode;
using Path = Alphaleonis.Win32.Filesystem.Path;
using Volume = Alphaleonis.Win32.Filesystem.Volume;

namespace cdeLib
{
    [ProtoContract]
    public class RootEntry : CommonEntry
    {
        const string MatchAll = "*";

        [ProtoMember(2, IsRequired = true)]
        public string RootPath { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public string VolumeName { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public string Description { get; set; } // user entered description ?

        /// <summary>
        /// There are a standard set on C: drive in win7 do we care about them ? Hmmmmm filter em out ? or hold internal filter to filter em out ojn display optionally.
        /// </summary>
        [ProtoMember(5, IsRequired = true)]
        public IList<string> PathsWithUnauthorisedExceptions { get; set; }

        [ProtoMember(6, IsRequired = true)]
        public string DefaultFileName { get; set; }

        [ProtoMember(7, IsRequired = true)]
        public string DriveLetterHint { get; set; }

        [ProtoMember(8, IsRequired = true)]
        public ulong AvailSpace { get; set; }

        [ProtoMember(9, IsRequired = true)]
        public ulong UsedSpace { get; set; }

        [ProtoMember(10, IsRequired = true)]
        public DateTime ScanStartUTC { get; set; }

        [ProtoMember(11, IsRequired = true)]
        public DateTime ScanEndUTC { get; set; }

        public RootEntry ()
        {
            PathsWithUnauthorisedExceptions = new List<string>();    
        }

        public void PopulateRoot(string startPath)
        {
            if (!Directory.Exists(startPath))
            {
                throw new ArgumentException(string.Format("Cannot find path \"{0}\"", startPath));
            }
            string deviceHint;
            string volumeName;
            string volRoot;
            DefaultFileName = GetDefaultFileName(startPath, out deviceHint, out volRoot, out volumeName);

            DriveLetterHint = deviceHint;
            VolumeName = volumeName;

            var dsi = Volume.GetDiskFreeSpace(RootPath);
            AvailSpace = dsi.FreeBytesAvailable;
            UsedSpace = dsi.TotalNumberOfBytes;

            ScanStartUTC = DateTime.UtcNow;
            RecurseTree(startPath);
            ScanEndUTC = DateTime.UtcNow;

            SetSummaryFields();
        }

        public string GetDefaultFileName(string scanPath, out string hint, out string volumeRoot, out string volumeName)
        {
            string fileName;

            scanPath = FullPath(scanPath);  // Fully qualified path used to generate filename
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
                {   // how to make a nice name out of path ?
                    fileName = string.Format("{0}-{1}_.cde", hint, filenameSafePath);
                }
            }
            return fileName;
        }

        #region Methods virtual to assist testing.
        public virtual string FullPath(string path)
        {
            return Path.GetFullPath(path);
        }

        public virtual bool IsPathRooted(string path)
        {
            return Path.IsPathRooted(path);
        }

        public virtual bool IsUnc(string path)
        {
            return IsUnc(path);
        }

        public virtual string GetDirectoryRoot(string path)
        {
            return Directory.GetDirectoryRoot(path);
        }

        public virtual string GetVolumeName(string rootPath)
        {
            return Volume.GetVolumeInformation(rootPath).Name;
        }
        #endregion

        public string GetDriverLetterHint(string path, string volumeRoot)
        {
            string hint;
            if (IsUnc(path))
            {
                hint = "UNC";
            }
            else
            {
                if (volumeRoot == path)
                {
                    hint = volumeRoot.Substring(0, 1);
                }
                else
                {
                    hint = "PATH";
                }
            }
            return hint;
        }

        public string SafeFileName(string path)
        {
            return path.Replace('\\', '_')
                        .Replace('$', '_')
                        .Replace(':', '_');
        }

        public string GetDefaultFileName(string scanPath, VolumeInfo volInfo)
        {
            return "";
        }

 
        /// <summary>
        /// This version recurses itself so it can cache the folders and the node in tree.
        /// This improves performance when building the tree enormously.
        /// </summary>
        public void RecurseTree(string startPath)
        {
            var dirs = new Stack<Tuple<CommonEntry,string>>();
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
                }
            }
        }

        private EnumerationExceptionDecision exceptionHandler(string path, Exception e)
        {
            if (e.GetType().Equals(typeof(UnauthorizedAccessException)))
            {
                PathsWithUnauthorisedExceptions.Add(path);
                return EnumerationExceptionDecision.Skip;
            }
            return EnumerationExceptionDecision.Abort;
        }

        public CommonEntry FindDir(string basePath, string entryPath)
        {
            var relativePath = entryPath.GetRelativePath(basePath);
            if (relativePath == null)
            {
                throw new ArgumentException("Error entryPath must be logically under basePath.");
            }

            if (relativePath == string.Empty)
            {
                return this;
            }

            return FindClosestParentDir(relativePath);
        }

        public void SaveRootEntry()
        {
            using(var newFS = File.Open(DefaultFileName, FileMode.Create))
            {
                Write(newFS);
            }
        }

        public void Write(Stream output)
        {
            #if BZIP2
            using (var bzip2Stream = new BZip2OutputStream(output, 1))
            {
                Serializer.Serialize(bzip2Stream, this);
            }
            #elif GZIP
            using (var gzipStream = new GZipOutputStream(output))
            {
                Serializer.Serialize(gzipStream, this);
            }
            #elif MSGZIP
            using (var gzipStream = new GZipStream(output, CompressionMode.Compress, false))
            {
                Serializer.Serialize(gzipStream, this);
            }
            #else
            Serializer.Serialize(output, this);
            #endif
        }

        public static RootEntry Read(Stream input)
        {
            RootEntry re;
            #if BZIP2
            using (var bzip2InputStream = new BZip2InputStream(input))
            {
                re = Serializer.Deserialize<RootEntry>(bzip2InputStream);
            }
            #elif GZIP
            using (var gzipInputStream = new GZipInputStream(input))
            {
                re = Serializer.Deserialize<RootEntry>(gzipInputStream);
            }
            #elif MSGZIP
            using (var gzipInputStream = new GZipStream(input, CompressionMode.Decompress, false))
            {
                re = Serializer.Deserialize<RootEntry>(gzipInputStream);
            }
            #else
            re = Serializer.Deserialize<RootEntry>(input);
            #endif
            return re;
        }

        public uint FindEntries(string find)
        {
            return FindEntries(find, RootPath);
        }

        public static List<RootEntry> LoadCurrentDirCache()
        {
            var roots = new List<RootEntry>();
            var files = Directory.GetFiles(".", "*.cde", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                using (var fs = File.Open(file, FileMode.Open))
                {
                    var re = Read(fs);
                    roots.Add(re);
                }
            }
            return roots;
        }


        #region List of UAE paths on a known win7 volume - probably decent example
        #pragma warning disable 169
        // ReSharper disable InconsistentNaming
        private List<string> knownUAEpattern = new List<string>(100)
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

        private List<string> knownUAE = new List<string>(100)
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

        #region other implementations that are slower and or not worth using
        #if (LEFTOVERCODE)
        public bool RecurseTree1(string basePath)
        {
            var exceptionList = new List<Type>
                {
                    typeof (UnauthorizedAccessException)
                };

            var entries = Directory.GetFullFileSystemEntries
                (null, basePath, MatchAll, SearchOption.AllDirectories, false, null, exceptionList);
            foreach (var entry in entries)
            {
                FindDir(RootPath, entry.FullPath).Children.Add(new DirEntry(entry));
            }
            return true;
        }

        // version that manages recurse itself, so it can remember parent folders to build tree.
        // this is heaps quicker and covers all files, unlike theRecurseTree() above..
        //   dont understand why stuff is missing here ?
        public bool RecurseTree2(string basePath)
        {
            var dirs = new Stack<string>();
            dirs.Push(basePath);
            while (dirs.Count > 0)
            {
                var path = dirs.Pop();
                var ce = FindDir(RootPath, path);

                var entries = Directory.GetFullFileSystemEntries(path, MatchAll, SearchOption.TopDirectoryOnly);
                foreach (var entry in entries)
                {
                    ce.Children.Add(new DirEntry(entry));
                    if (entry.IsDirectory)
                    {
                        dirs.Push(entry.FullPath);
                    }
                }
            }
            return true;
        }

        public void BuildList(int startSize)
        {
            var exceptionList = new List<Type>
                {
                    typeof (UnauthorizedAccessException)
                };

            //var list = new List<string>(startSize);
            List = new List<DirEntry>(startSize);
            var entries = Directory.GetFullFileSystemEntries//(RootPath, MatchAll, SearchOption.AllDirectories);
                (null, RootPath, MatchAll, SearchOption.AllDirectories, false, null, exceptionList);
            foreach (var entry in entries)
            {
                //list.Add(entry.FullPath);
                List.Add(DirEntry.GetDirEntryFullPath(entry));
            }
            Console.WriteLine(" list " + List.Count);
        }
        #endif
        #endregion
    }
}