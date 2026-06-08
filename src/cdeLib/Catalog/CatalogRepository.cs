using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Entities;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Serialization;
using FlatSharp;
using MessagePack;
using ProtoBuf;
using Serilog;
using SerilogTimings;
using ILogger = Serilog.ILogger;

namespace cdeLib.Catalog;

public sealed class CatalogRepository : ICatalogRepository, IDisposable
{
    private SerializerProtocol _serializerProtocol = SerializerProtocol.MessagePack; // hard coded for now.
    private readonly ILogger _logger;
    private static readonly BufferPool BufferPool = new();
    private readonly FileStreamManager _fileStreamManager = FileStreams.Instance;
    private bool _disposed;

    public CatalogRepository(ILogger logger)
    {
        _logger = logger;
    }

    public RootEntry Read(string file)
    {
        try
        {
            using var input = File.OpenRead(file);

            switch (_serializerProtocol)
            {
                case SerializerProtocol.Protobuf:
                    return Serializer.Deserialize<RootEntry>(input);
                case SerializerProtocol.Flatbuffers:
                    byte[] bytes;
                    using (Operation.Time("ReadStream"))
                    {
                        // Read stream efficiently - avoids ToByteArray() overhead
                        // FlatSharp can parse from byte[], ReadOnlyMemory<byte>, or ReadOnlySpan<byte>
                        bytes = new byte[input.Length];
                        input.ReadExactly(bytes);
                    }

                    using (Operation.Time("Deserialize"))
                    {
                        var serializer = new FlatBufferSerializer(new FlatBufferSerializerOptions());
                        // Use ReadOnlyMemory<byte> overload to avoid defensive copy
                        return serializer.Parse<RootEntry>(bytes.AsMemory());
                    }
                case SerializerProtocol.MessagePack:
                    return MessagePackSerializer.Deserialize<RootEntry>(input, MessagePackConfig.Options);

                default:
                    throw new Exception("Invalid Serializer Protocol");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error Reading catalogue {FileName}", file);
            throw;
        }
    }

    public async Task<RootEntry> ReadAsync(string file)
    {
        try
        {
            switch (_serializerProtocol)
            {
                case SerializerProtocol.Protobuf:
                    await using (var input = _fileStreamManager.CreateReadStream(file))
                    {
                        return Serializer.Deserialize<RootEntry>(input);
                    }
                case SerializerProtocol.Flatbuffers:
                    var bytes = await _fileStreamManager.ReadAllBytesOptimizedAsync(file);
                    using (Operation.Time("Deserialize"))
                    {
                        var serializer = new FlatBufferSerializer(
                            new FlatBufferSerializerOptions());
                        return serializer.Parse<RootEntry>(bytes);
                    }
                case SerializerProtocol.MessagePack:
                    // Read file into memory first (async I/O), then deserialize on thread pool.
                    // This is faster than DeserializeAsync with a stream because:
                    // 1. No async state machine overhead during CPU-bound deserialization
                    // 2. MessagePack can work directly on the memory buffer without internal buffering
                    // Task.Run ensures deserialization doesn't block the UI thread.
                    var msgPackBytes = await _fileStreamManager.ReadAllBytesOptimizedAsync(file);
                    return await Task.Run(() =>
                        MessagePackSerializer.Deserialize<RootEntry>(msgPackBytes, MessagePackConfig.Options));

                default:
                    throw new Exception("Invalid Serializer Protocol");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error Reading catalogue {FileName}", file);
            throw;
        }
    }

    public IList<RootEntry> Load(IList<string> cdeList)
    {
        using (Operation.Time("Loading Catalogs {Count}", cdeList.Count))
        {
            var results = new ConcurrentBag<RootEntry>();
            Parallel.ForEach(cdeList, file =>
            {
                var newRootEntry = LoadDirCache(file);
                if (newRootEntry != null)
                {
                    results.Add(newRootEntry);
                }

                _logger.Information("Catalog [{file}] read on ThreadId: {ThreadId}", file,
                    Thread.CurrentThread.ManagedThreadId);
            });

            return results.ToList();
        }
    }

    public async Task<IList<RootEntry>> LoadAsync(IList<string> cdeList)
    {
        using (Operation.Time("Loading Catalogs Async {Count}", cdeList.Count))
        {
            var tasks = cdeList.Select(async file =>
            {
                var rootEntry = await LoadDirCacheAsync(file);
                if (rootEntry != null)
                {
                    _logger.Information("Catalog [{file}] read on ThreadId: {ThreadId}", file,
                        Environment.CurrentManagedThreadId);
                }
                return rootEntry;
            }).ToList();

            var results = await Task.WhenAll(tasks);
            return results.Where(r => r != null).ToList()!;
        }
    }

    public IList<RootEntry> LoadCurrentDirCache()
    {
        return LoadAsync(GetCacheFileList(["./"])).GetAwaiter().GetResult();
    }

    /// <summary>
    /// This gets .cde files in the current dir or one directory down.
    /// Use directory permissions to control who can load what .cde files one dir down if you like.
    /// </summary>
    public IList<string> GetCacheFileList(IEnumerable<string> paths)
    {
        var cacheFilePaths = CollectionPool.GetStringList();
        try
        {
            foreach (var path in paths)
            {
                cacheFilePaths.AddRange(GetCdeFiles(path));

                foreach (var childPath in Directory.GetDirectories(path))
                {
                    try
                    {
                        cacheFilePaths.AddRange(GetCdeFiles(childPath));
                    }
                    // ReSharper disable once EmptyGeneralCatchClause
                    catch
                    {
                    } // if cant list folders don't care.
                }
            }

            // Return a new list to avoid pool corruption, since this list will be used externally
            return new List<string>(cacheFilePaths);
        }
        finally
        {
            CollectionPool.ReturnStringList(cacheFilePaths);
        }
    }

    private static IEnumerable<string> GetCdeFiles(string path)
    {
        return FileSystemHelper.GetFilesWithExtension(path, "cde");
    }

    public IList<string> GetColumnarFileList(IEnumerable<string> paths)
    {
        var result = new List<string>();
        foreach (var path in paths)
        {
            result.AddRange(FileSystemHelper.GetFilesWithExtension(path, "cdex"));

            foreach (var childPath in Directory.GetDirectories(path))
            {
                try
                {
                    result.AddRange(FileSystemHelper.GetFilesWithExtension(childPath, "cdex"));
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                } // if cant list folders don't care.
            }
        }

        return result;
    }

    public RootEntry LoadDirCache(string file)
    {
        if (!File.Exists(file)) return null;
        try
        {
            var rootEntry = Read(file);
            if (rootEntry == null) return null;
            rootEntry.ActualFileName = file;
            rootEntry.SetInMemoryFields();
            return rootEntry;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error Reading file");
            throw;
        }
    }

    public async Task<RootEntry> LoadDirCacheAsync(string file)
    {
        if (!File.Exists(file)) return null;
        try
        {
            var rootEntry = await ReadAsync(file);
            if (rootEntry == null) return null;
            rootEntry.ActualFileName = file;
            rootEntry.SetInMemoryFields();
            return rootEntry;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error Reading file");
            throw;
        }
    }

    public async Task Save(RootEntry rootEntry)
    {
        var fileName = rootEntry.ActualFileName ?? rootEntry.DefaultFileName;
        switch (_serializerProtocol)
        {
            case SerializerProtocol.Protobuf:
                await using (var newFs = _fileStreamManager.CreateWriteStream(fileName))
                {
                    Serializer.Serialize(newFs, rootEntry);
                }

                break;
            case SerializerProtocol.Flatbuffers:
                var maxBytesNeeded = FlatBufferSerializer.Default.GetMaxSize(rootEntry);
                var buffer = new byte[maxBytesNeeded];
                FlatBufferSerializer.Default.Serialize(rootEntry, buffer);
                await _fileStreamManager.WriteAllBytesOptimizedAsync(fileName, buffer);
                break;
            case SerializerProtocol.MessagePack:
                // Use ArrayBufferWriter to avoid intermediate byte[] allocation
                var bufferWriter = new ArrayBufferWriter<byte>();
                MessagePackSerializer.Serialize(bufferWriter, rootEntry, MessagePackConfig.Options);
                await _fileStreamManager.WriteAllBytesOptimizedAsync(fileName, bufferWriter.WrittenMemory);
                break;
            default:
                throw new Exception("Invalid Serializer Protocol");
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose of managed resources
                BufferPool?.Clear();
                // Note: FileStreamManager is a singleton, don't dispose of it here
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}