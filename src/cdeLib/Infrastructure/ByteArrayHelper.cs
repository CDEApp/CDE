using System.Text;

namespace cdeLib.Infrastructure
{
    public static class ByteArrayHelper
    {
        public static string ByteArrayToString(byte[] bytes)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < bytes.Length; i++)
            {
                //TODO: Optimize via http://blogs.msdn.com/b/blambert/archive/2009/02/22/blambert-codesnip-fast-byte-array-to-hex-string-conversion.aspx
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}