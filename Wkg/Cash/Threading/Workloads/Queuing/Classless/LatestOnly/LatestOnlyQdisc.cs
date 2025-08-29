using Cash.Diagnostic;
using Cash.Threading.Workloads.Queuing.Routing;
using System.Diagnostics.CodeAnalysis;
using Cash.Threading.Workloads.Exceptions;
using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Scheduling;

namespace Cash.Threading.Workloads.Queuing.Classless.LatestOnly;

internal sealed class LatestOnlyQdisc<THandle>(THandle handle, IFilterManager filters) : ClassifyingQdisc<THandle>(handle, filters) where THandle : unmanaged
{
    private volatile AbstractWorkloadBase? _singleWorkload;

    public override bool IsEmpty => _singleWorkload is null;

    public override int BestEffortCount => IsEmpty ? 0 : 1;

    protected override bool CanClassify(object? state) => Filters.Match(state);

    public override bool TryFindChild(THandle handle, [NotNullWhen(true)] out IClassifyingQdisc<THandle>? child)
    {
        child = null;
        return false;
    }

    protected override void EnqueueDirect(AbstractWorkloadBase workload)
    {
        if (TryBindWorkload(workload))
        {
            AbstractWorkloadBase? old = Interlocked.Exchange(ref _singleWorkload, workload);
            // we need to abort the old workload and invoke any continuations
            old?.InternalAbort();
        }
        else if (workload.IsCompleted)
        {
            DebugLog.WriteInfo(SR.ThreadingWorkloads_QdiscEnqueueFailed_AlreadyCompleted);
        }
        else
        {
            throw new WorkloadSchedulingException(SR.ThreadingWorkloads_QdiscEnqueueFailed_NotBound);
        }
    }

    protected override bool TryDequeueInternal(WorkerContext worker, bool backTrack, [NotNullWhen(true)] out AbstractWorkloadBase? workload)
    {
        workload = Interlocked.Exchange(ref _singleWorkload, null);
        return workload is not null;
    }

    protected override bool TryEnqueue(object? state, AbstractWorkloadBase workload)
    {
        if (Filters.Match(state))
        {
            EnqueueDirect(workload);
            return true;
        }
        return false;
    }

    protected override bool TryEnqueueByHandle(THandle handle, AbstractWorkloadBase workload) => false;

    protected override bool TryFindRoute(THandle handle, ref RoutingPath<THandle> path) => false;

    protected override bool TryPeekUnsafe(WorkerContext worker, [NotNullWhen(true)] out AbstractWorkloadBase? workload)
    {
        workload = _singleWorkload;
        return workload is not null;
    }

    protected override bool TryRemoveInternal(AwaitableWorkload workload) => Interlocked.CompareExchange(ref _singleWorkload, null, workload) is not null;
}
