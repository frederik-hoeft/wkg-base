using Cash.Diagnostic;
using Cash.Threading.Workloads.Exceptions;
using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.Scheduling;
using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Queuing;

/// <summary>
/// Represents a sentinel implementation of <see cref="INotifyWorkScheduled"/> that throws an exception when a workload is scheduled.
/// </summary>
/// <remarks>
/// This implementation is used to prevent workloads from being scheduled on partially-initialized qdiscs.
/// </remarks>
internal sealed class WorkloadSchedulerSentinel<THandle> : IWorkloadScheduler<THandle>
    where THandle : unmanaged
{
    private WorkloadSchedulerSentinel() => Pass();

    /// <summary>
    /// A sentinal instance representing an uninitialized qdisc.
    /// </summary>
    public static WorkloadSchedulerSentinel<THandle> Uninitialized { get; } = new();

    /// <summary>
    /// A sentinal instance representing a completed qdisc.
    /// </summary>
    public static WorkloadSchedulerSentinel<THandle> Completed { get; } = new();

    public IClassifyingQdisc<THandle> Root => ThrowSentinelInstance<IClassifyingQdisc<THandle>>();

    public void Schedule(THandle handle, AbstractWorkloadBase workload) => ThrowSentinelInstance<object>();

    public void Schedule(AbstractWorkloadBase workload) => ThrowSentinelInstance<object>();

    public void Classify(object? state, AbstractWorkloadBase workload) => ThrowSentinelInstance<object>();

    [DoesNotReturn]
    private static T ThrowSentinelInstance<T>()
    {
        WorkloadSchedulingException exception = WorkloadSchedulingException.CreateVirtual(SR.ThreadingWorkloads_SchedulingFailed_SentinelInstance);
        DebugLog.WriteException(exception);
        throw exception;
    }

    [SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "Singleton sentinel, no resources to dispose.")]
    [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "Singleton sentinel, no resources to dispose.")]
    public void Dispose() => Pass();
}
