using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProtoBuf;
using cdeLib;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using NSubstitute;

namespace cdeLibTest
{
    /// <summary>
    /// Test out how it serializes.
    /// That it serializes.
    /// If i can deserialize without rehydrating entire tree.
    /// </summary>
    [TestFixture]
    class SerializeDataModelTest
    {
        // ReSharper disable InconsistentNaming
        private DirEntry de2a;
        private DirEntry de2b;
        private DirEntry de2c;
        private DirEntry de3a;
        private DirEntry de4a;
        private RootEntry re1;
        readonly IConfiguration _config = Substitute.For<IConfiguration>();

        [SetUp]
        public void SetUp()
        {
            _config.ProgressUpdateInterval.Returns(5000);
        }

        [Test]
        public void Serialize_RootEntry()
        {
            re1 = CommonEntryTest_TraverseTree.NewTestRootEntry(_config, out de2a, out de2b, out de2c, out de3a, out de4a);

            var ms = new MemoryStream();
            Serializer.Serialize(ms, re1);
            var b = ms.ToArray();

            Console.WriteLine($"b.Length {b.Length}");
            Console.WriteLine($"b {ByteArrayHelper.ByteArrayToString(b)}");
            Assert.That(b.Length, Is.EqualTo(118));
        }

        // just a sanity check - shouldnt ever fail and only checking rootpath and name field.
        [Test]
        public void Serialize_Deserialize_RootEntryMatches()
        {
            re1 = CommonEntryTest_TraverseTree.NewTestRootEntry(_config, out de2a, out de2b, out de2c, out de3a, out de4a);

            var ms = new MemoryStream();
            Serializer.Serialize(ms, re1);
            var b = ms.ToArray();

            var newMS = new MemoryStream(b);
            var re2 = Serializer.Deserialize<RootEntry>(newMS);

            var same = re1.SameTree(re2);
            Assert.That(same, Is.True, TreeDuplicate.LastMessage);
        }

        [Test]
        public void Serialize_Deserialize_RootEntryFailsMatches()
        {
            re1 = CommonEntryTest_TraverseTree.NewTestRootEntry(_config, out de2a, out de2b, out de2c, out de3a, out de4a);

            var ms = new MemoryStream();
            Serializer.Serialize(ms, re1);
            var b = ms.ToArray();

            var newMS = new MemoryStream(b);
            var re2 = Serializer.Deserialize<RootEntry>(newMS);

            re2.Path = "moo";
            var same = re1.SameTree(re2);
            Assert.That(same, Is.False, TreeDuplicate.LastMessage);
        }

        [Ignore("Cant do this as dir tree is part of root entry bending test to toy with protobuf-net a bit")]
        [Test]
        public void DeSerialize_JustRootEntryThatHasTree()
        {
            re1 = CommonEntryTest_TraverseTree.NewTestRootEntry(_config, out de2a, out de2b, out de2c, out de3a, out de4a);

            var ms = new MemoryStream();

            Serializer.SerializeWithLengthPrefix(ms, re1, PrefixStyle.Base128, 1);
            Serializer.SerializeWithLengthPrefix(ms, re1, PrefixStyle.Base128, 1);

            var b = ms.ToArray();
            Console.WriteLine($"b.Length {b.Length}");
            Console.WriteLine($"b {ByteArrayHelper.ByteArrayToString(b)}");

            var newMS = new MemoryStream(b);

            var iter = Serializer.DeserializeItems<RootEntry>(newMS, PrefixStyle.Base128, 1);
            var first = iter.FirstOrDefault();
            if (first != null) Console.WriteLine("first.RootPath " + first.Path);

            var second = iter.FirstOrDefault();
            if (second != null) Console.WriteLine("second.RootPath " + second.Path);
            var third = iter.FirstOrDefault();
            if (third != null)
            {
                Console.WriteLine("third.RootPath " + third.Path);
            }
            else
            {
                Console.WriteLine("third.RootPath doesn't exist.");
            }
        }
        // ReSharper restore InconsistentNaming
    }

    public static class TreeDuplicate
    {
        public static string LastMessage;

        //public static bool AreTreesSame(RootEntry re1, RootEntry re2)
        //{
        //    LastMessage = "";
        //    var differenceFound = false;
        //    var e1 = new DirEntryEnumerator(re1);
        //    var e2 = new DirEntryEnumerator(re2);

        //    while (e1.MoveNext() && e2.MoveNext())
        //    {
        //        LastMessage = string.Format("{0} not same as {1}", e1.Current.Name, e2.Current.Name);
        //        if (e1.Current.Name != e2.Current.Name)
        //        {
        //            differenceFound = true;
        //            break;
        //        }
        //        Console.WriteLine(LastMessage);
        //    }
        //    var bothTreesComplete = e1.MoveNext() == false && e2.MoveNext() == false;
        //    return !differenceFound && bothTreesComplete;
        //}

        public static bool SameTree(this RootEntry re1, RootEntry re2)
        {
            LastMessage = "";

            if (re1.Path != re2.Path)
            {
                LastMessage = $"RootPath {re1.Path} not same as {re2.Path}";
                return false;
            }

            var differenceFound = false;
            var e1 = new DirEntryEnumerator(re1);
            var e2 = new DirEntryEnumerator(re2);
            while (e1.MoveNext() && e2.MoveNext())
            {
                LastMessage = $"{e1.Current.Path} not same as {e2.Current.Path}";
                if (e1.Current.Path != e2.Current.Path)
                {
                    differenceFound = true;
                    break;
                }
                Console.WriteLine(LastMessage);
            }
            var bothTreesComplete = e1.MoveNext() == false && e2.MoveNext() == false;
            return !differenceFound && bothTreesComplete;
        }
    }
}
