using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.Scheduling;
using Cash.Threading.Workloads.WorkloadTypes;
using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Factories;

public abstract class WorkloadFactory<THandle> : IDisposable where THandle : unmanaged
{
    private bool _disposedValue;

    internal protected IWorkloadScheduler<THandle> Scheduler { get; }

    private protected AnonymousWorkloadPoolManager? Pool { get; }

    public WorkloadContextOptions DefaultOptions { get; }

    private protected WorkloadFactory(IWorkloadScheduler<THandle> root, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options)
    {
        Scheduler = root;
        Pool = pool;
        DefaultOptions = options ?? new WorkloadContextOptions();
    }

    [MemberNotNullWhen(true, nameof(Pool))]
    private protected bool SupportsPooling => Pool is not null;

    private protected IClassifyingQdisc<THandle> Root => Scheduler.Root;

    protected void CheckDisposed() => ObjectDisposedException.ThrowIf(_disposedValue, this);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Scheduler.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}