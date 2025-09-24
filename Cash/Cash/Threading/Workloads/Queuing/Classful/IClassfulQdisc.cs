using Cash.Threading.Workloads.Queuing.Classless;

namespace Cash.Threading.Workloads.Queuing.Classful;

public interface IClassfulQdisc<THandle> : IQdisc, IClassifyingQdisc<THandle>
    where THandle : unmanaged
{
    /// <summary>
    /// Attempts to add the child to the qdisc.
    /// </summary>
    /// <param name="child">The child to add.</param>
    /// <returns><see langword="true"/> if the child was added, <see langword="false"/> if the child was already added.</returns>
    bool TryAddChild(IClassifyingQdisc<THandle> child);

    /// <summary>
    /// Attempts to remove the child from the qdisc.
    /// </summary>
    /// <param name="child">The child to remove.</param>
    /// <returns><see langword="true"/> if the child was removed, <see langword="false"/> if the child was not found or in use.</returns>
    bool TryRemoveChild(IClassifyingQdisc<THandle> child);

    /// <summary>
    /// Removes the child from the qdisc, blocking until the child is no longer in use if necessary.
    /// </summary>
    /// <param name="child">The child to remove.</param>
    /// <returns><see langword="true"/> if the child was removed, <see langword="false"/> if the child was not found.</returns>
    bool RemoveChild(IClassifyingQdisc<THandle> child);
}