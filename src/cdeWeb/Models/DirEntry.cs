using System;
using System.Diagnostics;

namespace cdeWeb.Models
{
    // api data model for DirEntry - thinking if i want to restructure upstream for this ?
    [DebuggerDisplay("{Name} {Size}")]
    public class DirEntry
    {
        public long Size { get; set; }
        public string Name { get; set; } // entry name when not RootEntry
        public string Path { get; set; } // not this includes Path. - at moment.
        public DateTime Modified { get; set; }
        //public Flags BitFields; // need ? not yet maybe not ever.

        public DirEntry()
        {
        }

        public DirEntry(cdeLib.CommonEntry parent, cdeLib.DirEntry e)
        {
            Size = e.Size;
            Name = e.Path;
            Path = parent.FullPath;
            Modified = e.Modified;
        }
    }
}
