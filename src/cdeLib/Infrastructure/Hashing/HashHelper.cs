using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace cdeLib.Infrastructure.Hashing;

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
            await using Stream stream = File.OpenRead(filename);
            long totalBytesRead;
            var streamLength = stream.Length;
            if (bytesToHash == null)
            {
                //avoid 'Array dimensions exceeded supported range', don't use byte[]
                totalBytesRead = streamLength;
                hashResponse.Hash = BitConverter.GetBytes(_hashAlgorithm.HashStream(stream));
            }
            else
            {
                // Rent buffer from pool to avoid allocation
                byte[] rentedBuffer = null;
                try
                {
                    var bufferSize = bytesToHash.Value;
                    rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    var buf = rentedBuffer.AsMemory(0, bufferSize);

                    var bytesRead = await stream.ReadAsync(buf);

                    totalBytesRead = bytesRead;
                    while (bytesRead > 0 && totalBytesRead <= bytesToHash)
                    {
                        bytesRead = stream.Read(rentedBuffer, 0, bufferSize);
                        totalBytesRead += bytesRead;
                    }

                    // Hash only the actual bytes read, using Span-based overload
                    var actualBytesToHash = (int)Math.Min(totalBytesRead, bufferSize);
                    hashResponse.Hash = BitConverter.GetBytes(_hashAlgorithm.Hash(rentedBuffer.AsSpan(0, actualBytesToHash)));
                }
                finally
                {
                    if (rentedBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(rentedBuffer);
                    }
                }
            }

            hashResponse.BytesHashed = totalBytesRead;
            hashResponse.IsPartialHash = streamLength > bytesToHash;

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