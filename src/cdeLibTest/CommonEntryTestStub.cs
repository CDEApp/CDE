using System.Collections.Generic;
using cdeLib;

namespace cdeLibTest
{
    public class CommonEntryTestStub : CommonEntry
    {
        // TODO not sure initialise Children best way for test.
        public CommonEntryTestStub()
        {
            Children = new List<DirEntry>();
        }
    }
}