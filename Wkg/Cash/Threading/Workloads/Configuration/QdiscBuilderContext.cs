using Cash.Threading.Workloads.DependencyInjection;

namespace Cash.Threading.Workloads.Configuration;

public interface IQdiscBuilderContext
{
    int MaximumConcurrency { get; }

    int PoolSize { get; }

    bool UsePooling { get; }
}

internal sealed class QdiscBuilderContext : IQdiscBuilderContext
{
    public int MaximumConcurrency { get; set; } = 2;

    public int PoolSize { get; set; } = -1;

    public bool UsePooling => PoolSize > 0;

    public IWorkloadServiceProviderFactory? ServiceProviderFactory { get; set; }

    public WorkloadContextOptions ContextOptions { get; set; } = new();
}