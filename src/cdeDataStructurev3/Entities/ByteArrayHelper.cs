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
            for (var i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }

            return sb.ToString();
        }
    }
}