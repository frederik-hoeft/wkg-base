using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Queuing.Routing;
using Cash.Threading.Workloads.Scheduling;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Queuing.Classless.Fifo;

/// <summary>
/// A qdisc that implements the First-In-First-Out (FIFO) scheduling algorithm.
/// </summary>
/// <typeparam name="THandle">The type of the handle.</typeparam>
/// <param name="handle">The handle of the qdisc.</param>
/// <param name="filters">The predicate used to determine if a workload can be scheduled.</param>
internal sealed class FifoQdisc<THandle>(THandle handle, IFilterManager filters) : ClasslessQdisc<THandle>(handle, filters) where THandle : unmanaged
{
    private readonly ConcurrentQueue<AbstractWorkloadBase> _queue = [];

    public override bool IsEmpty => _queue.IsEmpty;

    public override int BestEffortCount => _queue.Count;

    protected override void EnqueueDirectLocal(AbstractWorkloadBase workload) => _queue.Enqueue(workload);

    protected override bool TryDequeueInternal(WorkerContext worker, bool backTrack, [NotNullWhen(true)] out AbstractWorkloadBase? workload) => _queue.TryDequeue(out workload);

    protected override bool TryEnqueueByHandle(THandle handle, AbstractWorkloadBase workload) => false;

    protected override bool TryEnqueue(object? state, AbstractWorkloadBase workload)
    {
        if (Filters.Match(state))
        {
            EnqueueDirect(workload);
            return true;
        }
        return false;
    }

    protected override bool TryFindRoute(THandle handle, ref RoutingPath<THandle> path) => false;

    protected override bool TryPeekUnsafe(WorkerContext worker, [NotNullWhen(true)] out AbstractWorkloadBase? workload) => _queue.TryPeek(out workload);

    protected override bool TryRemoveInternal(AwaitableWorkload workload) => false;

    public override string ToString() => $"FIFO qdisc (handle: {Handle}, count: {BestEffortCount})";
}
