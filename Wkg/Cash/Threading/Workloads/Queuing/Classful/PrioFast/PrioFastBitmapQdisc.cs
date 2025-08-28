using Cash.Collections.Concurrent;
using Cash.Common.Extensions;
using Cash.Diagnostic;
using Cash.Threading.Workloads.Configuration.Classless;
using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.Queuing.Routing;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Cash.Threading.Workloads.Exceptions;
using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Scheduling;
using System.Runtime.CompilerServices;

namespace Cash.Threading.Workloads.Queuing.Classful.PrioFast;

/// <summary>
/// A classful qdisc that implements a simple priority scheduling algorithm to dequeue workloads from its children. Works best for high contention and deep hierarchies.
/// </summary>
/// <typeparam name="THandle">The type of the handle.</typeparam>
internal sealed class PrioFastBitmapQdisc<THandle> : ClassfulQdisc<THandle>, IClassfulQdisc<THandle>, IWorkerDataOwner
    where THandle : unmanaged
{
    private readonly int _instanceHash;
    private readonly ConcurrentBitmap _dataMap;
    private readonly IClassifyingQdisc<THandle> _localQueue;
    private readonly IClassifyingQdisc<THandle>[] _children;

    private int _maxRoutingPathDepthEncountered = 4;

    public PrioFastBitmapQdisc(THandle handle, IFilterManager filters, IClasslessQdiscBuilder localQueueBuilder, IClassifyingQdisc<THandle>[] children) 
        : base(handle, filters)
    {
        _localQueue = localQueueBuilder.BuildUnsafe(handle: default(THandle), filters: null);
        _children = [_localQueue, .. children];
        foreach (IClassifyingQdisc<THandle> child in children)
        {
            BindChildQdisc(child);
        }
        _dataMap = new ConcurrentBitmap(_children.Length);
        _instanceHash = RuntimeHelpers.GetHashCode(this);
    }

    protected override void OnInternalInitialize(IWorkloadScheduler<THandle> scheduler) =>
        BindChildQdisc(_localQueue);

    int IWorkerDataOwner.InstanceHash => _instanceHash;

    public override bool IsEmpty => IsEmptyInternal;

    private bool IsEmptyInternal => _dataMap.IsEmptyUnsafe;

    public override int BestEffortCount
    {
        get
        {
            // we can take a shortcut here, if we established that all children are empty
            if (IsEmptyInternal)
            {
                return 0;
            }
            // get a local snapshot of the children array, other threads may still add new children which we don't care about here
            IClassifyingQdisc<THandle>[] children = _children;
            int count = 0;
            for (int i = 0; i < children.Length; i++)
            {
                count += children[i].BestEffortCount;
            }
            return count;
        }
    }

    // not supported.
    // would only need to consider the local queue, since this
    // method is only called on the direct parent of a workload.
    protected override bool TryRemoveInternal(AwaitableWorkload workload) => false;

    protected override bool TryDequeueInternal(WorkerContext worker, bool backTrack, [NotNullWhen(true)] out AbstractWorkloadBase? workload)
    {
        // if we have to backtrack, we can do so by dequeuing from the last child qdisc
        // that was dequeued from. If the last child qdisc is empty, we can't backtrack and continue
        // with the next child qdisc.
        if (backTrack && !IsEmptyInternal && worker.TryGetData(this, out IClassifyingQdisc<THandle>? lastQdisc) && lastQdisc.TryDequeueInternal(worker, backTrack, out workload))
        {
            DebugLog.WriteDiagnostic($"{this} Backtracking to last child qdisc {lastQdisc.GetType().Name} ({lastQdisc}).");
            return true;
        }
        // backtracking failed, or was not requested. We need to iterate over all child qdiscs.
        IClassifyingQdisc<THandle>[] children = _children;
        while (!IsEmptyInternal)
        {
            for (int index = 0; index < children.Length; index++)
            {
                // if the qdisc is empty, we can skip it
                GuardedBitInfo bitInfo = _dataMap.GetBitInfoUnsafe(index);
                if (!bitInfo.IsSet)
                {
                    // skip empty children
                    continue;
                }
                // usually we should succeed first try, so we use the existing token from our read earlier
                byte token = bitInfo.Token;
                IClassifyingQdisc<THandle> child = children[index];
                int expiredTokens = 0;
                do
                {
                    if (expiredTokens != 0)
                    {
                        // on subsequent tries, we need to refresh the token
                        DebugLog.WriteDebug($"{this}: emptiness bit map changed while attempting to declare child {child} as empty. Attempts so far {expiredTokens}. Resampling...");
                        token = _dataMap.GetTokenUnsafe(index);
                    }
                    // get our assigned child qdisc
                    if (child.TryDequeueInternal(worker, backTrack, out workload))
                    {
                        DebugLog.WriteDiagnostic($"{this} Dequeued workload from child qdisc {child}.");
                        // we found a workload, update the last child qdisc and reset the empty counter
                        worker.SetData(this, child);
                        return true;
                    }
                    // the child seems to be empty, but we can't be sure.
                    // attempt to update the emptiness bit map to reflect the new state
                    DebugLog.WriteDiagnostic($"{this}: child {child} seems to be empty. Updating emptiness bit map.");
                    expiredTokens++;
                } while (!_dataMap.TryUpdateBitUnsafe(index, token, isSet: false));
                DebugLog.WriteDebug($"{this}: Emptiness state of child qdisc {child} changed to empty.");
            }
        }
        // all children are empty
        DebugLog.WriteDebug($"{this}: All children are empty.");
        workload = null;
        return false;
    }

    protected override bool TryPeekUnsafe(WorkerContext worker, [NotNullWhen(true)] out AbstractWorkloadBase? workload)
    {
        while (!IsEmptyInternal)
        {
            IClassifyingQdisc<THandle>[] children = _children;

            // this one is easier than TryDequeueInternal, since we operate entirely read-only and we have out own local state
            // in theory, we could participate in the empty counter tracking, but that's not necessary
            for (int i = 0; i < children.Length; i++)
            {
                // we can use the unsafe version here, since we are holding a read lock (the bitmap structure won't change)
                if (_dataMap.IsBitSetUnsafe(i) && children[i].TryPeekUnsafe(worker, out workload))
                {
                    return true;
                }
            }
        }
        workload = null;
        return false;
    }

    protected override bool CanClassify(object? state)
    {
        if (!Filters.Match(state))
        {
            // fast path, we can't classify the workload
            return false;
        }

        IClassifyingQdisc<THandle>[] children = _children;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].CanClassify(state))
            {
                return true;
            }
        }
        return false;
    }

    protected override bool TryEnqueueByHandle(THandle handle, AbstractWorkloadBase workload)
    {
        DebugLog.WriteDiagnostic($"{this} Trying to enqueue workload {workload} to child qdisc with handle {handle}.");

        // only lock the children array while we need to access it
        IClassifyingQdisc<THandle>[] children = _children;
        RoutingPath<THandle> path = new(Volatile.Read(in _maxRoutingPathDepthEncountered));
        // start at 1, since the local queue is always the first child and the local queue has "our" handle
        // so checking us/our local queue is redundant
        for (int i = 1; i < children.Length; i++)
        {
            IClassifyingQdisc<THandle> child = children[i];
            if (child.Handle.Equals(handle))
            {
                child.Enqueue(workload);
                DebugLog.WriteDiagnostic($"Enqueued workload {workload} to child qdisc {child}.");
                // update the emptiness tracking
                PostEnqueueToChild(i);
                goto SUCCESS;
            }
            // we must first check if the child can enqueue the workload.
            // then we must prepare everything for the enqueueing operation.
            // only then can we actually enqueue the workload.
            // in order to achieve this we construct a routing path and then directly enqueue the workload to the child.
            // using a routing path allows us to avoid having to do the same work twice.
            if (child.TryFindRoute(handle, ref path) && path.Leaf is not null)
            {
                // enqueue the workload to the leaf
                path.Leaf.Enqueue(workload);
                // we need to call OnEnqueueFromRoutingPath on all nodes in the path
                // failure to do so may result in incorrect emptiness tracking of the child qdiscs
                foreach (ref readonly RoutingPathNode<THandle> node in path)
                {
                    node.Qdisc.OnEnqueueFromRoutingPath(in node, workload);
                }
                Atomic.WriteMaxFast(ref _maxRoutingPathDepthEncountered, path.Count);
                DebugLog.WriteDiagnostic($"{this}: enqueued workload {workload} to child {child}.");
                // update the emptiness tracking
                PostEnqueueToChild(i);
                goto SUCCESS;
            }
        }
        DebugLog.WriteDiagnostic($"Could not enqueue workload {workload} to any child qdisc. No child qdisc with handle {handle} found.");
        path.Dispose();
        return false;
    SUCCESS:
        path.Dispose();
        return true;
    }

    protected override bool TryEnqueue(object? state, AbstractWorkloadBase workload)
    {
        DebugLog.WriteDiagnostic($"Trying to enqueue workload {workload} to round robin qdisc {this}.");
        if (!Filters.Match(state))
        {
            DebugLog.WriteDiagnostic($"{this}: cannot classify workload {workload}, as it does not match the filter.");
            return false;
        }

        // only lock the children array while we need to access it
        IClassifyingQdisc<THandle>[] children = _children;
        for (int i = 0; i < children.Length; i++)
        {
            IClassifyingQdisc<THandle> child = children[i];
            if (child.CanClassify(state))
            {
                if (!child.TryEnqueue(state, workload))
                {
                    // this should never happen, as we already checked if the child can classify the workload
                    ThrowClassificationFailure(child, workload);
                }
                DebugLog.WriteDiagnostic($"{this}: enqueued workload {workload} to child {child}.");
                // update the emptiness tracking
                PostEnqueueToChild(i);
                return true;
            }
        }
        EnqueueDirect(workload);
        return true;
    }

    [DoesNotReturn]
    private static void ThrowClassificationFailure(IClassifyingQdisc<THandle> child, AbstractWorkloadBase workload)
    {
        WorkloadSchedulingException exception = WorkloadSchedulingException.CreateVirtual($"Scheduler inconsistency: child qdisc {child} reported to be able to classify workload {workload}, but failed to do so.");
        Debug.Fail(exception.Message);
        DebugLog.WriteException(exception);
        // we are on the enqueueing thread, so we can just throw here
        throw exception;
    }

    protected override bool TryFindRoute(THandle handle, ref RoutingPath<THandle> path)
    {
        IClassifyingQdisc<THandle>[] children = _children;
        for (int i = 1; i < children.Length; i++)
        {
            IClassifyingQdisc<THandle> child = children[i];
            if (child.Handle.Equals(handle))
            {
                path.Add(new RoutingPathNode<THandle>(this, handle, i));
                path.Complete(child);
                return true;
            }
            if (child.TryFindRoute(handle, ref path))
            {
                path.Add(new RoutingPathNode<THandle>(this, handle, i));
                return true;
            }
        }
        return false;
    }

    protected override void OnEnqueueFromRoutingPath(ref readonly RoutingPathNode<THandle> routingPathNode, AbstractWorkloadBase workload)
    {
        int index = routingPathNode.Offset;
        DebugLog.WriteDiagnostic($"{this}: enqueued workload {workload} to child {_children[index]} via routing path.");
        PostEnqueueToChild(index);
    }

    protected override void EnqueueDirect(AbstractWorkloadBase workload)
    {
        const int LOCAL_QUEUE_INDEX = 0;
        _localQueue.Enqueue(workload);
        DebugLog.WriteDiagnostic($"{this}: enqueued workload {workload} to local queue ({_localQueue}).");
        PostEnqueueToChild(LOCAL_QUEUE_INDEX);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PostEnqueueToChild(int index)
    {
        // worker threads attempting to mark this child as empty will just fail to do so as their token will be invalidated by us
        // so no ABA problem here (not empty -> worker finds no workload -> we set it to not empty -> worker tries to set it to empty -> worker fails)
        _dataMap.UpdateBitUnsafe(index, isSet: true);
        DebugLog.WriteDebug($"{this}: cleared empty flag for {(index == 0 ? this : _children[index])}.");
    }

    /// <inheritdoc/>
    public override bool TryAddChild(IClassifyingQdisc<THandle> child) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override bool RemoveChild(IClassifyingQdisc<THandle> child) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override bool TryRemoveChild(IClassifyingQdisc<THandle> child) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override bool TryFindChild(THandle handle, [NotNullWhen(true)] out IClassifyingQdisc<THandle>? child)
    {
        IClassifyingQdisc<THandle>[] children = _children;
        for (int i = 1; i < children.Length; i++)
        {
            child = children[i];
            if (child.Handle.Equals(handle))
            {
                return true;
            }
            if (child is IClassfulQdisc<THandle> classfulChild && classfulChild.TryFindChild(handle, out child))
            {
                return true;
            }
        }
        child = null;
        return false;
    }

    protected override void OnWorkerTerminated(WorkerContext worker)
    {
        // forward to children, no lock needed. if children are removed then they don't need to be notified
        // and if new children are added, they shouldn't know about the worker anyway
        IClassifyingQdisc<THandle>[] children = _children;
        for (int i = 0; i < children.Length; i++)
        {
            children[i].OnWorkerTerminated(worker);
        }

        base.OnWorkerTerminated(worker);
    }

    protected override void DisposeManaged()
    {
        IClassifyingQdisc<THandle>[] children = _children;
        foreach (IClassifyingQdisc<THandle> child in children)
        {
            child.Complete();
            child.Dispose();
        }

        base.DisposeManaged();
    }

    protected override void ChildrenToTreeString(StringBuilder builder, int indent)
    {
        builder.AppendIndent(indent).Append($"Local 0: ");
        ChildToTreeString(_localQueue, builder, indent);
        for (int i = 1; i < _children.Length; i++)
        {
            builder.AppendIndent(indent).Append($"Child {i}: ");
            ChildToTreeString(_children[i], builder, indent);
        }
    }
}