using System;
using System.IO;
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

    }
    // ReSharper restore InconsistentNaming
}
