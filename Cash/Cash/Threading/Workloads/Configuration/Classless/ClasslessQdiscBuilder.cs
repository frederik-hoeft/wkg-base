using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.Exceptions;
using Cash.Threading.Workloads.Queuing.Classification;
using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Configuration.Classless;

public interface IClasslessQdiscBuilder
{
    IClassifyingQdisc<THandle> Build<THandle>(THandle handle, IFilterManager filters) where THandle : unmanaged;

    IClassifyingQdisc<THandle> BuildUnsafe<THandle>(THandle handle = default, IFilterManager? filters = null) where THandle : unmanaged;
}

public interface IClasslessQdiscBuilder<TSelf> : IClasslessQdiscBuilder where TSelf : ClasslessQdiscBuilder<TSelf>, IClasslessQdiscBuilder<TSelf>
{
    static abstract TSelf CreateBuilder(IQdiscBuilderContext context);
}

public abstract class ClasslessQdiscBuilder<TSelf> : IClasslessQdiscBuilder where TSelf : ClasslessQdiscBuilder<TSelf>, IClasslessQdiscBuilder<TSelf>
{
    protected abstract IClassifyingQdisc<THandle> BuildInternal<THandle>(THandle handle, IFilterManager filters) where THandle : unmanaged;

    [SuppressMessage(RELIABILITY, CA2000_DISPOSE_OBJECT, Justification = JUSTIFY_CA2000_OWNERSHIP_TRANSFER_TO_CALLER)]
    IClassifyingQdisc<THandle> IClasslessQdiscBuilder.BuildUnsafe<THandle>(THandle handle, IFilterManager? filters) => 
        BuildInternal(handle, filters ?? FilterManager.MatchNothing());

    IClassifyingQdisc<THandle> IClasslessQdiscBuilder.Build<THandle>(THandle handle, IFilterManager filters)
    {
        WorkloadSchedulingException.ThrowIfHandleIsDefault(handle);

        return BuildInternal(handle, filters);
    }
}