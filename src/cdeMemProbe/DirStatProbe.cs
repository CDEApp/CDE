using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using cdeLib.Entities.Columnar;

namespace cdeMemProbe;

/// <summary>
/// <c>--dir-stat &lt;catalog.cdex&gt; [--threads N]</c> (#235 A2 gate). The floor cost of a quick rescan: read the
/// live modified time of every directory in the catalog, one stat call each, and count how many still match.
/// A quick rescan can be no faster than this, so it is compared against the full rescan time.
/// </summary>
public static class DirStatProbe
{
    public static int Run(string cdex, Func<string, string?> flag)
    {
        var threads = int.TryParse(flag("--threads"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) && t > 0 ? t : 1;
        using var reader = new ColumnarCatalogReader(cdex);

        var sw = Stopwatch.StartNew();
        var dirs = new System.Collections.Generic.List<(string Path, long Ticks)>();
        for (var i = 1; i < reader.Count; i++)
        {
            if (reader.IsDirectory(i)) dirs.Add((reader.FullPath(i), reader.ModifiedOf(i).ToUniversalTime().Ticks));
        }
        var pathsMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        long same = 0, changed = 0, missing = 0;
        Parallel.For(0, dirs.Count, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            var (path, ticks) = dirs[i];
            try
            {
                var info = new DirectoryInfo(path);
                if (!info.Exists) { Interlocked.Increment(ref missing); return; }
                if (info.LastWriteTimeUtc.Ticks == ticks) Interlocked.Increment(ref same);
                else Interlocked.Increment(ref changed);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Interlocked.Increment(ref missing);
            }
        });

        Console.WriteLine("dirs,threads,buildPathsMs,statMs,same,changed,missing");
        Console.WriteLine(string.Join(',', dirs.Count, threads, pathsMs.ToString("0", CultureInfo.InvariantCulture),
            sw.Elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture), same, changed, missing));
        return 0;
    }
}
