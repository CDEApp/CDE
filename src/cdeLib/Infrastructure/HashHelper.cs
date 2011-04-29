using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using File = Alphaleonis.Win32.Filesystem.File;

namespace cdeLib.Infrastructure
{
    public class HashHelper
    {
        public static string GetMD5HashFromFileOld(string filename)
        {
            using (var md5 = new MD5CryptoServiceProvider())
            {
                var buffer = md5.ComputeHash(File.ReadAllBytes(filename));
                var sb = new StringBuilder();
                for (int i = 0; i < buffer.Length; i++)
                {
                    sb.Append(buffer[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static string GetMD5HashFromFile(string filename)
        {
            try
            {
                using (Stream stream = File.OpenRead(filename))
                {
                    using (MD5 md5 = MD5.Create())
                    {
                        var buffer = md5.ComputeHash(stream);
                        return ByteArrayToString(buffer);
                    }
                }
            }
            catch (Exception ex)
            {
                ILogger logger = new Logger();
                logger.LogException(ex,"MD5Hash");
                return null;
            }
        }

        public static string ByteArrayToString(byte[] bytes)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }

    }
}