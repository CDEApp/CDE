using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

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

    /// <summary>
    /// Hash a file using a raw file handle and positional reads into a pooled buffer (no FileStream).
    /// bytesToHash null is a full hash, otherwise a partial hash of that many bytes.
    /// </summary>
    public async ValueTask<HashResponse> GetHashResponseFromFile(string filename, int? bytesToHash)
    {
        try
        {
            using SafeFileHandle handle = File.OpenHandle(filename, FileMode.Open, FileAccess.Read, FileShare.Read,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var fileLength = RandomAccess.GetLength(handle);

            if (bytesToHash == null)
            {
                var fullHash = await HashWholeFile(handle);
                return new HashResponse(true, fullHash, false, fileLength);
            }

            var bufferSize = bytesToHash.Value;
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                // Partial hash is exactly the first N bytes (or the whole file when shorter), read once.
                // Issue #37: the old loop re-read the next block over the first one.
                var totalBytesRead = await FillBufferAsync(
                    (memory, offset) => RandomAccess.ReadAsync(handle, memory, offset),
                    rentedBuffer.AsMemory(0, bufferSize));

                var partialHash = _hashAlgorithm.Hash(rentedBuffer.AsSpan(0, totalBytesRead));
                return new HashResponse(true, partialHash, fileLength > bytesToHash, totalBytesRead);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
        catch (FileLoadException) // if doing hashing on system drive cant open files don't care.
        {
            return HashResponse.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($" original filename \"{filename}\"");
            _logger.LogException(ex, "Hash");
            return HashResponse.Failed;
        }
    }

    /// <summary>
    /// Fill <paramref name="buffer"/> from the start of the file, looping over short reads until the buffer is
    /// full or end of file. Returns the number of bytes placed in the buffer.
    /// </summary>
    /// <param name="read">Positional read: (destination, fileOffset) returning bytes read, 0 at end of file.</param>
    public static async ValueTask<int> FillBufferAsync(Func<Memory<byte>, long, ValueTask<int>> read, Memory<byte> buffer)
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var n = await read(buffer.Slice(filled), filled);
            if (n <= 0)
            {
                break;
            }

            filled += n;
        }

        return filled;
    }

    private async ValueTask<ulong> HashWholeFile(SafeFileHandle handle)
    {
        var state = new MurMurHash3.Incremental(MurmurHashWrapper.Seed);
        var buffer = ArrayPool<byte>.Shared.Rent(MurMurHash3.StreamChunkSize);
        try
        {
            long position = 0;
            int read;
            while ((read = await RandomAccess.ReadAsync(handle, buffer.AsMemory(0, MurMurHash3.StreamChunkSize), position)) > 0)
            {
                state.Append(buffer.AsSpan(0, read));
                position += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return state.Finish();
    }
}
