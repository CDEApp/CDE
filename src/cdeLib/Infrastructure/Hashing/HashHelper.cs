using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace cdeLib.Infrastructure.Hashing;

public class HashHelper
{
    private readonly ILogger _logger;
    // CA1859 suppressed: deliberately the interface so the hash algorithm stays swappable (see IHashAlgorithm).
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance",
        Justification = "Hash algorithm abstraction; kept as an interface so it can be swapped or substituted.")]
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// bytesToHash is zero or negative. A zero-length partial hash would be the hash of empty input for every
    /// file (all colliding), so a bad first-pass size fails fast instead of producing meaningless hashes.
    /// </exception>
    public async ValueTask<HashResponse> GetHashResponseFromFile(string filename, int? bytesToHash)
    {
        if (bytesToHash <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesToHash), bytesToHash,
                "The partial hash size (first pass size) must be greater than zero.");
        }

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

            // Never ask for more than the file holds: a short file is read in one call with no extra read at EOF.
            var bufferSize = (int)Math.Min(bytesToHash.Value, fileLength);
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                // Partial hash is exactly the first N bytes (or the whole file when shorter), read once.
                // Issue #37: the old loop re-read the next block over the first one.
                var totalBytesRead = await FillAsync(new HandleReader(handle), rentedBuffer.AsMemory(0, bufferSize));

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
    public static ValueTask<int> FillBufferAsync(Func<Memory<byte>, long, ValueTask<int>> read, Memory<byte> buffer)
        => FillAsync(new DelegateReader(read), buffer);

    private interface IPositionalReader
    {
        ValueTask<int> ReadAsync(Memory<byte> destination, long fileOffset);
    }

    // Struct readers keep the hot path free of per-file closure/delegate allocations (issues #35/#36).
    private readonly struct HandleReader(SafeFileHandle handle) : IPositionalReader
    {
        public ValueTask<int> ReadAsync(Memory<byte> destination, long fileOffset)
            => RandomAccess.ReadAsync(handle, destination, fileOffset);
    }

    private readonly struct DelegateReader(Func<Memory<byte>, long, ValueTask<int>> read) : IPositionalReader
    {
        public ValueTask<int> ReadAsync(Memory<byte> destination, long fileOffset) => read(destination, fileOffset);
    }

    private static async ValueTask<int> FillAsync<TReader>(TReader reader, Memory<byte> buffer)
        where TReader : struct, IPositionalReader
    {
        var filled = 0;
        while (filled < buffer.Length)
        {
            var n = await reader.ReadAsync(buffer.Slice(filled), filled);
            if (n <= 0)
            {
                break;
            }

            filled += n;
        }

        return filled;
    }

    private static async ValueTask<ulong> HashWholeFile(SafeFileHandle handle)
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
