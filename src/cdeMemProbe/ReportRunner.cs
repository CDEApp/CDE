using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using cdeLib.Entities;
using cdeLib.Entities.Columnar;
using cdeLib.Entities.Soa;

namespace cdeMemProbe;

/// <summary>
/// <c>cdeMemProbe --report &lt;size&gt;</c>: builds a deterministic fixture, produces a .cdex, then measures
/// search latency and peak RSS on the mmap path in a fresh child process (so cold really is the first
/// run, and the RSS is not polluted by fixture generation).
/// </summary>
public static class ReportRunner
{
    public const int WarmRepeats = 10;

    /// <summary>Fixtures at or above this size skip the real scan: the tree is too big to materialise on disk.</summary>
    public const int DirectWriteThreshold = 5_000_000;

    /// <summary>The fixed queries; the text is recorded in perf.json. Names come from <see cref="SyntheticCatalog"/>.</summary>
    public static readonly IReadOnlyList<(string Name, string Mode, string Pattern)> Queries =
    [
        ("name substring", "name", "Report_00"),
        ("regex", "regex", @"^Backup_\d*7\.(log|zip)$"),
        ("path", "path", "dir_0000050"),
    ];

    public static int Run(string[] args, long size, Func<string, string?> flag)
    {
        var seed = int.TryParse(flag("--seed"), out var s) ? s : 42;
        var outPath = flag("--out") ?? "perf.json";
        var workDir = flag("--work") ?? Path.Combine(Path.GetTempPath(), "cde-perf");
        var direct = args.Contains("--direct") || size >= DirectWriteThreshold;
        Directory.CreateDirectory(workDir);

        string cdex;
        double? scanMs = null;
        if (direct)
        {
            cdex = Path.Combine(workDir, "synthetic.cdex");
            Console.Error.WriteLine($"Writing {size:N0} entries straight to {cdex} (no scan) ...");
            WriteDirect(size, seed, cdex);
        }
        else
        {
            var cde = flag("--cde") ?? "cde";
            var fixtureDir = Path.Combine(workDir, "fixture");
            Console.Error.WriteLine($"Materialising {size:N0} entries under {fixtureDir} ...");
            if (Directory.Exists(fixtureDir)) Directory.Delete(fixtureDir, recursive: true);
            Materialise(SyntheticCatalog.Generate((int)size, seed: seed), fixtureDir);
            (cdex, scanMs) = Scan(cde, fixtureDir, workDir);
        }

        var cacheDropped = DropPageCache();
        var child = RunSearchChild(cdex);

        var report = new PerfReport
        {
            Commit = flag("--commit") ?? Environment.GetEnvironmentVariable("GITHUB_SHA") ?? GitCommit(),
            Os = RuntimeInformation.OSDescription,
            FixtureEntries = size,
            Seed = seed,
            ScanMs = scanMs,
            CdexBytes = new FileInfo(cdex).Length,
            ColdCacheDropped = cacheDropped,
            PeakRssBytes = child.PeakRssBytes,
            Queries = child.Queries,
        };
        File.WriteAllText(outPath, report.ToJson());
        Console.Error.WriteLine($"Wrote {outPath}");
        Console.WriteLine(outPath);
        return 0;
    }

    /// <summary>Child mode (<c>--search-child &lt;file.cdex&gt;</c>): prints the search results as perf.json fields.</summary>
    public static int RunSearchChildMode(string cdex)
    {
        var timings = new List<QueryTiming>();
        using (var reader = new ColumnarCatalogReader(cdex))
        {
            foreach (var (name, mode, pattern) in Queries)
            {
                var cold = TimeSearch(reader, mode, pattern, out var matches);
                var warm = Enumerable.Range(0, WarmRepeats)
                    .Select(_ => TimeSearch(reader, mode, pattern, out _))
                    .ToArray();
                timings.Add(new QueryTiming(name, mode, pattern, cold, Median(warm), matches));
            }
        }

        using var proc = Process.GetCurrentProcess();
        var result = new PerfReport { PeakRssBytes = proc.PeakWorkingSet64, Queries = timings };
        Console.WriteLine(result.ToJson());
        return 0;
    }

