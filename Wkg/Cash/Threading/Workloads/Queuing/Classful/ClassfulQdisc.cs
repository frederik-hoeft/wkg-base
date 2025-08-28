using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Queuing.Classless;

namespace Cash.Threading.Workloads.Queuing.Classful;

public abstract class ClassfulQdisc<THandle>(THandle handle, IFilterManager filters) 
    : ClassifyingQdisc<THandle>(handle, filters), IClassfulQdisc<THandle> 
    where THandle : unmanaged
{
    /// <summary>
    /// Binds the specified child qdisc to this qdisc, allowing child notifications to be propagated to the parent scheduler.
    /// </summary>
    /// <param name="child">The child qdisc to bind.</param>
    protected void BindChildQdisc(IQdisc<THandle> child) =>
        child.InternalInitialize(Scheduler);

    /// <inheritdoc/>
    public abstract bool RemoveChild(IClassifyingQdisc<THandle> child);

    /// <inheritdoc/>
    public abstract bool TryAddChild(IClassifyingQdisc<THandle> child);

    /// <inheritdoc/>
    public abstract bool TryRemoveChild(IClassifyingQdisc<THandle> child);
}