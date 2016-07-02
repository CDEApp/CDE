using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using LZ4;
using Lz4Net;
using NSpec;
using cdeLib;


// ReSharper disable InconsistentNaming
namespace cdeLibSpec
{
    /// <summary>
    /// test loading and saving performance of .cde files.
    /// also try out compression and its affects.
    /// try builtin gzip ? zlib stuff of .Net - did this before it wasnt so good.
    /// try lz4 [claimed fastest out there[
    /// </summary>
    [Tag("performance_test")]
    class loadsave_cde_performance : nspec
    {
        public loadsave_cde_performance()
        {
            Console.WriteLine($"loadsave_cde_performance directory name {TestDirectory}");
            Directory.SetCurrentDirectory(TestDirectory);
            
            // Can't find test files if this line is run. so this disables test
            Directory.SetCurrentDirectory(@"C:\");
        }

        public class Result
        {
            public Stream stream;
            public RootEntry root;
        }

        public struct scenario
        {
            public int count;
            public long originalSize;
            public long writtenSize;
            public long writeDuration;
            public long readDuration;
            public long writeFileDuration;
            public long readFileDuration;
            public long writeFileSSDDuration;
            public long readFileSSDDuration;
            public string fqFileName;
            public long sampleCount;

            public string desc;
            public string compressCode;
            public bool isSerialising;
            public RootEntry root;
            public Action<RootEntry, Stream> write;
            public Func<Stream, Result> read;

            public void writer(Stream stream)
            {
                write(root, stream);
            }

            public int counter()
            {
                if (count == 0 && root != null)
                {
                    count = CommonEntry.GetDirEntries(root).Count();
                }
                return count;
            }

            public void printer()
            {
                if (sampleCount > 0)
                {
                    Console.WriteLine(
                        $"\"{desc}\", {compressCode}, {sampleCount}, {count}, {originalSize}, {writtenSize}, {writeDuration}, {readDuration}, {writeFileDuration}, {readFileDuration}, {writeFileSSDDuration}, {readFileSSDDuration}");
                }
            }
        }


        //private const string PathToTest = @"..\..\..\..\test\";
        private const string PathToTest = @"..\..\..\..\..\test\";
        private const string TestCatalog32K = PathToTest + "G-SN750B_02_S13UJ1NQ221583.cde";
        private const string TestCatalog200K = PathToTest + "C-V3Win7.cde";
        private const string TestCatalog1_2M = PathToTest + "D-SM15T_2_1.cde";
        public const int SampleCount = 5;
        public const int OneHundredMB = 100*1024*1024; // 100 MB is big enough for test files i have.

        public byte[] buffer = new byte[OneHundredMB];
        //public byte[] buffer2 = new byte[OneHundredMB];

        public class Info
        {
#pragma warning disable 0649            
            public RootEntry root;
            public int count;
            public long durationMsec;
#pragma warning restore 0649
        }

        public void describe_performance_test_compression_of_cde()
        {
            //wtf();
            //specify = () => "gumby".should_be("gumby");

            //describe["long perf test"] = () => {
            //it["runs performance tests"] = () => { 
            //    Console.WriteLine("performance testing.");
            //    specify = () => "gumby".should_be("gumby");
            //};

            RootEntry tiny = null;
            tiny = RootEntry.LoadDirCache(TestCatalog32K);
            RootEntry small = null;
            small = RootEntry.LoadDirCache(TestCatalog200K);
            RootEntry large = null;
            large = RootEntry.LoadDirCache(TestCatalog1_2M);
            
            scenario[] Scenarios =
            {
                new scenario {
                        desc = "Serialize tiny to MemStream", fqFileName = "tinyNone.cde",
                        isSerialising = true, compressCode = "None", 
                        write = writeStream, root = tiny,
                        read = readStream
                    },
                new scenario {
                        desc = "Serialize small to MemStream", fqFileName = "smallNone.cde",
                        isSerialising = true, compressCode = "None",
                        write = writeStream, root = small,
                        read = readStream
                    },
                new scenario {
                        desc = "Serialize large to MemStream", fqFileName = "largeNone.cde",
                        isSerialising = true, compressCode = "None",
                        write = writeStream, root = large,
                        read = readStream
                    },

                new scenario {
                        desc = "Serialize tiny to Gzip MemStream", fqFileName = "tinyGzip.cde",
                        isSerialising = true, compressCode = "Gzip",
                        write = writeGzipStream, root = tiny,
                        read = readGzipStream
                    },
                new scenario {
                        desc = "Serialize small to Gzip MemStream", fqFileName = "smallGzip.cde",
                        isSerialising = true, compressCode = "Gzip",
                        write = writeGzipStream, root = small,
                        read = readGzipStream
                    },
                new scenario {
                        desc = "Serialize large to Gzip MemStream", fqFileName = "largeGzip.cde",
                        isSerialising = true, compressCode = "Gzip",
                        write = writeGzipStream, root = large,
                        read = readGzipStream
                    },

                // to similar to Gzip to bother testing
                //new scenario {
                //        desc = "Serialize tiny to Deflate MemStream", fqFileName = "tinyDefl.cde",
                //        isSerialising = true, compressCode = "Defl",
                //        write = writeDeflateStream, root = tiny,
                //        read = readDeflateStream
                //    },
                //new scenario {
                //        desc = "Serialize small to Deflate MemStream", fqFileName = "smallDefl.cde",
                //        isSerialising = true, compressCode = "Defl",
                //        write = writeDeflateStream, root = small,
                //        read = readDeflateStream
                //    },
                //new scenario {
                //        desc = "Serialize large to Deflate MemStream", fqFileName = "largeDefl.cde",
                //        isSerialising = true, compressCode = "Defl",
                //        write = writeDeflateStream, root = large,
                //        read = readDeflateStream
                //    },

                new scenario {
                        desc = "Serialize tiny to LZ4 MemStream",
                        isSerialising = true, compressCode = "lz4", fqFileName = "tinyLZ4.cde",
                        write = writeLZ4Stream, root = tiny,
                        read = readLZ4Stream
                    },
                new scenario {
                        desc = "Serialize small to LZ4 MemStream",
                        isSerialising = true, compressCode = "lz4", fqFileName = "smallLZ4.cde",
                        write = writeLZ4Stream, root = small,
                        read = readLZ4Stream
                    },
                new scenario {
                        desc = "Serialize large to LZ4 MemStream",
                        isSerialising = true, compressCode = "lz4", fqFileName = "largeLZ4.cde",
                        write = writeLZ4Stream, root = large,
                        read = readLZ4Stream
                    },

                new scenario {
                        desc = "Serialize tiny to LZ4H MemStream",
                        isSerialising = true, compressCode = "lz4h", fqFileName = "tinyLZ4H.cde",
                        write = writeLZ4HStream, root = tiny,
                        read = readLZ4Stream
                    },
                new scenario {
                        desc = "Serialize small to LZ4H MemStream",
                        isSerialising = true, compressCode = "lz4h", fqFileName = "smallLZ4H.cde",
                        write = writeLZ4HStream, root = small,
                        read = readLZ4Stream
                    },
                new scenario {
                        desc = "Serialize large to LZ4H MemStream",
                        isSerialising = true, compressCode = "lz4h", fqFileName = "largeLZ4H.cde",
                        write = writeLZ4HStream, root = large,
                        read = readLZ4Stream
                    },    
    
                new scenario {
                        desc = "Serialize tiny to LZ4N MemStream",
                        isSerialising = true, compressCode = "lz4N", fqFileName = "tinyLZ4N.cde",
                        write = writeLZ4NStream, root = tiny,
                        read = readLZ4NStream
                    },
                new scenario {
                        desc = "Serialize small to LZ4N MemStream",
                        isSerialising = true, compressCode = "lz4N", fqFileName = "smallLZ4N.cde",
                        write = writeLZ4NStream, root = small,
                        read = readLZ4NStream
                    },
                new scenario {
                        desc = "Serialize large to LZ4N MemStream",
                        isSerialising = true, compressCode = "lz4N", fqFileName = "largeLZ4N.cde",
                        write = writeLZ4NStream, root = large,
                        read = readLZ4NStream
                    },  
 
                new scenario {
                        desc = "Serialize tiny to LZ4NH MemStream",
                        isSerialising = true, compressCode = "lz4NH", fqFileName = "tinyLZ4NH.cde",
                        write = writeLZ4NHStream, root = tiny,
                        read = readLZ4NStream
                    },
                new scenario {
                        desc = "Serialize small to LZ4NH MemStream",
                        isSerialising = true, compressCode = "lz4NH", fqFileName = "smallLZ4NH.cde",
                        write = writeLZ4NHStream, root = small,
                        read = readLZ4NStream
                    },
                new scenario {
                        desc = "Serialize large to LZ4NH MemStream",
                        isSerialising = true, compressCode = "lz4NH", fqFileName = "largeLZ4NH.cde",
                        write = writeLZ4NHStream, root = large,
                        read = readLZ4NStream
                    },  

                //new scenario
                //    {
                //        desc = "Blat small to MemStream",
                //        isSerialising = false,
                //        isCompressed = false,
                //        write = writeStream,
                //        root = small,
                //    },
            };

            for (var i = 0; i < Scenarios.Length; i++)
            {
                measureThis(ref Scenarios[i]);
            }
            Console.WriteLine("");
            Console.WriteLine("");
            for (var i = 0; i < Scenarios.Length; i++) // set originalSize on records from None records.
            {
                Scenarios[i].originalSize =
                    Scenarios.Where(s =>
                                    s.count == Scenarios[i].count &&
                                    s.compressCode == "None")
                                    .First().writtenSize;
            }

            for (var i = 0; i < Scenarios.Length; i++)
            {
                Scenarios[i].printer();
            }
        }

        public void wtf()
        {   // THIS ANNOYS ME.
            // It appears that supplying my own buffer to MemoryStream means that ToArray() cant give me only the data written.

            Console.WriteLine("BEFORE");
            var str = "The quick brown fox jumped over the lazy dog and then had a nap.";
            var buf = new byte[1000];
            MemoryStream ms;
            using (ms = new MemoryStream(buf))
            //using (ms = new MemoryStream())
            {
                Console.WriteLine($"c: {ms.Capacity} p: {ms.Position} l: {ms.Length}");
                var b = Encoding.UTF8.GetBytes(str);
                ms.Write(b, 0, b.Length);
                ms.Flush();
                Console.WriteLine($"c: {ms.Capacity} p: {ms.Position} l: {ms.Length}");
                ms.Close();
            }
            var x = ms.ToArray();
            Console.WriteLine($"x.Length: {x.Length}");

            //using (ms = new MemoryStream(buf))
            using (ms = new MemoryStream())
            {
                Console.WriteLine($"c: {ms.Capacity} p: {ms.Position} l: {ms.Length}");
                var b = Encoding.UTF8.GetBytes(str);
                ms.Write(b, 0, b.Length);
                ms.Flush();
                Console.WriteLine($"c: {ms.Capacity} p: {ms.Position} l: {ms.Length}");
                ms.Close();
            }
            var x2 = ms.ToArray();
            Console.WriteLine($"x2.Length: {x.Length}");


            Console.WriteLine("AFTER");
        }

        public void measureThis(ref scenario sce)
        {
            Console.WriteLine("measureThis " + sce.desc);
            if (sce.root == null)
            {
                return;
            }
            Console.WriteLine(sce.compressCode);
            MemoryStream memStream = null;
            var times = new List<long>(SampleCount);
            var timesRead = new List<long>(SampleCount);
            var timesFileWrite = new List<long>(SampleCount);
            var timesFileRead = new List<long>(SampleCount);
            var timesFileSSDWrite = new List<long>(SampleCount);
            var timesFileSSDRead = new List<long>(SampleCount);

            var sw = new Stopwatch();
            long streamLength = 0;

            for (var j = 0; j < SampleCount; j++)
            {
                //memStream = new MemoryStream(buffer); // THIS IS BROKEN in .Net so far as toArray().
                memStream = new MemoryStream();

                // doing a warm run for JIT first with no measure might improve consistency of results.. as well.
                //{   // trying to make benchmark a little more consistent.
                //    GC.Collect();
                //    System.Threading.Thread.Sleep(100);
                //}
                sw.Reset();
                sw.Start();
                sce.writer(memStream);
                sw.Stop();
                times.Add(sw.ElapsedMilliseconds);

                var writtenBuffer = memStream.ToArray();
                //Console.WriteLine("writtenBuffer.Length {0}", writtenBuffer.Length);
                streamLength = writtenBuffer.Length;

                //var b = memStream.GetBuffer();
                //Buffer.BlockCopy(buffer, 0, buffer2, 0, (int)streamLength);
                //str.Close();

                // read stream stuff.
                if (sce.read != null)
                {
                    //var ms2 = new MemoryStream(buffer, 0, (int)streamLength); // our captured buffer as read stream.
                    var ms2 = new MemoryStream(writtenBuffer); // our captured buffer as read stream.
                    sw.Reset();
                    sw.Start();
                    var r = sce.read(ms2);
                    r.stream.Close();
                    sw.Stop();
                    r.root = null;

                    timesRead.Add(sw.ElapsedMilliseconds);
                }

                if (!string.IsNullOrEmpty(sce.fqFileName))
                {
                    //Console.WriteLine(sce.fqFileName);

                    // write file test
                    sw.Reset();
                    sw.Start();
                    using (var fs = File.Open(sce.fqFileName, FileMode.Create))
                    {
                        sce.writer(fs);
                    }
                    sw.Stop();
                    timesFileWrite.Add(sw.ElapsedMilliseconds);

                    if (sce.read != null)
                    {
                        // read file test
                        sw.Reset();
                        sw.Start();
                        using (var fs = new FileStream(sce.fqFileName, FileMode.Open, FileAccess.Read))
                        {
                            sce.read(fs);
                        }
                        sw.Stop();
                        timesFileRead.Add(sw.ElapsedMilliseconds);
                    }

                    var ssdFile = @"C:\" + sce.fqFileName;
                    // write file test
                    sw.Reset();
                    sw.Start();
                    using (var fs = File.Open(ssdFile, FileMode.Create))
                    {
                        sce.writer(fs);
                    }
                    sw.Stop();
                    timesFileSSDWrite.Add(sw.ElapsedMilliseconds);

                    if (sce.read != null)
                    {
                        // read file test
                        sw.Reset();
                        sw.Start();
                        using (var fs = new FileStream(ssdFile, FileMode.Open, FileAccess.Read))
                        {
                            sce.read(fs);
                        }
                        sw.Stop();
                        timesFileSSDRead.Add(sw.ElapsedMilliseconds);
                    }

                }
            }
            //Console.WriteLine("memStream.Length");
            //Console.WriteLine(memStream.Length);
            sce.writtenSize = streamLength;
            sce.counter();
            sce.writeDuration = (long)Math.Floor(times.Average());
            if (timesRead.Count == SampleCount)
            {
                sce.readDuration = (long)Math.Floor(timesRead.Average());
            }
            sce.writeFileDuration = (long)Math.Floor(timesFileWrite.Average());
            sce.readFileDuration = (long)Math.Floor(timesFileRead.Average());
            sce.writeFileSSDDuration = (long)Math.Floor(timesFileSSDWrite.Average());
            sce.readFileSSDDuration = (long)Math.Floor(timesFileSSDRead.Average());
            sce.sampleCount = SampleCount;
        }

        #region supportsy stuff

        public Result readStream(Stream s)
        {
            var result = new Result { stream = s };
            var root = RootEntry.Read(s);
            if (root == null) { throw new Exception("root is null ERROR !!!!!!!!!!!!!!!"); }
            root.SetInMemoryFields();
            result.root = root;
            return result;
        }

        public Result readGzipStream(Stream s)
        {
            var xFs = new GZipStream(s, CompressionMode.Decompress);
            var result = new Result { stream = xFs };
            var root = RootEntry.Read(xFs);
            if (root == null) { throw new Exception("root is null ERROR !!!!!!!!!!!!!!!"); }
            root.SetInMemoryFields();
            result.root = root;
            return result;
        }

        public Result readDeflateStream(Stream s)
        {
            var xFs = new DeflateStream(s, CompressionMode.Decompress);
            var result = new Result { stream = xFs };
            var root = RootEntry.Read(xFs);
            if (root == null) { throw new Exception("root is null ERROR !!!!!!!!!!!!!!!"); }
            root.SetInMemoryFields();
            result.root = root;
            return result;
        }

        public Result readLZ4Stream(Stream s)
        {
            var xFs = new LZ4Stream(s, CompressionMode.Decompress);
            var result = new Result { stream = xFs };
            var root = RootEntry.Read(xFs);
            if (root == null) { throw new Exception("root is null ERROR !!!!!!!!!!!!!!!"); }
            root.SetInMemoryFields();
            result.root = root;
            return result;
        }

        public Result readLZ4NStream(Stream s)
        {
            var xFs = new Lz4DecompressionStream(s);
            var result = new Result { stream = xFs };
            var root = RootEntry.Read(xFs);
            if (root == null) { throw new Exception("root is null ERROR !!!!!!!!!!!!!!!"); }
            root.SetInMemoryFields();
            result.root = root;
            return result;
        }

        public void writeStream(RootEntry root, Stream s)
        {
            using (s)
            {
                root.Write(s);
            }
        }

        public void writeGzipStream(RootEntry root, Stream s)
        {
            using (var xFs = new GZipStream(s, CompressionMode.Compress, true))
            {
                root.Write(xFs);
            }
        }

        public void writeDeflateStream(RootEntry root, Stream s)
        {
            using (var xFs = new DeflateStream(s, CompressionMode.Compress))
            {
                root.Write(xFs);
            }
        }

        public void writeLZ4Stream(RootEntry root, Stream s)
        {
            using  (var xFs = new LZ4Stream(s, CompressionMode.Compress))
            {
                root.Write(xFs);
            }
        }

        public void writeLZ4HStream(RootEntry root, Stream s)
        {
            using (var xFs = new LZ4Stream(s,LZ4StreamMode.Compress,LZ4StreamFlags.HighCompression))
            {
                root.Write(xFs);
            }
        }

        public void writeLZ4NStream(RootEntry root, Stream s)
        {
            using (var xFs = new Lz4CompressionStream(s, 1<<18, Lz4Mode.Fast))
            {
                root.Write(xFs);
            }
        }

        public void writeLZ4NHStream(RootEntry root, Stream s)
        {
            using (var xFs = new Lz4CompressionStream(s, 1 << 18, Lz4Mode.HighCompression))
            {
                root.Write(xFs);
            }
        }
        #endregion
    }
}
// ReSharper restore InconsistentNaming

