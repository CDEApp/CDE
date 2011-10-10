using System;
using System.IO;
using NUnit.Framework;

namespace ProtobufTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    class MiscTest
    {

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
}
