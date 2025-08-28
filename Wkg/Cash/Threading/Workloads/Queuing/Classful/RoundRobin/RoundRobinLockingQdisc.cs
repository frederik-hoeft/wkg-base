using Cash.Common.Extensions;
using Cash.Diagnostic;
using Cash.Threading.Workloads.Configuration.Classless;
using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.Queuing.Routing;
using Cash.Threading.Workloads.Scheduling;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Cash.Threading.Workloads.Queuing.Classful.RoundRobin;

/// <summary>
/// A classful qdisc that implements the Round Robin scheduling algorithm to dequeue workloads from its children.
/// </summary>
/// <typeparam name="THandle">The type of the handle.</typeparam>
// TODO: we should add branch pruning here as a zero-cost optimization
internal sealed class RoundRobinLockingQdisc<THandle> : ClassfulQdisc<THandle>, IClassfulQdisc<THandle>, IWorkerDataOwner
    where THandle : unmanaged
{
    private readonly int _instanceHash;
    private readonly Lock _syncRoot;
    private readonly IClassifyingQdisc<THandle> _localQueue;

    private IClassifyingQdisc<THandle>[] _children;
    private int _rrIndex;

    public RoundRobinLockingQdisc(THandle handle, IFilterManager filters, IClasslessQdiscBuilder localQueueBuilder) : base(handle, filters)
    {
        _localQueue = localQueueBuilder.BuildUnsafe(handle: default(THandle), filters: null);
        _children = [_localQueue];
        _syncRoot = new Lock();
        _instanceHash = RuntimeHelpers.GetHashCode(this);
    }

    protected override void OnInternalInitialize(IWorkloadScheduler<THandle> scheduler) =>
        BindChildQdisc(_localQueue);

    public override bool IsEmpty => BestEffortCount == 0;

    int IWorkerDataOwner.InstanceHash => _instanceHash;

    public override int BestEffortCount
    {
        get
        {
            lock (_syncRoot)
            {
                int count = 0;
                for (int i = 0; i < _children.Length; i++)
                {
                    count += _children[i].BestEffortCount;
                }
                return count;
            }
        }
    }

    // not supported. this is a classful qdisc that never contains workloads directly.
    // workloads are always contained in leaf qdiscs. classful qdiscs always have at least one child qdisc by default.
    protected override bool TryRemoveInternal(AwaitableWorkload workload) => false;

    protected override bool TryDequeueInternal(WorkerContext worker, bool backTrack, [NotNullWhen(true)] out AbstractWorkloadBase? workload)
    {
        lock (_syncRoot)
        {
            if (backTrack && worker.TryGetData(this, out IClassifyingQdisc<THandle>? lastQdisc) && lastQdisc.TryDequeueInternal(worker, backTrack, out workload))
            {
                return true;
            }
            for (int i = 0; i < _children.Length; i++, _rrIndex = (_rrIndex + 1) % _children.Length)
            {
                IClassifyingQdisc<THandle> child = _children[_rrIndex];
                if (child.TryDequeueInternal(worker, backTrack, out workload))
                {
                    // remember the last qdisc we dequeued from for this worker
                    worker.SetData(this, child);
                    _rrIndex = (_rrIndex + 1) % _children.Length;
                    return true;
                }
            }
        }
        workload = null;
        return false;
    }

    protected override bool TryPeekUnsafe(WorkerContext worker, [NotNullWhen(true)] out AbstractWorkloadBase? workload)
    {
        lock (_syncRoot)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                if (_children[i].TryPeekUnsafe(worker, out workload))
                {
                    return true;
                }
            }
        }
        workload = null;
        return false;
    }

    // no need to recurse, as we are the superset of the filters of all children (match-all if no children match)
    protected override bool CanClassify(object? state) => Filters.Match(state);

    protected override bool TryEnqueueByHandle(THandle handle, AbstractWorkloadBase workload)
    {
        lock (_syncRoot)
        {
            for (int i = 1; i < _children.Length; i++)
            {
                IClassifyingQdisc<THandle> child = _children[i];
                if (child.Handle.Equals(handle))
                {
                    child.Enqueue(workload);
                    return true;
                }
                if (child.TryEnqueueByHandle(handle, workload))
                {
                    return true;
                }
            }
        }
        return false;
    }

    protected override bool TryEnqueue(object? state, AbstractWorkloadBase workload)
    {
        if (!Filters.Match(state))
        {
            return false;
        }
        lock (_syncRoot)
        {
            for (int i = 1; i < _children.Length; i++)
            {
                if (_children[i].TryEnqueue(state, workload))
                {
                    return true;
                }
            }
            EnqueueDirect(workload);
            return true;
        }
    }

    protected override bool TryFindRoute(THandle handle, ref RoutingPath<THandle> path)
    {
        lock (_syncRoot)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                IClassifyingQdisc<THandle> child = _children[i];
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
        }
        return false;
    }

    protected override void EnqueueDirect(AbstractWorkloadBase workload)
    {
        // the local queue is a qdisc itself, so we can enqueue directly to it
        // it will call back to us with OnWorkScheduled, so we can reset the empty counter there
        // we will never need a lock here, since the local queue itself is thread-safe and cannot
        // be removed from the children array.
        _localQueue.Enqueue(workload);
        DebugLog.WriteDiagnostic($"{this}: enqueued workload {workload} to local queue ({_localQueue}).");
    }

    public override bool TryAddChild(IClassifyingQdisc<THandle> child)
    {
        lock (_syncRoot)
        {
            if (TryFindChild(child.Handle, out _))
            {
                DebugLog.WriteWarning($"{this}: failed to add child {child} because it is already a child of this qdisc.");
                return false;
            }

            // bind the child to the same scheduler as us
            BindChildQdisc(child);

            IClassifyingQdisc<THandle>[] children = _children;
            _children = [.. children, child];
            return true;
        }
    }

    /// <inheritdoc/>
    public override bool RemoveChild(IClassifyingQdisc<THandle> child) =>
        // block up to 60 seconds to allow the child to become empty
        RemoveChildCore(child, Timeout.Infinite);

    /// <inheritdoc/>
    public override bool TryRemoveChild(IClassifyingQdisc<THandle> child) =>
        RemoveChildCore(child, 0);

    private bool RemoveChildCore(IClassifyingQdisc<THandle> child, int millisecondsTimeout)
    {
        // before locking on the write lock, check if the child is even present. if it's not, we can return early
        if (!ContainsChild(child.Handle))
        {
            return false;
        }
        int startTime = Environment.TickCount;
        // wait for child to be empty
        if (!Wait.Until(() => child.IsEmpty, millisecondsTimeout))
        {
            return false;
        }
        lock (_syncRoot)
        {
            // check if child is still there
            if (!TryFindChild(child.Handle, out _))
            {
                return false;
            }

            // mark the child as completed (all new scheduling attempts will fail)
            child.Complete();
            // someone may have scheduled new workloads in the meantime
            // we preserve them by moving them to the local queue
            // this may break the intended scheduling order, but it is better than losing workloads
            // also that is acceptable, as it should happen very rarely and only if the user is doing something wrong
            // we simply impersonate worker 0 here, as we have exclusive access to the child qdisc anyway
            WorkerContext impersonatedWorker = new(id: 0);
            while (child.TryDequeueInternal(impersonatedWorker, backTrack: false, out AbstractWorkloadBase? workload))
            {
                // enqueue the workloads in the local queue
                _localQueue.Enqueue(workload);
            }

            IClassifyingQdisc<THandle>[] newChildren = new IClassifyingQdisc<THandle>[_children.Length - 1];
            for (int i = 0; i < _children.Length && i < newChildren.Length; i++)
            {
                if (!_children[i].Handle.Equals(child.Handle))
                {
                    newChildren[i] = _children[i];
                }
            }
            _children = newChildren;
            return true;
        }
    }

    /// <inheritdoc/>
    public override bool TryFindChild(THandle handle, [NotNullWhen(true)] out IClassifyingQdisc<THandle>? child)
    {
        lock (_syncRoot)
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
        foreach (IClassifyingQdisc<THandle> child in _children)
        {
            child.Complete();
            child.Dispose();
        }
        _children.AsSpan().Clear();

        base.DisposeManaged();
    }

    protected override void ChildrenToTreeString(StringBuilder builder, int indent)
    {
        lock (_syncRoot)
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
}