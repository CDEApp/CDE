using System.Threading.Tasks;
using Serilog;

namespace cde;

internal static class FireAndForget
{
    /// <summary>
    /// For progress callbacks that are synchronous Actions: do not await the publish, but observe a
    /// fault (logging it) so it is never an unobserved task exception.
    /// </summary>
    public static void Observe(Task task, string what)
    {
        if (task.IsCompletedSuccessfully) return;
        _ = task.ContinueWith(
            t => Log.Warning(t.Exception, "Background publish of {What} failed", what),
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
    }
}
