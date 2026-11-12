using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore.Dtos;
using cdeAppCore.Jobs;
using cdeAppCore.Serialization;
using cdeAppCore.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace cdeApiTest;

[TestFixture]
public class JobsEndpointsTests
{
    private static readonly IServiceProvider Services = new ServiceCollection().AddLogging().BuildServiceProvider();

    private string _dir;
    private string _cat;
    private string[] _data;
    private string[] _cdex;
    private CatalogSession _session;
    private JobRegistry _jobs;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cde-api-jobs-" + Guid.NewGuid().ToString("N"));
        _cat = Path.Combine(_dir, "cat");
        Directory.CreateDirectory(_cat);
        _data = [Path.Combine(_dir, "data0"), Path.Combine(_dir, "data1")];
        _cdex = [Path.Combine(_cat, "a.cdex"), Path.Combine(_cat, "b.cdex")];
        for (var i = 0; i < 2; i++)
        {
            Directory.CreateDirectory(_data[i]);
            File.WriteAllText(Path.Combine(_data[i], "a.txt"), "a");
            TestCatalog.WriteCatalog(_data[i], _cdex[i]);
        }

        _session = new CatalogSession(new TestCatalog.FolderLoader(), null);
        _session.LoadAsync(_cat).GetAwaiter().GetResult();
        _jobs = new JobRegistry(_session, maxConcurrent: 2);
    }

    [TearDown]
    public void TearDown()
    {
        _jobs.Dispose();
        _session.Dispose();
        try { Directory.Delete(_dir, true); }
        catch { }
    }

    private static async Task<(int Status, JsonElement Body)> Run(IResult result)
    {
        var (ctx, body) = TestCatalog.NewContext();
        ctx.RequestServices = Services;
        await result.ExecuteAsync(ctx);
        var text = TestCatalog.Text(body);
        return (ctx.Response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private Task<(int Status, JsonElement Body)> Submit(JobSubmitRequest request) =>
        Run(JobsEndpoints.Submit(request, _jobs));

    private static JobSubmitRequest Rescan(int catalogId) => new() { Kind = JobKind.Rescan, CatalogId = catalogId };

    private JobSubmitRequest Create(string fileName, string root = null) =>
        new() { Kind = JobKind.Create, RootPath = root ?? _data[0], FileName = fileName };

    private Task<JobDto> WaitForEnd(long id) => TestCatalog.WaitForJob(_jobs, id, j => j.IsTerminal);

    [Test]
    public async Task Submitting_a_rescan_is_202_with_the_job_as_camel_case_json_with_lowercase_enums()
    {
        var (status, job) = await Submit(Rescan(1));

        Assert.That(status, Is.EqualTo(StatusCodes.Status202Accepted));
        Assert.That(job.GetProperty("id").GetInt64(), Is.GreaterThan(0));
        Assert.That(job.GetProperty("kind").GetString(), Is.EqualTo("rescan"));
        Assert.That(job.GetProperty("catalogId").GetInt32(), Is.EqualTo(1));
        Assert.That(job.GetProperty("catalogName").GetString(), Is.EqualTo("b"));
        Assert.That(job.GetProperty("title").GetString(), Does.StartWith("Rescanning ").And.Contain("data1"));
        Assert.That(job.GetProperty("state").GetString(), Is.AnyOf("queued", "running"));
        Assert.That(job.GetProperty("queuedAtUtc").GetDateTime().Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(job.TryGetProperty("isTerminal", out _), Is.False);

        var done = await WaitForEnd(job.GetProperty("id").GetInt64());
        Assert.That(done.State, Is.EqualTo(JobState.Succeeded));
    }

    [Test]
    public async Task Submitting_a_create_is_202_and_the_job_gets_the_new_catalog_id_when_it_succeeds()
    {
        var root = Path.Combine(_dir, "fresh");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "f.txt"), "f");

        var (status, job) = await Submit(Create("fresh", root));

        Assert.That(status, Is.EqualTo(StatusCodes.Status202Accepted));
        Assert.That(job.GetProperty("kind").GetString(), Is.EqualTo("create"));
        Assert.That(job.GetProperty("catalogId").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(job.GetProperty("catalogName").GetString(), Is.EqualTo("fresh"));
        var done = await WaitForEnd(job.GetProperty("id").GetInt64());
        Assert.That(done.State, Is.EqualTo(JobState.Succeeded), () => done.Error?.Message);
        Assert.That(done.CatalogId, Is.EqualTo(2));
        Assert.That(done.Result.SavedPath, Is.EqualTo(Path.Combine(Path.GetFullPath(_cat), "fresh.cdex")).IgnoreCase);
        Assert.That(_session.CatalogCount, Is.EqualTo(3));
    }

    [Test]
    public async Task A_create_body_with_hash_options_is_parsed_hashes_the_new_catalog_and_reports_the_hash_result()
    {
        var root = Path.Combine(_dir, "fresh");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "x1.bin"), new byte[3000]);
        File.WriteAllBytes(Path.Combine(root, "x2.bin"), new byte[3000]);
        var request = JsonSerializer.Deserialize(
            """{"kind":"create","rootPath":""" + JsonSerializer.Serialize(root) +
            ""","fileName":"fresh","hash":{"filter":"ext:bin","includeUniqueSizes":false}}""",
            AppCoreJsonContext.Default.JobSubmitRequest);

        var (status, job) = await Submit(request);

        Assert.That(status, Is.EqualTo(StatusCodes.Status202Accepted));
        var done = await WaitForEnd(job.GetProperty("id").GetInt64());
        Assert.That(done.State, Is.EqualTo(JobState.Succeeded), () => done.Error?.Message);
        Assert.That(done.Result.Hash.FilesHashed, Is.EqualTo(2));
        var (_, list) = await Run(JobsEndpoints.List(_jobs));
        Assert.That(list.GetProperty("jobs").EnumerateArray().Single().GetProperty("result").GetProperty("hash").GetRawText(),
            Is.EqualTo("""{"filesHashed":2,"filesFailed":0}"""));
    }

    [Test]
    public async Task A_create_with_an_invalid_hash_filter_is_a_400_before_any_scan()
    {
        var request = Create("fresh");
        request.Hash = new HashOptions { Filter = "size:>>" };

        await AssertRefused(request, 400, JobErrorCodes.InvalidFilter);

        Assert.That(_jobs.GetJobs(), Is.Empty);
        Assert.That(File.Exists(Path.Combine(_cat, "fresh.cdex")), Is.False);
    }

    [Test]
    public async Task Submitting_a_hash_is_202_and_the_finished_job_carries_the_hash_result()
    {
        var (status, job) = await Submit(new JobSubmitRequest
        {
            Kind = JobKind.Hash, CatalogIds = [0, 1], Hash = new HashOptions { Filter = "a.txt" }
        });

        Assert.That(status, Is.EqualTo(StatusCodes.Status202Accepted));
        Assert.That(job.GetProperty("kind").GetString(), Is.EqualTo("hash"));
        Assert.That(job.GetProperty("catalogIds").EnumerateArray().Select(e => e.GetInt32()), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(job.GetProperty("catalogId").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(job.GetProperty("catalogName").GetString(), Is.EqualTo("a, b"));
        Assert.That(job.GetProperty("title").GetString(), Is.EqualTo("Hashing 2 catalogs"));

        await WaitForEnd(job.GetProperty("id").GetInt64());
        var (_, list) = await Run(JobsEndpoints.List(_jobs));
        var done = list.GetProperty("jobs").EnumerateArray().Single();
        Assert.That(done.GetProperty("state").GetString(), Is.EqualTo("succeeded"));
        Assert.That(done.GetProperty("phase").GetString(), Is.EqualTo("swapping"));
        Assert.That(done.GetProperty("hash").GetRawText(),
            Is.EqualTo("""{"filesDone":2,"filesTotal":2,"bytesDone":2,"bytesTotal":2}"""));
        Assert.That(done.GetProperty("result").GetProperty("hash").GetRawText(),
            Is.EqualTo("""{"filesHashed":2,"filesFailed":0}"""));
        Assert.That(done.GetProperty("result").GetProperty("catalogId").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Hash_refusals_are_json_errors_with_the_contract_status()
    {
        JobSubmitRequest Hash(int[] ids, string filter = null) =>
            new() { Kind = JobKind.Hash, CatalogIds = ids, Hash = new HashOptions { Filter = filter } };

        await AssertRefused(Hash([0], "size:>>"), 400, JobErrorCodes.InvalidFilter);
        await AssertRefused(Hash([]), 400, JobErrorCodes.InvalidRequest);
        await AssertRefused(Hash([0, 9]), 404, JobErrorCodes.UnknownCatalog);
        using (new TestCatalog.SwapBlockingLease(_session, 0))
        {
            var (_, rescan) = await Submit(Rescan(0));
            await AssertRefused(Hash([0, 1]), 409, JobErrorCodes.AlreadyRunning);
            await TestCatalog.WaitForJob(_jobs, rescan.GetProperty("id").GetInt64(), j => j.Phase == "swapping");
        }

        await TestCatalog.WaitForJob(_jobs, 1, j => j.IsTerminal);
        Directory.Delete(_data[1], true);
        await AssertRefused(Hash([0, 1]), 422, JobErrorCodes.RootNotAccessible);
        Assert.That(JobsEndpoints.StatusFor(JobErrorCodes.LegacyCatalog), Is.EqualTo(422));
        Assert.That(_jobs.GetJobs().Select(j => j.Kind), Is.All.EqualTo(JobKind.Rescan));
    }

    [Test]
    public async Task Refusals_are_json_errors_with_the_contract_status()
    {
        Directory.Delete(_data[1], true);
        File.WriteAllText(Path.Combine(_cat, "stray.cdex"), "x");

        await AssertRefused(Rescan(9), 404, JobErrorCodes.UnknownCatalog);
        await AssertRefused(Rescan(1), 422, JobErrorCodes.RootNotAccessible);
        await AssertRefused(Create(@"a\b"), 422, JobErrorCodes.InvalidFileName);
        await AssertRefused(Create("stray"), 409, JobErrorCodes.FileExists);
        await AssertRefused(Create("x", Path.Combine(_dir, "nope")), 422, JobErrorCodes.RootNotAccessible);
        await AssertRefused(new JobSubmitRequest { Kind = JobKind.Rescan }, 400, JobErrorCodes.InvalidRequest);
        Assert.That(_jobs.GetJobs(), Is.Empty);
    }

    [Test]
    public async Task A_second_job_for_the_same_catalog_or_file_name_is_409_already_running()
    {
        _jobs.Dispose();
        _jobs = new JobRegistry(_session, maxConcurrent: 1);
        long createId;
        using (new TestCatalog.SwapBlockingLease(_session, 0))
        {
            var (_, first) = await Submit(Rescan(0));
            createId = (await Submit(Create("fresh"))).Body.GetProperty("id").GetInt64();

            await AssertRefused(Rescan(0), 409, JobErrorCodes.AlreadyRunning);
            await AssertRefused(Create("fresh", _data[1]), 409, JobErrorCodes.AlreadyRunning);

            await TestCatalog.WaitForJob(_jobs, first.GetProperty("id").GetInt64(), j => j.Phase == "swapping");
        }

        await WaitForEnd(createId);
        Assert.That(_jobs.GetJobs().Select(j => j.State), Is.All.EqualTo(JobState.Succeeded));
    }

    [Test]
    public async Task A_load_in_progress_refuses_submits_as_409_busy()
    {
        using var hold = _jobs.TryBeginExclusive();

        await AssertRefused(Rescan(0), 409, JobErrorCodes.Busy);
    }

    [Test]
    public async Task List_returns_the_jobs_and_the_cap()
    {
        var (_, job) = await Submit(Rescan(0));
        await WaitForEnd(job.GetProperty("id").GetInt64());

        var (status, list) = await Run(JobsEndpoints.List(_jobs));

        Assert.That(status, Is.EqualTo(200));
        Assert.That(list.GetProperty("maxConcurrent").GetInt32(), Is.EqualTo(2));
        var listed = list.GetProperty("jobs").EnumerateArray().Single();
        Assert.That(listed.GetProperty("state").GetString(), Is.EqualTo("succeeded"));
        Assert.That(listed.GetProperty("result").GetProperty("entries").GetInt64(), Is.EqualTo(1));
        Assert.That(listed.GetProperty("endedAtUtc").GetDateTime(), Is.GreaterThanOrEqualTo(listed.GetProperty("startedAtUtc").GetDateTime()));
    }

    [Test]
    public async Task Cancel_is_204_and_unknown_is_404()
    {
        var bytes = File.ReadAllBytes(_cdex[0]);
        File.WriteAllText(Path.Combine(_data[0], "new.txt"), "n");
        long id;
        using (new TestCatalog.SwapBlockingLease(_session, 0))
        {
            id = (await Submit(Rescan(0))).Body.GetProperty("id").GetInt64();
            await TestCatalog.WaitForJob(_jobs, id, j => j.Phase == "swapping");

            Assert.That((await Run(JobsEndpoints.Cancel(id, _jobs))).Status, Is.EqualTo(204));
            Assert.That((await WaitForEnd(id)).State, Is.EqualTo(JobState.Cancelled));
        }

        Assert.That(File.ReadAllBytes(_cdex[0]), Is.EqualTo(bytes));
        Assert.That((await Run(JobsEndpoints.Cancel(id, _jobs))).Status, Is.EqualTo(204), "finished: a no-op");
        Assert.That((await Run(JobsEndpoints.Cancel(999, _jobs))).Status, Is.EqualTo(404));
    }

    [Test]
    public async Task Dismissing_is_204_for_a_finished_job_409_for_an_active_one_and_404_for_an_unknown_one()
    {
        var done = (await Submit(Rescan(1))).Body.GetProperty("id").GetInt64();
        await WaitForEnd(done);
        using (new TestCatalog.SwapBlockingLease(_session, 0))
        {
            var active = (await Submit(Rescan(0))).Body.GetProperty("id").GetInt64();

            Assert.That((await Run(JobsEndpoints.Dismiss(active, _jobs))).Status, Is.EqualTo(409));
            Assert.That((await Run(JobsEndpoints.Dismiss(999, _jobs))).Status, Is.EqualTo(404));
            Assert.That((await Run(JobsEndpoints.DismissFinished(_jobs))).Status, Is.EqualTo(204));
            Assert.That(_jobs.GetJobs().Select(j => j.Id), Is.EqualTo(new[] { active }));

            await TestCatalog.WaitForJob(_jobs, active, j => j.Phase == "swapping");
        }

        var last = _jobs.GetJobs().Single().Id;
        await WaitForEnd(last);
        Assert.That((await Run(JobsEndpoints.Dismiss(last, _jobs))).Status, Is.EqualTo(204));
        Assert.That(_jobs.GetJobs(), Is.Empty);
    }

    [Test]
    public async Task Events_stream_a_snapshot_then_a_job_event_per_change()
    {
        var earlier = (await Submit(Rescan(1))).Body.GetProperty("id").GetInt64();
        await WaitForEnd(earlier);
        var (ctx, body) = TestCatalog.NewContext();
        var stream = JobsEndpoints.EventsAsync(ctx, _jobs);

        var id = (await Submit(Rescan(0))).Body.GetProperty("id").GetInt64();
        await WaitForEnd(id);
        _jobs.Dispose();
        await stream.WaitAsync(TimeSpan.FromSeconds(10));

        var events = TestCatalog.Events(TestCatalog.Text(body));
        Assert.That(ctx.Response.ContentType, Is.EqualTo("text/event-stream"));
        Assert.That(events[0].Event, Is.EqualTo("snapshot"));
        Assert.That(events[0].Data.GetProperty("maxConcurrent").GetInt32(), Is.EqualTo(2));
        Assert.That(events[0].Data.GetProperty("jobs").EnumerateArray().Select(j => j.GetProperty("id").GetInt64()),
            Is.EqualTo(new[] { earlier }));
        var states = events.Skip(1).Select(e =>
        {
            Assert.That(e.Event, Is.EqualTo("job"));
            Assert.That(e.Data.GetProperty("id").GetInt64(), Is.EqualTo(id));
            return e.Data.GetProperty("state").GetString();
        }).ToList();
        Assert.That(states.First(), Is.EqualTo("queued"));
        Assert.That(states, Does.Contain("running"));
        Assert.That(states.Last(), Is.EqualTo("succeeded"));
    }

    [TestCase(JobErrorCodes.UnknownCatalog, 404)]
    [TestCase(JobErrorCodes.AlreadyRunning, 409)]
    [TestCase(JobErrorCodes.Busy, 409)]
    [TestCase(JobErrorCodes.CatalogUnavailable, 409)]
    [TestCase(JobErrorCodes.FileExists, 409)]
    [TestCase(JobErrorCodes.RootNotAccessible, 422)]
    [TestCase(JobErrorCodes.NoSourceFile, 422)]
    [TestCase(JobErrorCodes.InvalidFileName, 422)]
    [TestCase(JobErrorCodes.NoCatalogDir, 422)]
    [TestCase(JobErrorCodes.FolderNotWritable, 422)]
    [TestCase(JobErrorCodes.InvalidRequest, 400)]
    public void Each_refusal_code_has_its_contract_status(string code, int status)
    {
        Assert.That(JobsEndpoints.StatusFor(code), Is.EqualTo(status));
    }

    private async Task AssertRefused(JobSubmitRequest request, int status, string code)
    {
        var (actualStatus, error) = await Submit(request);
        Assert.That(actualStatus, Is.EqualTo(status), code);
        Assert.That(error.GetProperty("code").GetString(), Is.EqualTo(code));
        Assert.That(error.GetProperty("message").GetString(), Is.Not.Empty);
    }
}
