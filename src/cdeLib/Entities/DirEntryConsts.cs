using System.Globalization;

namespace cdeLib.Entities
{
    public static class DirEntryConsts
    {
        public static CompareOptions MyCompareOptions = CompareOptions.IgnoreCase | CompareOptions.StringSort;
        public static readonly CompareInfo MyCompareInfo = CompareInfo.GetCompareInfo("en-US");
    }
}