using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModManagerEnhancements.Core;

internal sealed class ScanOutcome
{
    internal ConflictReport? Report { get; }
    internal Exception? Error { get; }
    internal ScanOutcome(ConflictReport? report, Exception? error) { Report = report; Error = error; }
}

// All state changes and result publication happen on the owner (UI) thread.
// Workers only inspect immutable snapshots and files. Obsolete results never reach the UI.
internal sealed class ScanSession : IDisposable
{
    private sealed class Work
    {
        internal readonly int Revision;
        internal readonly CancellationTokenSource Cancellation = new();
        internal readonly Task<ScanOutcome?> Task;
        internal Work(int revision, IReadOnlyList<ModSource> sources, Func<IReadOnlyList<ModSource>, CancellationToken, ConflictReport> scan)
        {
            Revision = revision;
            Task = System.Threading.Tasks.Task.Run(() =>
            {
                try { return new ScanOutcome(scan(sources, Cancellation.Token), null); }
                catch (OperationCanceledException) when (Cancellation.IsCancellationRequested) { return null; }
                catch (Exception error) { return new ScanOutcome(null, error); }
            });
        }
    }

    private readonly Func<IReadOnlyList<ModSource>, CancellationToken, ConflictReport> scan;
    private readonly Action<Exception> reportError;
    private readonly List<Work> pending = new();
    private int revision;
    private bool disposed;
    internal bool HasPending => pending.Count != 0;
    internal ScanSession(Func<IReadOnlyList<ModSource>, CancellationToken, ConflictReport> scan, Action<Exception> reportError)
    { this.scan = scan; this.reportError = reportError; }

    internal void Request(IReadOnlyList<ModSource> sources)
    {
        if (disposed) throw new ObjectDisposedException(nameof(ScanSession));
        foreach (var work in pending) work.Cancellation.Cancel();
        pending.Add(new Work(++revision, sources, scan));
    }

    internal ScanOutcome? Poll()
    {
        ScanOutcome? latest = null;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var work = pending[i];
            if (!work.Task.IsCompleted) continue;
            var outcome = work.Task.GetAwaiter().GetResult();
            pending.RemoveAt(i);
            work.Cancellation.Dispose();
            if (outcome?.Error != null) reportError(outcome.Error);
            if (work.Revision == revision) latest = outcome;
        }
        return latest;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var work in pending)
        {
            work.Cancellation.Cancel();
            _ = work.Task.ContinueWith(task =>
            {
                try { if (task.Result?.Error != null) reportError(task.Result.Error); }
                finally { work.Cancellation.Dispose(); }
            }, TaskScheduler.Default);
        }
        pending.Clear();
    }
}
