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
            Console.WriteLine($"Length: {m.Length}\tPosition: {m.Position}\tCapacity: {m.Capacity}");

            for (int i = 0; i < 64; i++)
            {
                m.WriteByte((byte)i);
            }
            Console.WriteLine($"Length: {m.Length}\tPosition: {m.Position}\tCapacity: {m.Capacity}");

            byte[] ba = m.ToArray();
            foreach (byte b in ba)
            {
                Console.Write($"{b,-3}");
            }

            m.Close();
        }

        [Test]
        public void hack()
        {
            var s = DateTime.Now.ToString("o");
            Console.WriteLine($"DateTime.Now.ToString(\"o\") {s}");
        }
    }
    // ReSharper restore InconsistentNaming
}
