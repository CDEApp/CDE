using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeLib.Extensions;
using cdeLib.Infrastructure;
using FlatSharp;
using FlatSharp.Unsafe;
using MessagePack;
using ProtoBuf;
using Serilog;
using SerilogTimings;
using ILogger = Serilog.ILogger;

namespace cdeLib.Catalog
{
    public class CatalogRepository : ICatalogRepository
    {
        private readonly SerializerProtocol _serializerProtocol = SerializerProtocol.MessagePack; //hard coded for now.
        private readonly ILogger _logger;

        public CatalogRepository(ILogger logger)
        {
            _logger = logger;
        }

        public RootEntry Read(Stream input)
        {
            try
            {
                byte[] bytes;
                switch (_serializerProtocol)
                {
                    case SerializerProtocol.Protobuf:
                        return Serializer.Deserialize<RootEntry>(input);
                    case SerializerProtocol.Flatbuffers:

                        using (Operation.Time("ToByteArray"))
                        {
                            bytes = input.ToByteArray(); //todo can we leverage Span<> Memory<> etc here.??
                        }

                        using (Operation.Time("Deserialize"))
                        {
                            var serializer = new FlatBufferSerializer(
                                new FlatBufferSerializerOptions(FlatBufferDeserializationOption.GreedyMutable));
                            return serializer.Parse<RootEntry>(new UnsafeArrayInputBuffer(bytes));
                        }
                    case SerializerProtocol.MessagePack:
                        bytes = input.ToByteArray();
                        return MessagePackSerializer.Deserialize<RootEntry>(bytes);

                    default:
                        throw new Exception("Invalid Serializer Protocol");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error Reading catalogue");
                return null;
            }
        }

        public IList<RootEntry> Load(IEnumerable<string> cdeList)
        {
            var results = new ConcurrentBag<RootEntry>();
            Parallel.ForEach(cdeList, file =>
            {
                var newRootEntry = LoadDirCache(file);
                if (newRootEntry != null)
                {
                    results.Add(newRootEntry);
                }

                Console.WriteLine($"{file} read..");
            });
            return results.ToList();
        }

        public IList<RootEntry> LoadCurrentDirCache()
        {
            return Load(GetCacheFileList(new[] {"./"}));
        }

        /// <summary>
        /// This gets .cde files in current dir or one directory down.
        /// Use directory permissions to control who can load what .cde files one dir down if you like.
        /// </summary>
        public IList<string> GetCacheFileList(IEnumerable<string> paths)
        {
            var cacheFilePaths = new List<string>();
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

            return cacheFilePaths;
        }

        private static IEnumerable<string> GetCdeFiles(string path)
        {
            return FileSystemHelper.GetFilesWithExtension(path, "cde");
        }


        public RootEntry LoadDirCache(string file)
        {
            if (!File.Exists(file)) return null;
            try
            {
                using var fileStream = File.OpenRead(file);
                var rootEntry = Read(fileStream);
                if (rootEntry == null) return null;
                rootEntry.ActualFileName = file;
                rootEntry.SetInMemoryFields();
                return rootEntry;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error Reading file");
                return null;
            }
        }

        public void SaveRootEntry(RootEntry rootEntry)
        {
            switch (_serializerProtocol)
            {
                case SerializerProtocol.Protobuf:
                    using (var newFs = File.Open(rootEntry.DefaultFileName, FileMode.Create))
                    {
                        Serializer.Serialize(newFs, rootEntry);
                    }

                    break;
                case SerializerProtocol.Flatbuffers:
                    var maxBytesNeeded = FlatBufferSerializer.Default.GetMaxSize(rootEntry);
                    var buffer = new byte[maxBytesNeeded];
                    FlatBufferSerializer.Default.Serialize(rootEntry, buffer);
                    File.WriteAllBytes(rootEntry.DefaultFileName, buffer);
                    break;
                case SerializerProtocol.MessagePack:
                    var bytes = MessagePackSerializer.Serialize(rootEntry);
                    File.WriteAllBytes(rootEntry.DefaultFileName, bytes);
                    break;
                default:
                    throw new Exception("Invalid Serializer Protocol");
            }
        }
    }
}