using Cash.Diagnostic;
using Cash.Threading.Workloads.Exceptions;
using Cash.Threading.Workloads.Factories;
using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.Scheduling.Dispatchers;
using System.Runtime.ExceptionServices;

namespace Cash.Threading.Workloads.Scheduling;

internal sealed class WorkloadScheduler<THandle>(IClassifyingQdisc<THandle> root, IWorkloadDispatcher dispatcher) : IWorkloadScheduler<THandle> where THandle : unmanaged
{
    private readonly IClassifyingQdisc<THandle> _root = root;
    private bool _disposedValue;

    public IClassifyingQdisc<THandle> Root
    {
        get
        {
            _root.AssertNotCompleted();
            return _root;
        }
    }

    public void Schedule(THandle handle, AbstractWorkloadBase workload)
    {
        workload.Dispatcher = dispatcher;
        IClassifyingQdisc<THandle> root = _root;
        if (root.Handle.Equals(handle))
        {
            root.Enqueue(workload);
        }
        else if (!root.TryEnqueueByHandle(handle, workload))
        {
            WorkloadSchedulingException.ThrowNoRouteFound(handle);
        }
        dispatcher.OnWorkScheduled();
    }

    public void Schedule(AbstractWorkloadBase workload)
    {
        workload.Dispatcher = dispatcher;
        _root.Enqueue(workload);
        dispatcher.OnWorkScheduled();
    }

    public void Classify(object? state, AbstractWorkloadBase workload)
    {
        workload.Dispatcher = dispatcher;
        if (!_root.TryEnqueue(state, workload))
        {
            WorkloadSchedulingException.ThrowClassificationFailed(state);
        }
        dispatcher.OnWorkScheduled();
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing && !_root.IsCompleted)
            {
                DebugLog.WriteInfo("Workload scheduler: disposal requested.");
                // CAS in the completion sentinel to prevent further scheduling.
                _root.Complete();
                // dispose the root scheduler and wait for all workers to exit.
                dispatcher.Dispose();
                // clear all workloads from the root scheduler.
                ObjectDisposedException exception = new(nameof(WorkloadFactory<THandle>), "The parent scheduler was disposed.");
                ExceptionDispatchInfo.SetCurrentStackTrace(exception);
                // the dispatcher has already been disposed, so no more workers are running.
                // we can now safely claim any worker ID we want to perform the cleanup.
                WorkerContext cleanupWorker = new(id: 0);
                while (_root.TryDequeueInternal(worker: cleanupWorker, backTrack: false, out AbstractWorkloadBase? workload))
                {
                    if (!workload.IsCompleted)
                    {
                        workload.InternalAbort(exception);
                        DebugLog.WriteWarning($"Disposing workload scheduler but queueing scructures still contain uncompleted workloads. Forcefully aborted workload {workload}.");
                    }
                    if (workload is AwaitableWorkload awaitable)
                    {
                        awaitable.UnbindQdiscUnsafe();
                    }
                }
                // dispose the qdisc data structures.
                DebugLog.WriteDebug("Disposing scheduler data structures NOW.");
                _root.Dispose();
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
