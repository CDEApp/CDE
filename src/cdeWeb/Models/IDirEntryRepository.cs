using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace cdeWeb.Models
{
    public interface IDirEntryRepository
    {
        IEnumerable<DirEntry> Entries { get; }
        IEnumerable<DirEntry> GetAll();
        DirEntry Get(int id);
        IEnumerable<DirEntry> GetQuery(string query);
    }

    public class DirEntryRepository : IDirEntryRepository
    {
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        DirEntry[] _entries = new[] 
        {
            new DirEntry { Name = "Tomato Soup", Size = 1, Path = @"C:\Tomato Soup", Modified = new DateTime(2013, 01, 01, 09, 10, 11, DateTimeKind.Local) },
            new DirEntry { Name = "Yo-yo", Size = 3, Path = @"C:\Yo-yo", Modified = new DateTime(2013, 01, 02, 09, 10, 12, DateTimeKind.Local) },
            new DirEntry { Name = "Hammer", Size = 4, Path = @"C:\Hammer", Modified = new DateTime(2013, 01, 03, 09, 10, 13, DateTimeKind.Local) } ,
            new DirEntry { Name = "Tomato Soup2", Size = 1, Path = @"C:\Tomato Soup2", Modified = new DateTime(2013, 01, 04, 09, 10, 14, DateTimeKind.Local) },
            new DirEntry { Name = "Yo-yo2", Size = 3, Path = @"C:\Yo-yo2", Modified = new DateTime(2013, 01, 04, 09, 10, 15, DateTimeKind.Local) },
            new DirEntry { Name = "Hammer2", Size = 4, Path = @"C:\Hammer2", Modified = new DateTime(2013, 01, 05, 09, 10, 16, DateTimeKind.Local) }
        };
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        public IEnumerable<DirEntry> Entries { get { return _entries;  } }

        public IEnumerable<DirEntry> GetAll()
        {
            return _entries;
        }

        public DirEntry Get(int id)
        {
            throw new HttpResponseException(HttpStatusCode.NotImplemented);
        }

        // todo idea
        // different end point for regex ? use name of param 'regex', 'substring', 'magic'
        public IEnumerable<DirEntry> GetQuery(string query)
        {
            var entry = _entries.Where(p => p.Name.Contains(query));
            // ReSharper disable PossibleMultipleEnumeration
            if (!entry.Any())
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
            return entry;
            // ReSharper restore PossibleMultipleEnumeration
        }
    }
}