using Cash.Diagnostic;
using Cash.Threading.Workloads.Queuing;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using CommonFlags = Cash.Threading.Workloads.WorkloadStatus.CommonFlags;

namespace Cash.Threading.Workloads.Scheduling.Dispatchers;

internal sealed class BoundedWorkloadDispatcher : IWorkloadDispatcher
{
    private readonly IQdisc _rootQdisc;
    private readonly ManualResetEventSlim _fullyDisposed = new(false);
    private volatile bool _disposed;

    // do not mark as readonly, this struct is mutable
    // tracks the current degree of parallelism and the worker ids that are currently in use
    private WorkerState _state;

    public BoundedWorkloadDispatcher(IQdisc rootQdisc, int maximumConcurrencyLevel)
    {
        if (maximumConcurrencyLevel < 1)
        {
            DebugLog.WriteWarning($"The maximum degree of parallelism must be greater than zero. The specified value was {maximumConcurrencyLevel}.");
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrencyLevel), maximumConcurrencyLevel, "The maximum degree of parallelism must be greater than zero.");
        }
        _rootQdisc = rootQdisc;
        MaximumConcurrencyLevel = maximumConcurrencyLevel;
        _state = new WorkerState(maximumConcurrencyLevel);

        DebugLog.WriteInfo($"Created workload scheduler with root qdisc {_rootQdisc} and maximum concurrency level {MaximumConcurrencyLevel}.");
    }

    public int MaximumConcurrencyLevel { get; }

    void IWorkloadDispatcher.OnWorkScheduled()
    {
        DebugLog.WriteDiagnostic("Workload scheduler was poked.");
        // this atomic clamped increment is committing, if we have room for another worker, we must start one
        // we are not allowed to abort the operation, because that could lead to starvation
        WorkerStateSnapshot state = _state.TryClaimWorkerSlot();
        if (!_disposed && state.CallerClaimedWorkerSlot)
        {
            // we have room for another worker, so we'll start one
            DebugLog.WriteDiagnostic($"Successfully queued new worker {state.ClaimedWorker}. Worker count incremented: {state.WorkerCount - 1} -> {state.WorkerCount}.");
            // do not flow the execution context to the worker
            DispatchWorkerNonCapturing(state.ClaimedWorker);
            // we successfully started a worker, so we can exit
            return;
        }
        // we're at the max degree of parallelism, so we can exit
        DebugLog.WriteDiagnostic($"Reached maximum concurrency level: {state.WorkerCount} >= {MaximumConcurrencyLevel}.");
    }

    private void DispatchWorkerNonCapturing(WorkerContext worker)
    {
        using (ExecutionContext.SuppressFlow())
        {
            ThreadPool.QueueUserWorkItem(WorkerLoop, worker, preferLocal: false);
        }
    }

    internal async void WorkerLoop(WorkerContext? worker)
    {
        Debug.Assert(worker is not null);
        DebugLog.WriteInfo($"Started worker {worker.WorkerId}");
        bool previousExecutionFailed = false;
        // check for disposal before and after each dequeue (volatile read)
        AbstractWorkloadBase? workload = null;
        int previousWorkerId = worker.WorkerId;
        while (!_disposed && TryDequeueOrExitSafely(ref worker, previousExecutionFailed, out workload) && !_disposed)
        {
            previousWorkerId = worker.WorkerId;
            bool successfulExecution = workload switch
            {
                AsyncWorkload asyncWorkload => await asyncWorkload.TryRunAsynchronously().ConfigureAwait(continueOnCapturedContext: false),
                _ => workload.TryRunSynchronously(),
            };
            previousExecutionFailed = !successfulExecution;
            Debug.Assert(workload.Status.IsOneOf(CommonFlags.Completed));
            workload.InternalRunContinuations(worker);
        }
        OnWorkerTerminated(ref worker, previousWorkerId, workload);
    }

    private void OnWorkerTerminated([MaybeNull] ref WorkerContext? worker, int previousWorkerId, AbstractWorkloadBase? workload)
    {
        if (worker is not null)
        {
            previousWorkerId = worker.WorkerId;
            _state.ResignWorker(ref worker);
        }
        Debug.Assert(worker is null);
        if (_disposed)
        {
            if (workload?.IsCompleted is false)
            {
                // if we dequeued a workload, but the scheduler was disposed, we need to abort the workload
                workload.InternalAbort(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(nameof(BoundedWorkloadDispatcher))));
                DebugLog.WriteDiagnostic($"Aborted workload {workload} due to scheduler disposal.");
            }
            if (_state.VolatileWorkerCount <= 0)
            {
                _fullyDisposed.Set();
            }
            DebugLog.WriteInfo($"Scheduler was disposed. Terminated worker {previousWorkerId}.");
        }
        else
        {
            DebugLog.WriteInfo($"Terminated worker {previousWorkerId}.");
        }
    }

    /// <summary>
    /// Attempts to dequeue a workload from the root qdisc, and if that fails, attempts to clean up the worker thread and establish a well-defined state with one less worker.
    /// </summary>
    /// <param name="worker">The id of the worker that is attempting to dequeue a workload.</param>
    /// <param name="previousExecutionFailed"><see langword="true"/> if the previous workload execution failed; <see langword="false"/> if the previous workload execution succeeded. Instructs the underlying qdisc to back track to the previous state if possible.</param>
    /// <param name="workload">The dequeued <see cref="Workload"/>, or <see langword="null"/> if the worker should exit.</param>
    /// <returns><see langword="true"/> if a workload was dequeued, <see langword="false"/> if the worker should exit.</returns>
    /// <remarks>
    /// If this method returns <see langword="false"/>, the worker must exit in order to respect the max degree of parallelism.
    /// </remarks>
    private bool TryDequeueOrExitSafely([NotNullWhen(true)][DisallowNull] ref WorkerContext? worker, bool previousExecutionFailed, [NotNullWhen(true)] out AbstractWorkloadBase? workload)
    {
        DebugLog.WriteDiagnostic($"Worker {worker.WorkerId} is attempting to dequeue a workload.");
        // race against scheduling threads (unless we are being disposed)
        while (true)
        {
            if (_rootQdisc.TryDequeueInternal(worker, previousExecutionFailed, out workload))
            {
                DebugLog.WriteDiagnostic($"Worker {worker.WorkerId} successfully dequeued workload {workload}.");
                // we successfully dequeued a task, return with success
                return true;
            }
            // we are about to exit, so we must release the worker slot
            // by contract, we can only pass our own worker id to this method
            // once the worker slot is released, we must no longer assume that the worker id is valid
            // before we release the worker slot, we must allow the qdiscs to clean up worker-local states
            _rootQdisc.OnWorkerTerminated(worker);
            // now we can release the worker slot
            WorkerContext? previousWorker = worker;
            _state.ResignWorker(ref worker);
            // re-sample the queue
            DebugLog.WriteDiagnostic($"Worker holding ID {previousWorker.WorkerId} previously found no tasks, resampling root qdisc to ensure true emptiness.");
            // it is the responsibility of the qdisc implementation to ensure that this operation is thread-safe
            if (_disposed || _rootQdisc.IsEmpty)
            {
                if (_disposed)
                {
                    // we are being disposed, exit
                    DebugLog.WriteDebug($"Worker holding ID {previousWorker.WorkerId} previously found no tasks, exiting due to disposal.");
                }
                else
                {
                    // no more tasks, exit
                    DebugLog.WriteDebug($"Worker holding ID {previousWorker.WorkerId} previously found no tasks, exiting.");
                    if (_state.VolatileWorkerCount == 0)
                    {
                        DebugLog.WriteDiagnostic($"Last worker {previousWorker.WorkerId} resigned.");
                    }
                }
                return false;
            }
            DebugLog.WriteDiagnostic($"Worker holding ID {previousWorker.WorkerId} previously was interrupted while exiting due to new scheduling activity. Attempting to restore worker count.");
            // failure case: someone else scheduled a task, possibly before we decremented the worker count,
            // so we could lose a worker here attempt abort the exit by re-claiming the worker slot
            // we could be racing against a scheduling thread, or any other worker that is also trying to exit
            // we attempt to atomically restore the worker count to the previous value and claim a new worker slot
            // note that restoring the worker count may result in a different worker id being assigned to us
            WorkerStateSnapshot state = _state.TryClaimWorkerSlot();
            if (_disposed || !state.CallerClaimedWorkerSlot)
            {
                if (_disposed)
                {
                    // we are being disposed, exit
                    DebugLog.WriteDebug($"Worker holding ID {previousWorker.WorkerId} previously found no tasks, exiting due to disposal.");
                }
                else
                {
                    // somehow someone else comitted to creating a new worker, that's unfortunate due to the scheduling overhead
                    // but they are committed now, so we must give up and exit
                    DebugLog.WriteDebug($"Worker holding ID {previousWorker.WorkerId} previously is exiting after encountering maximum concurrency level {MaximumConcurrencyLevel} during restore attempt.");
                }
                return false;
            }
            worker = state.ClaimedWorker;
            DebugLog.WriteDebug($"Worker holding ID {previousWorker.WorkerId} previously is resuming with new ID {worker.WorkerId} after successfully restoring worker count.");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DebugLog.WriteInfo($"Disposing workload scheduler with root qdisc {_rootQdisc}...");
            _disposed = true;
            // it would be increadibly bad if we started waiting before we set the disposed flag
            Thread.MemoryBarrier();
            DebugLog.WriteInfo($"Waiting for all workers to terminate...");
            if (_state.VolatileWorkerCount > 0)
            {
                _fullyDisposed.Wait();
            }
            DebugLog.WriteInfo($"All workers terminated.");
        }
    }

    private struct WorkerState(int _maximumConcurrencyLevel)
    {
        private readonly ConcurrentBag<WorkerContext> _workerIds = [.. Enumerable.Range(0, _maximumConcurrencyLevel).Select(i => new WorkerContext(i))];
        private int _currentDegreeOfParallelism = 0;

        public int VolatileWorkerCount => Volatile.Read(ref _currentDegreeOfParallelism);

        public WorkerStateSnapshot TryClaimWorkerSlot()
        {
            WorkerContext? workerContext = null;
            // post increment, so we start at 0
            int original = Atomic.IncrementClampMaxFast(ref _currentDegreeOfParallelism, _maximumConcurrencyLevel);
            if (original < _maximumConcurrencyLevel)
            {
                DebugLog.WriteDiagnostic($"Attempting to claim worker slot {original}.");
                SpinWait spinner = default;
                for (int i = 0; !_workerIds.TryTake(out workerContext); i++)
                {
                    DebugLog.WriteWarning($"Worker slot is not yet available, spinning ({i} times so far).");
                    spinner.SpinOnce();
                }
                original++;
            }
            return new WorkerStateSnapshot(workerContext, original);
        }

        public void ResignWorker([MaybeNull][DisallowNull] ref WorkerContext? worker)
        {
            DebugLog.WriteDiagnostic($"Resigning worker {worker.WorkerId}.");
            // pre-decrement, so we start at the maximum value - 1
            int workerCount = Interlocked.Decrement(ref _currentDegreeOfParallelism);
            worker.Clear();
            _workerIds.Add(worker);
            Debug.Assert(workerCount >= 0);
            worker = null;
        }
    }

    private readonly ref struct WorkerStateSnapshot(WorkerContext? callerWorkerContext, int workerCount)
    {
        public readonly WorkerContext? ClaimedWorker = callerWorkerContext;
        public readonly int WorkerCount = workerCount;

        [MemberNotNullWhen(true, nameof(ClaimedWorker))]
        public bool CallerClaimedWorkerSlot => ClaimedWorker is not null;
    }
}
