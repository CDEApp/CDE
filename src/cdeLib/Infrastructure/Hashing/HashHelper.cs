using System;
using System.IO;
using System.Security.Cryptography;

namespace cdeLib.Infrastructure.Hashing
{
    public class HashResponse
    {
        public string HashAsString
        {
            get {
                if (Hash == null)
                    return null;
                return ByteArrayHelper.ByteArrayToString(Hash);
            }
        }

        public byte[] Hash { get; set; }
        public bool IsPartialHash { get; set; }
        public long BytesHashed { get; set; }
    }

    public class HashHelper
    {
        /// <summary>
        ///   Overload for GetMD5HashResponseFromFile(filename, bytesToHash);
        /// </summary>
        /// <param name = "filename"></param>
        /// <param name = "bytesToHash"></param>
        /// <returns></returns>
        public byte[] GetMD5HashFromFile(string filename, int bytesToHash)
        {
            return GetMD5HashResponseFromFile(filename, bytesToHash).Hash;
        }

        public static HashResponse GetMD5HashResponseFromFile(string filename, int bytesToHash)
        {
            var hashResponse = new HashResponse();
            try
            {
                using (Stream stream = File.OpenRead(filename))
                {
                    var buf = new byte[bytesToHash];
                    int bytesRead = stream.Read(buf, 0, buf.Length);
                    long totalBytesRead = bytesRead;
                    while (bytesRead > 0 && totalBytesRead <= bytesToHash)
                    {
                        bytesRead = stream.Read(buf, 0, buf.Length);
                        totalBytesRead += bytesRead;
                    }
                    hashResponse.BytesHashed = totalBytesRead;
                    hashResponse.IsPartialHash = stream.Length > bytesToHash;

                    using (var md5 = MD5.Create())
                    {
                        hashResponse.Hash = md5.ComputeHash(buf);
                    }
                }
                return hashResponse;
            }
            catch (FileLoadException)   // if doing hasing on system drive cant open files dont care.
            {
                return null;
            }
            catch (Exception ex)
            {
                ILogger logger = new Logger();
                logger.LogDebug(
                    $"                                                                                                        original filename \"{filename}\"");
                logger.LogException(ex, "MD5Hash");
                return null;
            }
        }

        public static HashResponse GetMD5HashFromFile(string filename)
        {
            try
            {
                using (Stream stream = File.OpenRead(filename))
                {
                    var hashResponse = new HashResponse();
                    using (var md5 = MD5.Create())
                    {
                        hashResponse.Hash = md5.ComputeHash(stream);
                        hashResponse.IsPartialHash = false;
                        hashResponse.BytesHashed += stream.Length;
                        return hashResponse;
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

        public byte[] ReadFully(Stream stream, int initialLength)
        {
            // If we've been passed an unhelpful initial length, just use 32k
            if (initialLength < 1)
            {
                initialLength = 32768;
            }

            var buffer = new byte[initialLength];
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
                    var newBuffer = new byte[buffer.Length*2];
                    Array.Copy(buffer, newBuffer, buffer.Length);
                    newBuffer[read] = (byte) nextByte;
                    buffer = newBuffer;
                    read++;
                }
            }
            // Buffer is now too big. Shrink it.
            var ret = new byte[read];
            Array.Copy(buffer, ret, read);
            return ret;
        }
    }
}