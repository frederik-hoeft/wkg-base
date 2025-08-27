using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Queuing.Classless;

namespace Cash.Threading.Workloads.Queuing.Classful;

public abstract class ClassfulQdisc<THandle>(THandle handle, IFilterManager filters) 
    : ClassifyingQdisc<THandle>(handle, filters), IClassfulQdisc<THandle> 
    where THandle : unmanaged
{

    /// <summary>
    /// Called when a workload is scheduled to any child qdisc.
    /// </summary>
    /// <remarks>
    /// <see langword="WARNING"/>: be sure to call base.OnWorkScheduled() in derived classes. Otherwise, the parent scheduler will not be notified of the scheduled workload.
    /// </remarks>
    protected virtual void OnWorkScheduled() => ParentScheduler.OnWorkScheduled();

    /// <summary>
    /// Binds the specified child qdisc to this qdisc, allowing child notifications to be propagated to the parent scheduler.
    /// </summary>
    /// <param name="child">The child qdisc to bind.</param>
    protected void BindChildQdisc(IQdisc child) =>
        child.InternalInitialize(this);

    /// <inheritdoc/>
    public abstract bool RemoveChild(IClassifyingQdisc<THandle> child);

    /// <inheritdoc/>
    public abstract bool TryAddChild(IClassifyingQdisc<THandle> child);

    /// <inheritdoc/>
    public abstract bool TryRemoveChild(IClassifyingQdisc<THandle> child);

    void INotifyWorkScheduled.OnWorkScheduled() => OnWorkScheduled();

    INotifyWorkScheduled IClassifyingQdisc.ParentScheduler => ParentScheduler;

    void INotifyWorkScheduled.DisposeRoot() => ParentScheduler.DisposeRoot();
}