using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using File = Alphaleonis.Win32.Filesystem.File;

namespace cdeLib.Infrastructure
{
    public class HashHelper
    {
        private IConfiguration _configuration;
        public HashHelper()
        {
            _configuration = new Configuration();

        }

        public string GetMurmerHashFromFile(string filename)
        {
            try
            {
                using (Stream stream = File.OpenRead(filename))
                {
                    IHashAlgorithm hashAlgorithm = new MurmurHash2Unsafe();
                    var num = hashAlgorithm.Hash(ReadFully(stream,_configuration.HashFirstPassSize ));
                    return num.ToString("x2");
                }
             
            }
            catch (Exception ex)
            {
                ILogger logger = new Logger();
                logger.LogException(ex, "Murmer");
                return null;
            }
        }
        public string GetMD5HashFromFile(string filename, int bytesToHash)
        {
            try
            {
                using (Stream stream = File.OpenRead(filename))
                {
                    byte[] buf = new byte[bytesToHash];
                    int bytesRead = stream.Read(buf, 0, buf.Length);
                    long totalBytesRead = bytesRead;
                    while (bytesRead > 0 && totalBytesRead <= bytesToHash)
                    {
                        bytesRead = stream.Read(buf, 0, buf.Length);
                        totalBytesRead += bytesRead;
                    }


                    using (MD5 md5 = MD5.Create())
                    {
                        //var buffer = md5.ComputeHash(stream);
                        var buffer = md5.ComputeHash(buf);
                        return ByteArrayToString(buffer);
                    }
                }
            }
            catch (Exception ex)
            {
                ILogger logger = new Logger();
                logger.LogException(ex, "MD5Hash");
                return null;
            }
        }


        public string GetMD5HashFromFile(string filename)
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
                logger.LogException(ex, "MD5Hash");
                return null;
            }
        }

        public string ByteArrayToString(byte[] bytes)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < bytes.Length; i++)
            {
                //TODO: Optimize via http://blogs.msdn.com/b/blambert/archive/2009/02/22/blambert-codesnip-fast-byte-array-to-hex-string-conversion.aspx
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public byte[] ReadFully(Stream stream, int initialLength)
        {
            // If we've been passed an unhelpful initial length, just
            // use 32K.
            if (initialLength < 1)
            {
                initialLength = 32768;
            }

            byte[] buffer = new byte[initialLength];
            int read = 0;

            int chunk;
            while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
            {
                read += chunk;

                // If we've reached the end of our buffer, check to see if there's
                // any more information
                if (read == buffer.Length)
                {
                    int nextByte = stream.ReadByte();

                    // End of stream? If so, we're done
                    if (nextByte == -1)
                    {
                        return buffer;
                    }

                    // Nope. Resize the buffer, put in the byte we've just
                    // read, and continue
                    byte[] newBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, newBuffer, buffer.Length);
                    newBuffer[read] = (byte)nextByte;
                    buffer = newBuffer;
                    read++;
                }
            }
            // Buffer is now too big. Shrink it.
            byte[] ret = new byte[read];
            Array.Copy(buffer, ret, read);
            return ret;
        }

       
    }
}