using Cash.Threading.Workloads.Configuration.Dispatcher;

namespace Cash.Threading.Workloads.Configuration;

public interface IQdiscBuilderContext
{
    int PoolSize { get; }

    bool UsePooling { get; }
}

internal sealed class QdiscBuilderContext : IQdiscBuilderContext
{
    public IWorkloadDispatcherFactory? WorkloadDispatcherFactory { get; set; }

    public int PoolSize { get; set; } = -1;

    public bool UsePooling => PoolSize > 0;

    public WorkloadContextOptions ContextOptions { get; set; } = new();
}