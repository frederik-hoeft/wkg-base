using Cash.Threading.Workloads.Queuing.Classful;
using Cash.Threading.Workloads.Queuing.Classification;

namespace Cash.Threading.Workloads.Configuration.Classful;

public interface IClassfulQdiscBuilder
{
    internal IClassfulQdisc<THandle> BuildInternal<THandle>(THandle handle, IFilterManager filters) where THandle : unmanaged;
}

public interface IClassfulQdiscBuilder<TSelf> where TSelf : IClassfulQdiscBuilder<TSelf>
{
    static abstract TSelf CreateBuilder(IQdiscBuilderContext context);
}

