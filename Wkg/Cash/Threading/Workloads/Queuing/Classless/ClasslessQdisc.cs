using Cash.Diagnostic;
using Cash.Threading.Workloads.Exceptions;
using Cash.Threading.Workloads.Queuing.Classification;
using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Queuing.Classless;

/// <summary>
/// A leaf qdisc with no child-management capabilities.
/// </summary>
/// <typeparam name="THandle">The type of the handle.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ClasslessQdisc{THandle}"/> class.
/// </remarks>
/// <param name="handle">The handle of the qdisc.</param>
public abstract class ClasslessQdisc<THandle>(THandle handle, IFilterManager filters) 
    : ClassifyingQdisc<THandle>(handle, filters) where THandle : unmanaged
{
    /// <summary>
    /// Enqueues the <paramref name="workload"/> onto the local queue, without additional checks or setup.
    /// </summary>
    /// <param name="workload">The already bound workload to enqueue.</param>
    protected abstract void EnqueueDirectLocal(AbstractWorkloadBase workload);

    protected override bool CanClassify(object? state) => Filters.Match(state);

    public override bool TryFindChild(THandle handle, [NotNullWhen(true)] out IClassifyingQdisc<THandle>? child)
    {
        child = null;
        return false;
    }

    /// <inheritdoc/>
    protected override void EnqueueDirect(AbstractWorkloadBase workload)
    {
        if (TryBindWorkload(workload))
        {
            EnqueueDirectLocal(workload);
            NotifyWorkScheduled();
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
}
