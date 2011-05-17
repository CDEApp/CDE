using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using cdeLib.Infrastructure;
using NUnit.Framework;
using ProtoBuf;

namespace ProtobufTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class ProtobufTest
    {
        [Test]
        public void Serialize_TestByte_CheckLength()
        {
            var tbClass = new TestByte();

            var ms = new MemoryStream();

            Serializer.Serialize(ms, tbClass);
            ms.Close();
            var b = ms.ToArray();

            Console.WriteLine("b.Length {0}", b.Length);
            Console.WriteLine("b {0}", ByteArrayHelper.ByteArrayToString(b));
            Assert.That(b.Length, Is.EqualTo(2));
        }

        [ProtoContract]
        public class TestByte
        {
            [ProtoMember(1, IsRequired = true)]
            public byte a = 0x55;
        }

        [Test]
        public void Serialize_TestBool_CheckLength()
        {
            var tbClass = new TestBool();

            var ms = new MemoryStream();

            Serializer.Serialize(ms, tbClass);
            ms.Close();
            var b = ms.ToArray();

            Console.WriteLine("b.Length {0}", b.Length);
            Console.WriteLine("b {0}", ByteArrayHelper.ByteArrayToString(b));
        }

        [ProtoContract]
        public class TestBool
        {
            [ProtoMember(1, IsRequired = true)]
            public bool a = true;
        }

        [Test]
        public void Serialize_TestByteArray10_CheckLength()
        {
            var tbClass = new TestByteClass();
            tbClass.f1 = new byte[10];

            var ms = new MemoryStream();

            Serializer.Serialize(ms, tbClass);

            var b = ms.ToArray();

            Console.WriteLine("b.Length {0}", b.Length);
            Console.WriteLine("b {0}", ByteArrayHelper.ByteArrayToString(b));
        }

        [Test]
        public void Serialize_TestByteArrayNull_CheckLength()
        {
            var tbClass = new TestByteClass();
            tbClass.f1 = null;

            var ms = new MemoryStream();

            Serializer.Serialize(ms, tbClass);

            var b = ms.ToArray();

            Console.WriteLine("b.Length {0}", b.Length);
            Console.WriteLine("b {0}", ByteArrayHelper.ByteArrayToString(b));
        }


        [ProtoContract]
        public class TestByteClass
        {
            [ProtoMember(1, IsRequired = true)]
            public byte a = 0x55;

            [ProtoMember(2, IsRequired = true)] 
            public byte[] f1;

            [ProtoMember(3, IsRequired = true)]
            public byte b = 0x99;
        }

        [Test]
        public void Serialize_TestStringIsNull_CheckLength()
        {
            var tbClass = new TestStringField();
            tbClass.f1 = null;
            //tbClass.f1 = "Wubba";

            var ms = new MemoryStream();

            Serializer.Serialize(ms, tbClass);

            var b = ms.ToArray();

            Console.WriteLine("b.Length {0}", b.Length);
            Console.WriteLine("b {0}", ByteArrayHelper.ByteArrayToString(b));
        }

        [ProtoContract]
        public class TestStringField
        {
            [ProtoMember(1, IsRequired = true)]
            public byte a = 0x55;

            [ProtoMember(2, IsRequired = true)]
            public string f1;

            [ProtoMember(3, IsRequired = true)]
            public byte b = 0x99;
        }

        [Test]
        public void Serialize_TestStringIs5_CheckLength()
        {
            var tbClass = new TestStringField();
            tbClass.f1 = "Wubba";

            var ms = new MemoryStream();

            Serializer.Serialize(ms, tbClass);

            var b = ms.ToArray();

            Console.WriteLine("b.Length {0}", b.Length);
            Console.WriteLine("b {0}", ByteArrayHelper.ByteArrayToString(b));
        }

        [Test]
        public void Serialize_TestStringIs15_CheckLength()
        {
            var tbClass = new TestStringField();
            tbClass.f1 = "Wubba0123456789";

            var ms = new MemoryStream();

            Serializer.Serialize(ms, tbClass);

            var b = ms.ToArray();

            Console.WriteLine("b.Length {0}", b.Length);
            Console.WriteLine("b {0}", ByteArrayHelper.ByteArrayToString(b));
        }

        [Test]
        public void MemoryStream_ToArray_Rather_Than_GetBuffer_Its_Much_More_Useful()
        {
            var m = new MemoryStream(64);
            Console.WriteLine("Length: {0}\tPosition: {1}\tCapacity: {2}", m.Length, m.Position, m.Capacity);

            for (int i = 0; i < 64; i++)
            {
                m.WriteByte((byte)i);
            }
            Console.WriteLine("Length: {0}\tPosition: {1}\tCapacity: {2}", m.Length, m.Position, m.Capacity);

            byte[] ba = m.ToArray();
            foreach (byte b in ba)
            {
                Console.Write("{0,-3}", b);
            }

            m.Close();
        }

        [Test]
        public void hack()
        {
            var s = DateTime.Now.ToString("o");
            Console.WriteLine("DateTime.Now.ToString(\"o\") {0}", s);
        }
    }
    // ReSharper restore InconsistentNaming


    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class ProtobufTest_Struct
    {
        [Test]
        public void Struct_Serialise_Protobuf_TestByte()
        {
            ES es;
            es = new ES(10);
            es.Save("test.l10__.f5___.bin");
            es = new ES(100);
            es.Save("test.l100_.f50__.bin");
            es = new ES(1000, 100);
            es.Save("test.l1000.f100_.bin");
            es = new ES(1000, 500);
            es.Save("test.l1000.f500_.bin");
            es = new ES(1000, 1000);
            es.Save("test.l1000.f1000.bin");

            es = new ES(65536, 1);
            es.Save("test.l65536.f1____.bin");
            es = new ES(65536, 10);
            es.Save("test.l65536.f10___.bin");
            es = new ES(65536, 100);
            es.Save("test.l65536.f100__.bin");
            es = new ES(65536, 1000);
            es.Save("test.l65536.f1000_.bin");
            es = new ES(65536, 10000);
            es.Save("test.l65536.f10000.bin");
            es = new ES(65536, 50000);
            es.Save("test.l65536.f50000.bin");
        }

        [ProtoContract]
        public class ES
        {
            [ProtoMember(1, IsRequired = true)]
            SortedList<uint, E[]> mine;

            public ES(uint length, uint fill = 0u)
            {
                mine = new SortedList<uint, E[]>(10);
                mine[0] = new E[length];
                var b = mine[0];
                if (fill == 0)
                {
                    fill = length / 2;
                }
                for (uint i = 0; i < fill; i++)
                {
                    b[i].f1 = 1 + i;
                    b[i].s1 = string.Format("s1{0}", i);
                }
            }

            public void Save(string fileName)
            {
                using (var newFs = File.Open(fileName, FileMode.Create))
                {
                    Write(newFs);
                }
            }

            public void Write(Stream output)
            {
                Serializer.Serialize(output, this);
            }
        }

        [ProtoContract]
        public struct E
        {
            [ProtoMember(1, IsRequired = true)]
            public uint f1;
            [ProtoMember(2, IsRequired = true)]
            public string s1;

            public ulong Size;
            public DateTime Modified;
            public string Name;
            public string FullPath;
            public Hash16 Hash; // waste 8 bytes with pointer if we dont store it here. this is 16bytes.

        }

        //// duplicated from project
        //public struct Hash16
        //{
        //    public ulong HashA; // first 8 bytes
        //    public ulong HashB; // last 8 bytes

        //    public Hash16(byte[] hash)
        //    {
        //        HashA = BitConverter.ToUInt64(hash, 0); // swapped offset because of intel
        //        HashB = BitConverter.ToUInt64(hash, 8); // swapped offset because of intel
        //    }

        //    public string HashAsString
        //    {
        //        get
        //        {
        //            var a = BitConverter.GetBytes(HashA);
        //            var b = BitConverter.GetBytes(HashB);
        //            return ByteArrayHelper.ByteArrayToString(a)
        //                   + ByteArrayHelper.ByteArrayToString(b);
        //        }
        //    }
        //}

        //// duplicated from project
        //public static class ByteArrayHelper
        //{
        //    public static string ByteArrayToString(byte[] bytes)
        //    {
        //        if (bytes == null)
        //        {
        //            return "null";
        //        }
        //        var sb = new StringBuilder();
        //        for (var i = 0; i < bytes.Length; i++)
        //        {
        //            //TODO: Optimize via http://blogs.msdn.com/b/blambert/archive/2009/02/22/blambert-codesnip-fast-byte-array-to-hex-string-conversion.aspx
        //            sb.Append(bytes[i].ToString("x2"));
        //        }
        //        return sb.ToString();
        //    }
        //}
    }
    // ReSharper restore InconsistentNaming
}
