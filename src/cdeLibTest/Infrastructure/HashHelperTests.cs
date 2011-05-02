using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace cdeLibTest.Infrastructure
{
    public class HashHelperTests
    {
        [Test]
        public void UintHashMatchesByteArrayHash()
        {
            string s = "My awesome string";
            MD5Hash hash = new MD5Hash();
            var stringAsBytes = Encoding.ASCII.GetBytes(s);
            MD5 md5 = MD5.Create();

            byte[] asBytes = md5.ComputeHash(stringAsBytes);
            
            var asUInt = BitConverter.ToUInt32(asBytes, 0);
            var hashAsUInt = hash.Hash(stringAsBytes);

            //reverse
            var toBytes = BitConverter.GetBytes(hashAsUInt);
            Assert.AreEqual(asBytes,toBytes);
        }
    }
}