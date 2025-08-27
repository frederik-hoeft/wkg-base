using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Queuing.Routing;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Queuing.Classless.Lifo;

/// <summary>
/// A qdisc that implements the Last-In-First-Out (LIFO) scheduling algorithm.
/// </summary>
/// <typeparam name="THandle">The type of the handle.</typeparam>
internal sealed class LifoQdisc<THandle>(THandle handle, IFilterManager filters) : ClasslessQdisc<THandle>(handle, filters) where THandle : unmanaged
{
    private readonly ConcurrentStack<AbstractWorkloadBase> _stack = new();

    public override bool IsEmpty => _stack.IsEmpty;

    public override int BestEffortCount => _stack.Count;

    protected override void EnqueueDirectLocal(AbstractWorkloadBase workload) => _stack.Push(workload);

    protected override bool TryDequeueInternal(int workerId, bool backTrack, [NotNullWhen(true)] out AbstractWorkloadBase? workload) => 
        _stack.TryPop(out workload);

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

    protected override bool TryPeekUnsafe(int workerId, [NotNullWhen(true)] out AbstractWorkloadBase? workload) => _stack.TryPeek(out workload);

    protected override bool TryRemoveInternal(AwaitableWorkload workload) => false;

    public override string ToString() => $"LIFO qdisc (handle: {Handle}, count: {BestEffortCount})";
}
