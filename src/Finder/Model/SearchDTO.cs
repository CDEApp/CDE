using System;

namespace Finder.Model
{
    public class SearchDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public DateTime ModifiedDate { get; set; }
    }
}