using System.Text;

namespace cdeDataStructure3.Entities
{
    public static class ByteArrayHelper
    {
        public static string ByteArrayToString(byte[] bytes)
        {
            if (bytes == null)
            {
                return "null";
            }

            var sb = new StringBuilder();
            foreach (var t in bytes)
            {
                sb.Append(t.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}