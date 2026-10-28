using System.Threading;

namespace cdeLib;

/// <summary>
/// Cooperative cancellation for long-running console operations, driven by Ctrl-C. Replaces the old
/// global <c>Hack.BreakConsoleFlag</c> with an injectable signal that exposes a real
/// <see cref="System.Threading.CancellationToken"/>.
/// <para>
/// It is <em>resettable</em>: a phase can call <see cref="Reset"/> to obtain a fresh, un-cancelled token,
/// so the user can interrupt the next phase independently. This mirrors the original "press break again"
/// behaviour used by the two-phase hashing and the find REPL.
/// </para>
/// </summary>
public sealed class OperationCancellation
{
    private CancellationTokenSource _cts = new();
    private int _breakCount;

    /// <summary>Token for the current operation/phase. Cancelled when the user requests a break.</summary>
    public CancellationToken Token => _cts.Token;

    /// <summary>True once a break has been requested for the current operation/phase.</summary>
    public bool IsCancellationRequested => _cts.IsCancellationRequested;

    /// <summary>
    /// Breaks requested since the last <see cref="Reset"/>. More than one means the user pressed Ctrl-C again while
    /// the phase was still stopping, which a phase that is about to <see cref="Reset"/> must not lose.
    /// </summary>
    public int BreakCount => Volatile.Read(ref _breakCount);

    /// <summary>Request cancellation of the current operation/phase (wired to Ctrl-C).</summary>
    public void Cancel()
    {
        Interlocked.Increment(ref _breakCount);
        _cts.Cancel();
    }

    /// <summary>Begin a fresh operation/phase with a new, un-cancelled token.</summary>
    public void Reset()
    {
        var old = _cts;
        _cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _breakCount, 0);
        old.Dispose();
    }
}
