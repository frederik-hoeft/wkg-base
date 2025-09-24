using Cash.Threading.Workloads.Queuing.Classful;
using Cash.Threading.Workloads.Exceptions;
using Cash.Threading.Workloads.Queuing.Classification;

namespace Cash.Threading.Workloads.Configuration.Classful;

public abstract class ClassfulQdiscBuilder<TSelf> : IClassfulQdiscBuilder where TSelf : ClassfulQdiscBuilder<TSelf>, IClassfulQdiscBuilder<TSelf>
{
    internal protected abstract IClassfulQdisc<THandle> BuildInternal<THandle>(THandle handle, IFilterManager filters) where THandle : unmanaged;

    internal IClassfulQdisc<THandle> Build<THandle>(THandle handle, IFilterManager filters) where THandle : unmanaged
    {
        WorkloadSchedulingException.ThrowIfHandleIsDefault(handle);

        return BuildInternal(handle, filters);
    }

    IClassfulQdisc<THandle> IClassfulQdiscBuilder.BuildInternal<THandle>(THandle handle, IFilterManager filters) => BuildInternal(handle, filters);
}
