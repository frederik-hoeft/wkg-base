using Cash.Collections.Concurrent;
using Cash.Common.Extensions;
using Cash.Common.ThrowHelpers;
using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Queuing.Classless.Fifo;
using Cash.Threading.Workloads.Queuing.Routing;
using Cash.Threading.Workloads.Scheduling;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Cash.Threading.Workloads.Queuing.Classless.PriorityFifoFast;

internal class PriorityFifoFastQdisc<THandle> : ClassifyingQdisc<THandle>
    where THandle : unmanaged
{
    private readonly ConcurrentBitmap _dataMap;
    private readonly IClassifyingQdisc<THandle>[] _bands;
    private readonly int _defaultBand;
    private readonly bool _bandHandlesConfigured;
    private readonly Func<object?, int> _bandSelector;
    private volatile int _fuzzyCount;

    [SuppressMessage(RELIABILITY, CA2000_DISPOSE_OBJECT, Justification = JUSTIFY_CA2000_OWNERSHIP_TRANSFER_TO_PROXY)]
    public PriorityFifoFastQdisc(THandle handle, THandle[] bandHandles, int bands, int defaultBand, Func<object?, int> bandSelector, IFilterManager filters) 
        : base(handle, filters)
    {
        Debug.Assert(bands > 1);
        Debug.Assert(defaultBand >= 0 && defaultBand < bands);
        Debug.Assert(bandSelector is not null);
        Debug.Assert(bandHandles.Length == 0 || bandHandles.Length == bands);
        _bandHandlesConfigured = bandHandles.Length == bands;
        _dataMap = new ConcurrentBitmap(bands);
        _bands = new FifoQdisc<THandle>[bands];
        for (int i = 0; i < bands; i++)
        {
            THandle bandHandle = _bandHandlesConfigured ? bandHandles[i] : default;
            FifoQdisc<THandle> band = new(bandHandle, FilterManager.MatchNothing());
            band.To<IQdisc<THandle>>().InternalInitialize(Scheduler);
            _bands[i] = band;
        }
        _defaultBand = defaultBand;
        _bandSelector = bandSelector;
    }

    public override bool IsEmpty => _dataMap.IsEmptyUnsafe;

    public override int BestEffortCount => _fuzzyCount;

    protected override bool CanClassify(object? state) => Filters.Match(state);

    public override bool TryFindChild(THandle handle, [NotNullWhen(true)] out IClassifyingQdisc<THandle>? child)
    {
        if (_bandHandlesConfigured)
        {
            for (int i = 0; i < _bands.Length; i++)
            {
                if (_bands[i].Handle.Equals(handle))
                {
                    child = _bands[i];
                    return true;
                }
            }
        }
        child = null;
        return false;
    }

    protected override void EnqueueDirect(AbstractWorkloadBase workload) => EnqueueDirectCore(workload, _defaultBand);

    private void EnqueueDirectCore(AbstractWorkloadBase workload, int band)
    {
        // fuzzy count must be greater than or equal to the actual count
        // therefore pre-increment
        Interlocked.Increment(ref _fuzzyCount);
        // update the data map. This must be done before notifying the scheduler
        // it doesn't matter if it happens before or after the enqueue
        _bands[band].Enqueue(workload);
        // update the empty map to reflect that this band is no longer empty
        PostWorkScheduled(band);
    }

    protected override bool TryDequeueInternal(WorkerContext worker, bool backTrack, [NotNullWhen(true)] out AbstractWorkloadBase? workload)
    {
        // we loop until we find something to dequeue
        // a simple for loop isn't enough because new workloads may be inserted at a lower band after we've checked it
        IClassifyingQdisc<THandle>[] bands = _bands;
        while (!IsEmpty)
        {
            for (int i = 0; i < bands.Length; i++)
            {
                GuardedBitInfo bitInfo = _dataMap.GetBitInfoUnsafe(i);
                if (!bitInfo.IsSet)
                {
                    // just skip this band if it's empty
                    // a lookup in the data map is faster than an attempted dequeue
                    continue;
                }
                byte token = bitInfo.Token;
                int j = 0;
                do
                {
                    if (j != 0)
                    {
                        token = _dataMap.GetTokenUnsafe(i);
                    }
                    if (bands[i].TryDequeueInternal(worker, backTrack, out workload))
                    {
                        Interlocked.Decrement(ref _fuzzyCount);
                        return true;
                    }
                    // the queue was empty, but the last state we knew about was that there should be something in the queue
                    // so we need to update the data map to reflect the new state
                    // something may have been enqueued in the meantime, so we use a token to ensure that we don't overwrite
                    // a newer state with an older one
                    j++;
                } while (!_dataMap.TryUpdateBitUnsafe(i, token, isSet: false));
            }
        }
        // the queue was empty, and we didn't find anything to dequeue
        workload = null;
        return false;
    }

    protected override bool TryEnqueue(object? state, AbstractWorkloadBase workload)
    {
        if (Filters.Match(state))
        {
            // TODO: use filters for children
            int band = _bandSelector.Invoke(state);
            if (band == -1)
            {
                band = _defaultBand;
            }
            Throw.ArgumentOutOfRangeException.IfNotInRange(band, 0, _bands.Length - 1, nameof(band));
            EnqueueDirectCore(workload, band);
            return true;
        }
        return false;
    }

    protected override bool TryEnqueueByHandle(THandle handle, AbstractWorkloadBase workload)
    {
        if (_bandHandlesConfigured)
        {
            for (int i = 0; i < _bands.Length; i++)
            {
                if (_bands[i].Handle.Equals(handle))
                {
                    EnqueueDirectCore(workload, i);
                    return true;
                }
            }
        }
        return false;
    }

    protected override bool TryFindRoute(THandle handle, ref RoutingPath<THandle> path)
    {
        if (_bandHandlesConfigured)
        {
            for (int i = 0; i < _bands.Length; i++)
            {
                if (_bands[i].Handle.Equals(handle))
                {
                    path.Add(new RoutingPathNode<THandle>(this, handle, i));
                    path.Complete(_bands[i]);
                    return true;
                }
            }
        }
        return false;
    }

    protected override bool TryPeekUnsafe(WorkerContext worker, [NotNullWhen(true)] out AbstractWorkloadBase? workload)
    {
        // we loop until we find something to peek or the queue is empty
        // a simple for loop isn't enough because new workloads may be inserted at a lower band after we've checked it
        while (!IsEmpty)
        {
            for (int i = 0; i < _bands.Length; i++)
            {
                if (!_dataMap.IsBitSetUnsafe(i))
                {
                    // just skip this band if it's empty
                    // a lookup in the data map is faster than an attempted peek
                    continue;
                }
                byte token;
                do
                {
                    token = _dataMap.GetTokenUnsafe(i);
                    if (_bands[i].TryPeekUnsafe(worker, out workload))
                    {
                        return true;
                    }
                    // the queue was empty, but the last state we knew about was that there should be something in the queue
                    // so we need to update the data map to reflect the new state
                    // something may have been enqueued in the meantime, so we use a token to ensure that we don't overwrite
                    // a newer state with an older one
                } while (!_dataMap.TryUpdateBitUnsafe(i, token, false));
            }
        }
        // the queue was empty, and we didn't find anything to peek
        workload = null;
        return false;
    }

    protected override bool TryRemoveInternal(AwaitableWorkload workload) => false;

    protected override void OnEnqueueFromRoutingPath(ref readonly RoutingPathNode<THandle> routingPathNode, AbstractWorkloadBase workload)
    {
        Interlocked.Increment(ref _fuzzyCount);
        PostWorkScheduled(routingPathNode.Offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PostWorkScheduled(int index) => _dataMap.UpdateBitUnsafe(index, isSet: true);

    protected override void DisposeManaged()
    {
        for (int i = 0; i < _bands.Length; i++)
        {
            _bands[i].Dispose();
        }
        _dataMap.Dispose();
    }

    protected override void ChildrenToTreeString(StringBuilder builder, int indent)
    {
        for (int i = 0; i < _bands.Length; i++)
        {
            builder.AppendIndent(indent).Append($"Band {i}: ");
            ChildToTreeString(_bands[i], builder, indent);
        }
    }
}
