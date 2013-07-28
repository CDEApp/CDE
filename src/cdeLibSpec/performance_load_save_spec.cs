using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using LZ4;
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
    [Tag("describe_test")]
    class loadsave_cde_performance : nspec
    {
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
                Console.WriteLine("\"{0}\", {1}, {2}, {3}, {4}, {5}, {6}, {7}",
                    desc, compressCode, sampleCount, count, originalSize, writtenSize, writeDuration, readDuration);
            }
        }

        private const string PathToTest = @"..\..\..\..\test\";
        private const string TestCatalog32K = PathToTest + "G-SN750B_02_S13UJ1NQ221583.cde";
        private const string TestCatalog200K = PathToTest + "C-V3Win7.cde";
        private const string TestCatalog1_2M = PathToTest + "D-SM15T_2_1.cde";
        public const int SampleCount = 10;
        public const int OneHundredMB = 100*1024*1024; // 100 MB is big enough for test files i have.

        public byte[] buffer = new byte[OneHundredMB];
        //public byte[] buffer2 = new byte[OneHundredMB];

        public class Info
        {
            public RootEntry root;
            public int count;
            public long durationMsec;
        }

        public void create_test_files_for_loading()
        {
            //wtf();

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

                new scenario {
                        desc = "Serialize tiny to Deflate MemStream", fqFileName = "tinyDefl.cde",
                        isSerialising = true, compressCode = "Defl",
                        write = writeDeflateStream, root = tiny,
                        read = readDeflateStream
                    },
                new scenario {
                        desc = "Serialize small to Deflate MemStream", fqFileName = "smallDefl.cde",
                        isSerialising = true, compressCode = "Defl",
                        write = writeDeflateStream, root = small,
                        read = readDeflateStream
                    },
                new scenario {
                        desc = "Serialize large to Deflate MemStream", fqFileName = "largeDefl.cde",
                        isSerialising = true, compressCode = "Defl",
                        write = writeDeflateStream, root = large,
                        read = readDeflateStream
                    },

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

            //write(small, "small.cde");
            //writeLZ4(small, "smallLZ4.cde");
            //writeDeflate(small, "smallDeflate.cde");
            //writeGzip(small, "smallGzip.cde");
            //writeMemGzip(small);

            //write(large, "large.cde");
            //writeLZ4(large, "largeLZ4.cde");
            //writeDeflate(large, "largeDeflate.cde");
            //writeGzip(large, "largeGzip.cde");
            //writeMemGzip(large);

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
                Console.WriteLine("c: {0} p: {1} l: {2}", ms.Capacity, ms.Position, ms.Length);
                var b = Encoding.UTF8.GetBytes(str);
                ms.Write(b, 0, b.Length);
                ms.Flush();
                Console.WriteLine("c: {0} p: {1} l: {2}", ms.Capacity, ms.Position, ms.Length);
                ms.Close();
            }
            var x = ms.ToArray();
            Console.WriteLine("x.Length: {0}", x.Length);

            //using (ms = new MemoryStream(buf))
            using (ms = new MemoryStream())
            {
                Console.WriteLine("c: {0} p: {1} l: {2}", ms.Capacity, ms.Position, ms.Length);
                var b = Encoding.UTF8.GetBytes(str);
                ms.Write(b, 0, b.Length);
                ms.Flush();
                Console.WriteLine("c: {0} p: {1} l: {2}", ms.Capacity, ms.Position, ms.Length);
                ms.Close();
            }
            var x2 = ms.ToArray();
            Console.WriteLine("x2.Length: {0}", x.Length);


            Console.WriteLine("AFTER");
        }

        public void measureThis(ref scenario sce)
        {
            if (sce.root == null)
            {
                return;
            }
            Console.WriteLine(sce.compressCode);
            MemoryStream memStream = null;
            var times = new List<long>(SampleCount);
            var timesRead = new List<long>(SampleCount);
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
                Console.WriteLine("writtenBuffer.Length {0}", writtenBuffer.Length);
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

            sce.sampleCount = SampleCount;
        }

        #region supportsy stuff

        public Result readStream(Stream s)
        {
            var result = new Result { stream = s };
            var root = RootEntry.Read(s);
            if (root == null) { throw new Exception("root is null ERROR !!!!!!!!!!!!!!!"); }
            root.ActualFileName = "gumby";
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
            root.ActualFileName = "gumby";
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
            root.ActualFileName = "gumby";
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
            root.ActualFileName = "gumby";
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
            using (var xFs = new LZ4Stream(s, CompressionMode.Compress, true))
            {
                root.Write(xFs);
            }
        }



        private void write(RootEntry small, string fileName)
        {
            using (var newFs = File.Open(fileName, FileMode.Create))
            {
                small.Write(newFs);
            }
        }
        private void writeLZ4(RootEntry small, string fileName)
        {
            using (var newFs = File.Open(fileName, FileMode.Create))
            using (var xFs = new LZ4Stream(newFs, CompressionMode.Compress))
            {
                small.Write(xFs);
            }
        }
        private void writeDeflate(RootEntry small, string fileName)
        {
            using (var newFs = File.Open(fileName, FileMode.Create))
            using (var xFs = new DeflateStream(newFs, CompressionMode.Compress))
            {
                small.Write(xFs);
            }
        }
        private void writeGzip(RootEntry small, string fileName)
        {
            using (var newFs = File.Open(fileName, FileMode.Create))
            {
                writeGzipStream(small, newFs);
            }
        }

        private void writeMemGzip(RootEntry small)
        {
            using (var newFs = new MemoryStream(buffer))
            {
                writeGzipStream(small, newFs);
            }
        }
        #endregion

        public Info LoadIt(string fqFileName)
        {
            var times = new long[SampleCount];
            var i = new Info();

            Console.WriteLine("LoadIt");
            for (int j = 0; j < SampleCount; j++)
            {
                var sw = new Stopwatch();
                {   // trying to make benchmark a little more consistent.
                    GC.Collect();
                    System.Threading.Thread.Sleep(100);
                }

                sw.Start();
                i.root = RootEntry.LoadDirCache(fqFileName);
                sw.Stop();

                i.count = CommonEntry.GetDirEntries(i.root).Count();
                times[j] = sw.ElapsedMilliseconds;
                Console.Write(".");
                //System.Console.WriteLine("{0}msecs", sw.ElapsedMilliseconds);
            }
            i.durationMsec = (long)Math.Floor(times.Average());
            Console.WriteLine("#{0} {1}msecs \"{2}\"", i.count, i.durationMsec, fqFileName);
            return i;
        }
    }

    // test write buffer, not serialize ? as test. as well.
    // save and load to ssd and to hd.
    // save and load uncompressed.
    // save and load zlib ? gzip?
    // save and load lz4

}
// ReSharper restore InconsistentNaming

