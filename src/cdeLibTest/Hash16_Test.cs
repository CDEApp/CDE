using System;
using System.Security.Cryptography;
using cdeLib.Entities;
using cdeLib.Infrastructure;
using NUnit.Framework;

namespace cdeLibTest
{
    // ReSharper disable InconsistentNaming
    [TestFixture]
    public class Hash16_Test
    {
        [Test]
        public void Constructor_Basic_OK()
        {
            var h = new Hash16();

            Assert.That(h.HashAsString, Is.EqualTo("00000000000000000000000000000000"));
        }

        [Test]
        public void Constructor_ValueComesOutAgainAsExpected_OK()
        {
            // ReSharper disable RedundantExplicitArraySize
            var b = new byte[16]
                        {
                            0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88,
                            0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00,
                        };
            // ReSharper restore RedundantExplicitArraySize
            var s1 = ByteArrayHelper.ByteArrayToString(b);
            var s2 = new Hash16(b).HashAsString;
            Console.WriteLine($"s1 {s1}");
            Console.WriteLine($"s2 {s2}");
            Assert.That(s2, Is.EqualTo("ffeeddccbbaa99887766554433221100"));
        }

        [Test]
        public void HashAsString_Init1_OK()
        {
            // ReSharper disable RedundantExplicitArraySize
            var b = new byte[16]
                        {
                            0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88,
                            0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00,
                        };
            // ReSharper restore RedundantExplicitArraySize

            byte[] hash;
            using (var md5 = MD5.Create())
            {
                hash = md5.ComputeHash(b);
            }
            var h16 = new Hash16(hash);

            Console.WriteLine($"h16.HashAsString {h16.HashAsString}");
            Console.WriteLine($"         old hex {ByteArrayHelper.ByteArrayToString(hash)}");

            Assert.That(h16.HashAsString, Is.EqualTo("a4bd60352d683c3eac5f826528bbfd02"));
        }
    }
    // ReSharper restore InconsistentNaming
}