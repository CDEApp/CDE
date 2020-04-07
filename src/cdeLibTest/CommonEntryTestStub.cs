using System.Collections.Generic;
using cdeLib;

namespace cdeLibTest
{
    public class CommonEntryTestStub : DirEntry
    {
        // TODO not sure initialise Children best way for test.
        public CommonEntryTestStub()
        {
            Children = new List<DirEntry>();
        }
    }
}