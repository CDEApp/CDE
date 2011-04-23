using System;
using System.Collections.Generic;
using System.IO;
using Alphaleonis.Win32.Filesystem;
using cde;
using ProtoBuf;
using Directory = Alphaleonis.Win32.Filesystem.Directory;

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
        public string Description { get; set; }

        /// <summary>
        /// There are a standard set on C: drive in win7 do we care about them ? Hmmmmm filter em out ? or hold internal filter to filter em out ojn display optionally.
        /// </summary>
        [ProtoMember(5, IsRequired = true)]
        public IList<string> PathsWithUnauthorisedExceptions { get; set; }

        //[ProtoMember(7, IsRequired = true)]
        //public IList<DirEntry> List { get; set; }

        //List<Type> exceptionList = new List<Type>
        //    {
        //        typeof (UnauthorizedAccessException)
        //    };

        public RootEntry ()
        {
            PathsWithUnauthorisedExceptions = new List<string>();    
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

        public override void Write(Stream output)
        {
            Serializer.Serialize(output, this);
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