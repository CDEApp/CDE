using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cdeLib;
using cdeLib.Duplicates;
using cdeLib.Entities;
using EFlags = cdeLib.Entities.Flags;
using cdeLib.Entities.Columnar;
using cdeLib.Hashing;
using cdeLib.Infrastructure;
using cdeLib.Infrastructure.Config;
using cdeLib.Infrastructure.Hashing;
using NSubstitute;
using NUnit.Framework;
using ILogger = cdeLib.Infrastructure.ILogger;

namespace cdeLibTest.Hashing;

/// <summary>
/// Issue #175: a hash phase that fails unexpectedly must not be swallowed. The hashes completed so far are still
/// saved, then the run fails with a summary line. Per-file read errors stay non-fatal but are counted and reported.
/// </summary>
[TestFixture]
[NonParallelizable]
public class HashFailureReportingTests
{
    private const int FirstPass = 1024;

    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cdehash175-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    /// <summary>Throws (an unexpected error, not a read failure) for a file whose name contains "boom".</summary>
    private sealed class ThrowingHashHelper(ILogger logger, bool onlyFullHash) : HashHelper(logger)
    {
        public override ValueTask<HashResponse> GetHashResponseFromFile(string filename, int? bytesToHash)
        {
            if (filename.Contains("boom", StringComparison.Ordinal) && (!onlyFullHash || bytesToHash == null))
                throw new InvalidOperationException("simulated hash failure");
            return base.GetHashResponseFromFile(filename, bytesToHash);
        }
    }

    private static Duplication NewDuplication(HashHelper helper = null, List<string> status = null)
    {
        var config = Substitute.For<IConfiguration>();
        config.ProgressUpdateInterval.Returns(100);
        config.HashFirstPassSize.Returns(FirstPass);
        config.DegreesOfParallelism.Returns(1);
        config.Config.Returns(new AppConfigurationSection { Display = new DisplaySection { ConsoleLogToSeq = true } });
        var logger = Substitute.For<ILogger>();
        var d = new Duplication(logger, config, Substitute.For<IApplicationDiagnostics>(), new OperationCancellation(),
            helper ?? new HashHelper(logger));
        if (status != null)
        {
            d.StatusMessageEvent = m => { lock (status) status.Add(m); };
        }

        return d;
    }

    private static byte[] Bytes(int size, int seed)
    {
        var b = new byte[size];
        new Random(seed).NextBytes(b);
        return b;
    }

    private string Put(string name, byte[] content)
    {
        var path = Path.Combine(_dir, "root", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    private string Scan()
    {
        var config = Substitute.For<IConfiguration>();
        config.Config.Returns(new AppConfigurationSection { Display = new DisplaySection { ConsoleLogToSeq = true } });
        var root = new RootEntry(config);
        root.PopulateRoot(Path.Combine(_dir, "root"));
        var cdex = Path.Combine(_dir, "root.cdex");
        ColumnarFormat.Write(root, cdex);
        return cdex;
    }

    private static EFlags FlagsOf(string cdex, string name)
    {
        using var r = new ColumnarCatalogReader(cdex);
        for (var i = 1; i < r.Count; i++)
        {
            if (r.FullPath(i).EndsWith(name, StringComparison.Ordinal)) return r.Flags(i);
        }

        throw new AssertionException("no entry " + name);
    }

    [Test]
    public async Task Success_path_has_no_failure_and_no_error_line()
    {
        Put("a1.bin", Bytes(4096, 1));
        Put("a2.bin", File.ReadAllBytes(Path.Combine(_dir, "root", "a1.bin")));
        var cdex = Scan();
        var status = new List<string>();
        var d = NewDuplication(status: status);

        await ColumnarCatalogHashing.HashAndSave(d, [cdex], null);

        Assert.That(d.UnexpectedFailure, Is.Null);
        Assert.That(d.FilesFailed, Is.Zero);
        Assert.That(status.Any(m => m.Contains("errors") || m.Contains("FAILED")), Is.False);
        Assert.That(FlagsOf(cdex, "a1.bin") & EFlags.HashDone, Is.EqualTo(EFlags.HashDone));
    }

    [Test]
    public async Task Unexpected_failure_in_the_first_pass_saves_completed_hashes_then_fails_with_a_summary()
    {
        // largest first: the 4096 pair is hashed before the smaller pair that throws.
        var big = Bytes(4096, 1);
        Put("big1.bin", big);
        Put("big2.bin", big);
        var small = Bytes(2000, 2);
        Put("boom1.bin", small);
        Put("boom2.bin", small);
        var cdex = Scan();
        var status = new List<string>();
        var d = NewDuplication(new ThrowingHashHelper(Substitute.For<ILogger>(), false), status);

        var ex = await Assert.ThrowsAsync<HashFailedException>(() => ColumnarCatalogHashing.HashAndSave(d, [cdex], null));

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.StartWith("Hash FAILED: "));
            Assert.That(ex.Message, Does.Contain("simulated hash failure"));
            Assert.That(ex.Message, Does.Contain("files hashed"));
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(status.Any(m => m.Contains("ERROR")), Is.True, "the error is printed, not only logged");
            Assert.That(FlagsOf(cdex, "big1.bin") & EFlags.HashDone, Is.EqualTo(EFlags.HashDone), "completed hashes are saved");
            Assert.That(File.Exists(cdex + ".tmp"), Is.False);
        });
    }

    [Test]
    public async Task Unexpected_failure_in_the_full_phase_keeps_the_first_pass_hashes_and_fails()
    {
        var big = Bytes(4096, 1);
        Put("boom1.bin", big);
        Put("boom2.bin", big);
        var cdex = Scan();
        var d = NewDuplication(new ThrowingHashHelper(Substitute.For<ILogger>(), true));

        var ex = await Assert.ThrowsAsync<HashFailedException>(() => ColumnarCatalogHashing.HashAndSave(d, [cdex], null));

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("simulated hash failure"));
            var f = FlagsOf(cdex, "boom1.bin");
            Assert.That(f & EFlags.HashDone, Is.EqualTo(EFlags.HashDone));
            Assert.That(f & EFlags.PartialHash, Is.EqualTo(EFlags.PartialHash));
        });
    }

    [Test]
    public async Task A_file_that_cannot_be_read_is_counted_and_reported_but_does_not_fail_the_run()
    {
        var big = Bytes(4096, 1);
        Put("a1.bin", big);
        Put("a2.bin", big);
        var gone = Put("gone.bin", big);
        var cdex = Scan();
        File.Delete(gone);
        var status = new List<string>();
        var d = NewDuplication(status: status);

        await ColumnarCatalogHashing.HashAndSave(d, [cdex], null);

        Assert.Multiple(() =>
        {
            Assert.That(d.UnexpectedFailure, Is.Null);
            Assert.That(d.FilesFailed, Is.EqualTo(1));
            Assert.That(status, Has.Some.Contain("Hashing finished with 1 error"));
            Assert.That(FlagsOf(cdex, "a1.bin") & EFlags.HashDone, Is.EqualTo(EFlags.HashDone));
        });
    }
}
