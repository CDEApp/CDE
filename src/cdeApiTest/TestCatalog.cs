using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using cdeApi;
using cdeAppCore;
using cdeAppCore.Dtos;
using cdeAppCore.Session;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using Microsoft.AspNetCore.Http;

namespace cdeApiTest;

/// <summary>
/// A one-catalog <see cref="CatalogSession"/> over a temp folder, for driving the endpoint handlers directly.
/// <code>
/// data/
///   a.txt
///   sub/
///     b.txt
/// </code>
/// </summary>
internal sealed class TestCatalog : IDisposable
{
    public string Dir { get; }
    public string Data { get; }
    public string Cdex { get; }
    public RecordingLoader Loader { get; }
    public CatalogSession Session { get; }

    public TestCatalog()
    {
        Dir = Path.Combine(Path.GetTempPath(), "cde-api-test-" + Guid.NewGuid().ToString("N"));
        Data = Path.Combine(Dir, "data");
        Directory.CreateDirectory(Path.Combine(Data, "sub"));
        File.WriteAllText(Path.Combine(Data, "a.txt"), "a");
        File.WriteAllText(Path.Combine(Data, "sub", "b.txt"), "bb");

        Cdex = Path.Combine(Dir, "one.cdex");
        WriteCatalog(Data, Cdex);

        Loader = new RecordingLoader(Cdex);
        Session = new CatalogSession(Loader, null);
        Session.LoadAsync("initial").GetAwaiter().GetResult();
    }

    /// <summary>Scans <paramref name="root"/> into a .cdex at <paramref name="cdex"/>.</summary>
    public static void WriteCatalog(string root, string cdex)
    {
        var re = new RootEntry();
        re.PopulateRoot(root);
        re.SortAllChildrenByPath();
        re.SetSummaryFields();
        re.ActualFileName = cdex;
        ColumnarFormat.Write(re, cdex);
    }

    /// <summary>The entry ref (<c>catalog-index</c>) of a direct child of the root, by name.</summary>
    public string RefOf(string name)
    {
        var node = Session.GetChildren(new EntryRefDto(0, 0)).Single(n => n.Name == name);
        return $"{node.Ref.CatalogId}-{node.Ref.EntryIndex}";
    }

    public void Dispose()
    {
        Session.Dispose();
        try { Directory.Delete(Dir, true); }
        catch { /* best effort */ }
    }

    public static (DefaultHttpContext ctx, MemoryStream body) NewContext(string queryString = null)
    {
        var body = new MemoryStream();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = body;
        if (queryString != null) ctx.Request.QueryString = new QueryString(queryString);
        return (ctx, body);
    }

    public static string Text(MemoryStream body) => Encoding.UTF8.GetString(body.ToArray());

    /// <summary>Splits an SSE body into (event, data) pairs.</summary>
    public static List<(string Event, JsonElement Data)> Events(string sse) =>
        sse.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(frame =>
        {
            var lines = frame.Split('\n');
            return (lines[0]["event: ".Length..], JsonDocument.Parse(lines[1]["data: ".Length..]).RootElement.Clone());
        }).ToList();

    /// <summary>Always serves the one .cdex, and records the folder each load asked for.</summary>
    internal sealed class RecordingLoader(string cdex) : ILoadCatalogService
    {
        public List<string> RequestedPaths { get; } = [];

        public Task<List<RootEntry>> LoadRootEntriesAsync(string configPath, Action<int, int, string> progressCallback,
            CancellationToken cancellationToken = default) => Task.FromResult(new List<RootEntry>());

        public IList<string> GetColumnarFiles(string configPath)
        {
            RequestedPaths.Add(configPath);
            return [cdex];
        }
    }
}
