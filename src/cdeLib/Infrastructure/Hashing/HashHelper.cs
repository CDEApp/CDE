using System;
using System.IO;
using System.Threading.Tasks;

namespace cdeLib.Infrastructure.Hashing
{
    public class HashHelper
    {
        private readonly ILogger _logger;
        private readonly IHashAlgorithm _hashAlgorithm;

        public HashHelper(ILogger logger)
        {
            _logger = logger;
            _hashAlgorithm = new MurmurHashWrapper();
        }

        public async Task<HashResponse> GetHashResponseFromFile(string filename, int? bytesToHash)
        {
            var hashResponse = new HashResponse();
            try
            {
                await using (Stream stream = File.OpenRead(filename))
                {
                    long totalBytesRead;
                    var streamLength = stream.Length;
                    if (bytesToHash == null)
                    {
                        //avoid 'Array dimensions exceeded supported range', don't use byte[]
                        totalBytesRead = streamLength;
                        hashResponse.Hash = BitConverter.GetBytes(_hashAlgorithm.HashStream(stream));
                    } else
                    {
                        var buf = new byte[bytesToHash.Value];
                        var bytesRead = await stream.ReadAsync(buf, 0, buf.Length);

                        totalBytesRead = bytesRead;
                        while (bytesRead > 0 && totalBytesRead <= bytesToHash)
                        {
                            bytesRead = stream.Read(buf, 0, buf.Length);
                            totalBytesRead += bytesRead;
                        }
                        hashResponse.Hash = BitConverter.GetBytes(_hashAlgorithm.Hash(buf));
                    }

                    hashResponse.BytesHashed = totalBytesRead;
                    hashResponse.IsPartialHash = streamLength > bytesToHash;
                }

                return hashResponse;
            }
            catch (FileLoadException) // if doing hashing on system drive cant open files don't care.
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($" original filename \"{filename}\"");
                _logger.LogException(ex, "Hash");
                return null;
            }
        }
    }
}