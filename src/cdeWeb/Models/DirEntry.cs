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

    }
}

/*

 * var searchResultTest = [
    {Name: 'moo1', Size: 10, Modified: 'adate1', Path: 'C:\\Here'},
    {Name: 'moo2', Size: 12, Modified: 'adate2', Path: 'C:\\'},
    {Name: 'Here', Size: 10, Modified: 'adate3', Path: 'C:\\'},
    {Name: 'Deep1', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate'},
    {Name: 'Deep2 Six', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate'},
    {Name: 'Deep3 Six', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate'},
    {Name: 'Deep4 Six', Size: 13, Modified: 'adate3', Path: 'C:\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate\\BigLong\\Stuff\\With\\A\\Longis\\Long\\Path\\To\\Demonstrate'}
];

*/