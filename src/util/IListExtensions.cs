using System.Collections;

namespace Util
{
    public static class IListExtensions
    {
        public static void TruncateList(this IList iList, int max)
        {
            for (var i = iList.Count - 1; i >= max; i--)
            {
                iList.RemoveAt(i);
            }
        }
    }
}
