using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;
using cdeLib.Output;
using NUnit.Framework;

namespace cdeLibTest.Output;

/// <summary>
/// The records <c>find</c>, <c>dupes</c> and <c>hash</c> write (#173): the name comes from the catalog entry, not from
/// re-parsing the path, and the CSV header and JSON keys come from the same column list in the same order.
/// </summary>
[TestFixture]
public class EntryRecordsTests
{
    private const string AwkwardName = "back\\slash.txt";

    private string _dir;

    [SetUp]
    public void SetUp() => _dir = Directory.CreateTempSubdirectory("cde-entry-records").FullName;

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { /* ignore */ }
    }

    // Built directly, not from a real file: a name with a backslash can't be a file name on Windows.
    private ColumnarCatalogReader Catalog()
    {
        var root = new RootEntry { Path = Path.Combine(_dir, "data") };
        root.AddChild(new DirEntry(false)
        {
            Path = AwkwardName,
            Size = 42,
            Modified = new DateTime(2024, 3, 5, 6, 7, 8, DateTimeKind.Utc),
        });
        root.SetInMemoryFields();
        var path = Path.Combine(_dir, "one.cdex");
        ColumnarFormat.Write(EntryStore.Build(root), path);
        return new ColumnarCatalogReader(path);
    }

    private static int IndexOfAwkward(IEntrySource source) =>
        Enumerable.Range(0, source.Count).Single(i => source.FullName(i) == AwkwardName);

    private static string Json(Action<IRecordWriter> write, string[] columns)
    {
        var text = new StringWriter();
        var writer = RecordWriter.Create(OutputFormat.Json, text, columns);
        write(writer);
        writer.Flush();
        return text.ToString();
    }

    private static string Csv(Action<IRecordWriter> write, string[] columns)
    {
        var text = new StringWriter();
        var writer = RecordWriter.Create(OutputFormat.Csv, text, columns);
        write(writer);
        writer.Flush();
        return text.ToString();
    }

    private static string[] Keys(string jsonLine) =>
        JsonDocument.Parse(jsonLine).RootElement.EnumerateObject().Select(p => p.Name).ToArray();

    [Test]
    public void Find_takes_the_name_from_the_entry_so_a_backslash_in_it_survives()
    {
        using var reader = Catalog();
        var i = IndexOfAwkward(reader);

        var line = Json(w => EntryRecords.WriteFind(w, "c.cdex", reader, i), EntryRecords.FindColumns);

        Assert.That(JsonDocument.Parse(line).RootElement.GetProperty("name").GetString(), Is.EqualTo(AwkwardName));
    }

    [Test]
    public void Dupes_takes_the_name_from_the_entry_so_a_backslash_in_it_survives()
    {
        using var reader = Catalog();
        var i = IndexOfAwkward(reader);

        var line = Json(w => EntryRecords.WriteDuplicate(w, 1, "c.cdex", reader, i), EntryRecords.DupesColumns);

        Assert.That(JsonDocument.Parse(line).RootElement.GetProperty("name").GetString(), Is.EqualTo(AwkwardName));
    }

    [Test]
    public void Hash_takes_the_name_from_the_entry_so_a_backslash_in_it_survives()
    {
        using var reader = Catalog();
        var i = IndexOfAwkward(reader);

        var line = Json(w => EntryRecords.WriteHashed(w, "c.cdex", reader, i, new Hash16(7), false),
            EntryRecords.HashColumns);

        Assert.That(JsonDocument.Parse(line).RootElement.GetProperty("name").GetString(), Is.EqualTo(AwkwardName));
    }

    [Test]
    public void Find_csv_header_equals_the_json_keys_in_order()
    {
        using var reader = Catalog();
        var i = IndexOfAwkward(reader);
        void Write(IRecordWriter w) => EntryRecords.WriteFind(w, "c.cdex", reader, i);

        Assert.That(CsvText.Parse(Csv(Write, EntryRecords.FindColumns))[0],
            Is.EqualTo(Keys(Json(Write, EntryRecords.FindColumns))));
    }

    [Test]
    public void Dupes_csv_header_equals_the_json_keys_in_order()
    {
        using var reader = Catalog();
        var i = IndexOfAwkward(reader);
        void Write(IRecordWriter w) => EntryRecords.WriteDuplicate(w, 1, "c.cdex", reader, i);

        Assert.That(CsvText.Parse(Csv(Write, EntryRecords.DupesColumns))[0],
            Is.EqualTo(Keys(Json(Write, EntryRecords.DupesColumns))));
    }

    [Test]
    public void Hash_csv_header_equals_the_json_keys_in_order()
    {
        using var reader = Catalog();
        var i = IndexOfAwkward(reader);
        void Write(IRecordWriter w) => EntryRecords.WriteHashed(w, "c.cdex", reader, i, new Hash16(7), true);

        Assert.That(CsvText.Parse(Csv(Write, EntryRecords.HashColumns))[0],
            Is.EqualTo(Keys(Json(Write, EntryRecords.HashColumns))));
    }
}