    public static double Median(double[] values)
    {
        var sorted = values.Order().ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static double TimeSearch(ColumnarCatalogReader reader, string mode, string pattern, out int matches)
    {
        var sw = Stopwatch.StartNew();
        matches = reader.Find(pattern, regexMode: mode == "regex", includePath: mode == "path",
            includeFiles: true, includeFolders: true);
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    private static PerfReport RunSearchChild(string cdex)
    {
        var self = Environment.ProcessPath ?? throw new InvalidOperationException("cannot locate own executable");
        var psi = new ProcessStartInfo(self) { RedirectStandardOutput = true, UseShellExecute = false };
        // "dotnet cdeMemProbe.dll ..." when run via the host; the apphost needs no extra argument.
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            psi.ArgumentList.Add(typeof(ReportRunner).Assembly.Location);
        psi.ArgumentList.Add("--search-child");
        psi.ArgumentList.Add(cdex);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("could not start search child");
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"search child failed with exit code {p.ExitCode}");
        return PerfReport.FromJson(stdout);
    }

    private static (string Cdex, double ScanMs) Scan(string cde, string fixtureDir, string workDir)
    {
        foreach (var old in Directory.GetFiles(workDir, "*.cdex")) File.Delete(old);

        var psi = new ProcessStartInfo(cde)
        {
            WorkingDirectory = workDir, UseShellExecute = false, RedirectStandardOutput = true,
        };
        psi.ArgumentList.Add("scan");
        psi.ArgumentList.Add(fixtureDir);
        var sw = Stopwatch.StartNew();
        using (var p = Process.Start(psi) ?? throw new InvalidOperationException($"could not start {cde}"))
        {
            // The scan's progress text goes to stderr so stdout stays just the perf.json path.
            Console.Error.Write(p.StandardOutput.ReadToEnd());
            p.WaitForExit();
            if (p.ExitCode != 0) throw new InvalidOperationException($"{cde} scan failed with exit code {p.ExitCode}");
        }
        sw.Stop();

        var produced = Directory.GetFiles(workDir, "*.cdex");
        if (produced.Length != 1)
            throw new InvalidOperationException($"expected one .cdex in {workDir}, found {produced.Length}");
        return (produced[0], sw.Elapsed.TotalMilliseconds);
    }

    private static void WriteDirect(long size, int seed, string cdex)
    {
        var store = EntryStore.Build(SyntheticCatalog.Generate((int)size, seed: seed));
        ColumnarFormat.Write(store, cdex);
    }

    /// <summary>Creates the tree as real directories holding empty files, skipping the synthetic root.</summary>
    public static void Materialise(RootEntry root, string target)
    {
        Directory.CreateDirectory(target);
        var stack = new Stack<(IList<DirEntry>? Children, string Path)>();
        stack.Push((root.Children, target));
        while (stack.Count > 0)
        {
            var (children, path) = stack.Pop();
            if (children is null) continue;
            foreach (var child in children)
            {
                var childPath = System.IO.Path.Combine(path, child.Path);
                if (child.IsDirectory)
                {
                    Directory.CreateDirectory(childPath);
                    stack.Push((child.Children, childPath));
                }
                else
                {
                    File.Create(childPath).Dispose();
                }
            }
        }
    }

    /// <summary>Drops the Linux page cache so the next search is cold. Returns false when it could not.</summary>
    private static bool DropPageCache()
    {
        if (!OperatingSystem.IsLinux()) return false;
        try
        {
            RunQuiet("sync");
            File.WriteAllText("/proc/sys/vm/drop_caches", "3");
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return RunQuiet("sudo", "-n", "sh", "-c", "sync; echo 3 > /proc/sys/vm/drop_caches");
        }
    }

    private static bool RunQuiet(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardError = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string GitCommit()
    {
        try
        {
            var psi = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true };
            psi.ArgumentList.Add("rev-parse");
            psi.ArgumentList.Add("HEAD");
            using var p = Process.Start(psi);
            var output = p?.StandardOutput.ReadToEnd().Trim();
            p?.WaitForExit();
            return string.IsNullOrEmpty(output) ? "unknown" : output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "unknown";
        }
    }
}
