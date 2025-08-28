using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Queuing.Routing;
using Cash.Threading.Workloads.Scheduling;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Queuing.Classless.WorkStealing;

internal sealed class WorkStealingQdisc<THandle>(THandle handle, IFilterManager filters) : ClasslessQdisc<THandle>(handle, filters) where THandle : unmanaged
{
    private readonly ConcurrentBag<AbstractWorkloadBase> _queue = [];

    public override bool IsEmpty => _queue.IsEmpty;

    public override int BestEffortCount => _queue.Count;

    protected override void EnqueueDirectLocal(AbstractWorkloadBase workload) => _queue.Add(workload);

    protected override bool TryDequeueInternal(WorkerContext worker, bool backTrack, [NotNullWhen(true)] out AbstractWorkloadBase? workload) => _queue.TryTake(out workload);

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

    public override string ToString() => $"WorkStealing qdisc (handle: {Handle}, count: {BestEffortCount})";
}
