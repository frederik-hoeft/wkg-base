using Cash.Common.Extensions;
using Cash.Diagnostic;
using Cash.Threading.Workloads.Queuing.Routing;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Text;
using Cash.Threading.Workloads.Exceptions;
using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Scheduling;

namespace Cash.Threading.Workloads.Queuing.Classless;

/// <summary>
/// Base class for qdiscs.
/// </summary>
/// <typeparam name="THandle">The type of the handle.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ClassifyingQdisc{THandle}"/> class.
/// </remarks>
/// <param name="handle">The handle uniquely identifying this qdisc in this scheduling hierarchy.</param>
/// <param name="predicate">The predicate that determines whether a workload with a given state can be enqueued into this qdisc, or <see langword="null"/> to match nothing by default.</param>
public abstract class ClassifyingQdisc<THandle>(THandle handle, IFilterManager filters) : IClassifyingQdisc<THandle> where THandle : unmanaged
{
    private readonly THandle _handle = handle;
    private IWorkloadScheduler<THandle> _parentScheduler = WorkloadSchedulerSentinel<THandle>.Uninitialized;
    private protected bool _disposedValue;

    public IFilterManager Filters { get; } = filters;

    /// <summary>
    /// The parent scheduler of this qdisc.
    /// </summary>
    private protected IWorkloadScheduler<THandle> Scheduler => Volatile.Read(ref _parentScheduler);

    /// <inheritdoc/>
    public ref readonly THandle Handle => ref _handle;

    /// <inheritdoc/>
    public abstract bool IsEmpty { get; }

    /// <inheritdoc/>
    public abstract int BestEffortCount { get; }

    bool IClassifyingQdisc.IsCompleted => ReferenceEquals(Scheduler, WorkloadSchedulerSentinel<THandle>.Completed);

    void IClassifyingQdisc.AssertNotCompleted()
    {
        if (this.To<IClassifyingQdisc>().IsCompleted)
        {
            ThrowCompleted();
        }
    }

    [DoesNotReturn]
    private void ThrowCompleted()
    {
        ObjectDisposedException exception = new(ToString(), "This qdisc was already marked as completed and is no longer accepting new workloads.");
        ExceptionDispatchInfo.SetCurrentStackTrace(exception);
        DebugLog.WriteException(exception);
        throw exception;
    }

    /// <summary>
    /// Called after this qdisc has been bound to a scheduler.
    /// </summary>
    /// <param name="parentScheduler">The parent scheduler.</param>
    protected virtual void OnInternalInitialize(IWorkloadScheduler<THandle> scheduler) => Pass();

