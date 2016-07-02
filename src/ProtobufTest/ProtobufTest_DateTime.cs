using System;
using System.IO;
using NUnit.Framework;
using ProtoBuf;

namespace ProtobufTest
{
    [TestFixture]
    class ProtobufTest_DateTime
    {
        [Test]
        public void test_datetime_kind()
        {
            var t = new Test() { d = DateTime.UtcNow };

            Assert.That(t.d.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void test_datetime_kind_deserialize()
        {
            var t = new Test() { d = DateTime.UtcNow };

            var ms = new MemoryStream();
            Serializer.Serialize(ms, t);
            ms.Seek(0, SeekOrigin.Begin);
            var t2 = Serializer.Deserialize<Test>(ms);

            // yes it does lose the Utc... dangit.
            Assert.That(t2.d.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        }


        [ProtoContract]
        public class Test
        {
            [ProtoMember(1, IsRequired = true)]
            public DateTime d;
        }

    }
}