    void IQdisc<THandle>.InternalInitialize(IWorkloadScheduler<THandle> scheduler)
    {
        DebugLog.WriteDiagnostic($"Initializing qdisc {GetType().Name} ({Handle}) with parent scheduler {scheduler.GetType().Name} ({scheduler.As<IQdisc<THandle>>()?.Handle.ToString().Coalesce("<unknown>")}).");
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _parentScheduler, scheduler, WorkloadSchedulerSentinel<THandle>.Uninitialized), WorkloadSchedulerSentinel<THandle>.Uninitialized))
        {
            WorkloadSchedulingException exception = WorkloadSchedulingException.CreateVirtual(SR.ThreadingWorkloads_QdiscInitializationFailed_AlreadyBound);
            DebugLog.WriteException(exception);
            throw exception;
        }
        OnInternalInitialize(scheduler);
    }

    /// <inheritdoc cref="IQdisc.TryDequeueInternal(int, bool, out AbstractWorkloadBase?)"/>"
    protected abstract bool TryDequeueInternal(WorkerContext worker, bool backTrack, [NotNullWhen(true)] out AbstractWorkloadBase? workload);

    /// <inheritdoc cref="IQdisc.TryPeekUnsafe(int, out AbstractWorkloadBase?)"/>"
    protected abstract bool TryPeekUnsafe(WorkerContext worker, [NotNullWhen(true)] out AbstractWorkloadBase? workload);

    /// <inheritdoc cref="IQdisc.TryRemoveInternal(AwaitableWorkload)"/>"
    protected abstract bool TryRemoveInternal(AwaitableWorkload workload);

    /// <inheritdoc cref="IClassifyingQdisc.Enqueue(AbstractWorkloadBase)"/>"
    protected abstract void EnqueueDirect(AbstractWorkloadBase workload);

    /// <summary>
    /// Attempts to bind the specified workload to this qdisc.
    /// </summary>
    /// <remarks>
    /// The workload should be bound first, before being enqueued into the qdisc. Only bind the workload if this qdisc actually stores the workload itself, i.e., if this qdisc does not delegate to another qdisc.
    /// </remarks>
    /// <param name="workload">The workload to bind.</param>
    /// <returns><see langword="true"/> if the workload was successfully bound to the qdisc; <see langword="false"/> if the workload has already completed, is in an unbindable state, or another binding operation was faster.</returns>
    protected bool TryBindWorkload(AbstractWorkloadBase workload) => workload.TryInternalBindQdisc(this);

    /// <inheritdoc cref="IQdisc.OnWorkerTerminated(WorkerContext)"/>
    protected virtual void OnWorkerTerminated(WorkerContext worker) => Pass();

    bool IQdisc.TryDequeueInternal(WorkerContext worker, bool backTrack, [NotNullWhen(true)] out AbstractWorkloadBase? workload) => 
        TryDequeueInternal(worker, backTrack, out workload);

    bool IQdisc.TryRemoveInternal(AwaitableWorkload workload) => TryRemoveInternal(workload);

    void IQdisc.Complete()
    {
        DebugLog.WriteDiagnostic($"Marking qdisc {GetType().Name} ({Handle}) as completed. Future scheduling attempts will be rejected.");
        if (ReferenceEquals(Interlocked.Exchange(ref _parentScheduler, WorkloadSchedulerSentinel<THandle>.Completed), WorkloadSchedulerSentinel<THandle>.Completed))
        {
            WorkloadSchedulingException exception = WorkloadSchedulingException.CreateVirtual(SR.ThreadingWorkloads_QdiscCompletionFailed_AlreadyCompleted);
            DebugLog.WriteException(exception);
            throw exception;
        }
    }

    void IClassifyingQdisc.Enqueue(AbstractWorkloadBase workload) => EnqueueDirect(workload);

    IWorkloadScheduler<THandle> IQdisc<THandle>.Scheduler => Scheduler;

    void IQdisc.OnWorkerTerminated(WorkerContext worker) => OnWorkerTerminated(worker);

    bool IQdisc.TryPeekUnsafe(WorkerContext worker, [NotNullWhen(true)] out AbstractWorkloadBase? workload) => TryPeekUnsafe(worker, out workload);

    #region IClassifyingQdisc / IClassifyingQdisc<THandle> implementation

    /// <inheritdoc cref="IClassifyingQdisc{THandle}.TryEnqueueByHandle(THandle, AbstractWorkloadBase)"/>
    protected abstract bool TryEnqueueByHandle(THandle handle, AbstractWorkloadBase workload);

    /// <inheritdoc cref="IClassifyingQdisc{THandle}.OnEnqueueFromRoutingPath(ref readonly RoutingPathNode{THandle}, AbstractWorkloadBase)"/>
    protected virtual void OnEnqueueFromRoutingPath(ref readonly RoutingPathNode<THandle> routingPathNode, AbstractWorkloadBase workload) => Pass();

    /// <inheritdoc cref="IClassifyingQdisc{THandle}.TryFindRoute(THandle, ref RoutingPath{THandle})"/>
    protected abstract bool TryFindRoute(THandle handle, ref RoutingPath<THandle> path);

    /// <inheritdoc cref="IClassifyingQdisc{THandle}.ContainsChild(THandle)"/>
    public virtual bool ContainsChild(THandle handle) => TryFindChild(handle, out _);

    /// <inheritdoc cref="IClassifyingQdisc.CanClassify(object?)"/>
    protected abstract bool CanClassify(object? state);

    /// <inheritdoc cref="IClassifyingQdisc.TryEnqueue(object?, AbstractWorkloadBase)"/>
    protected abstract bool TryEnqueue(object? state, AbstractWorkloadBase workload);

    /// <inheritdoc cref="IClassifyingQdisc{THandle}.TryFindChild(THandle, out IClassifyingQdisc{THandle}?)"/>
    public abstract bool TryFindChild(THandle handle, [NotNullWhen(true)] out IClassifyingQdisc<THandle>? child);

    bool IClassifyingQdisc<THandle>.TryEnqueueByHandle(THandle handle, AbstractWorkloadBase workload) => TryEnqueueByHandle(handle, workload);
    void IClassifyingQdisc<THandle>.OnEnqueueFromRoutingPath(ref readonly RoutingPathNode<THandle> routingPathNode, AbstractWorkloadBase workload) => OnEnqueueFromRoutingPath(in routingPathNode, workload);
    bool IClassifyingQdisc<THandle>.TryFindRoute(THandle handle, ref RoutingPath<THandle> path) => TryFindRoute(handle, ref path);
    bool IClassifyingQdisc.CanClassify(object? state) => CanClassify(state);
    bool IClassifyingQdisc.TryEnqueue(object? state, AbstractWorkloadBase workload) => TryEnqueue(state, workload);

    #endregion IClassifyingQdisc / IClassifyingQdisc<THandle> implementation

    /// <inheritdoc/>
    public override string ToString() => $"{GetType().Name} ({Handle})";

    /// <summary>
    /// Disposes of any managed resources held by this qdisc.
    /// </summary>
    protected virtual void DisposeManaged() => Filters.Dispose();

    /// <summary>
    /// Disposes of any unmanaged resources held by this qdisc.
    /// </summary>
    protected virtual void DisposeUnmanaged() => Pass();

    /// <summary>
    /// Disposes of any resources held by this qdisc.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> if this method is called from <see cref="Dispose()"/>; <see langword="false"/> if this method is called from the finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                DisposeManaged();
            }

            DisposeUnmanaged();
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

    /// <summary>
    /// Appends a string representation of the children of this qdisc to the specified <see cref="StringBuilder"/>
    /// </summary>
    /// <remarks>
    /// Overriding types should only append the single-line context (index, priority, etc.) of their *direct* children on a partial line, and then call <see cref="ChildToTreeString(IClassifyingQdisc{THandle}, StringBuilder, int)"/> immediately after to append the child's string representation and recurse into the child's children.
    /// </remarks>
    /// <param name="builder">The <see cref="StringBuilder"/> to append the string representation to.</param>
    /// <param name="indent">The current indentation level, to be used with <see cref="StringBuilderExtensions.AppendIndent(StringBuilder, int)"/>.</param>
    protected virtual void ChildrenToTreeString(StringBuilder builder, int indent) => Pass();

    /// <inheritdoc/>
    public virtual string ToTreeString()
    {
        StringBuilder builder = new();
        ChildToTreeString(this, builder, 0);
        return builder.ToString();
    }

    /// <summary>
    /// Appends a string representation of the specified qdisc and its children to the specified <see cref="StringBuilder"/>.
    /// </summary>
    protected void ChildToTreeString(IClassifyingQdisc<THandle> qdisc, StringBuilder builder, int currentIndent)
    {
        if (qdisc is ClassifyingQdisc<THandle> classifyingQdisc)
        {
            builder.AppendLine(classifyingQdisc.ToString());
            builder.AppendIndent(currentIndent + 1).AppendLine(classifyingQdisc.Filters.ToString());
            classifyingQdisc.ChildrenToTreeString(builder, currentIndent + 1);
        }
    }
}
