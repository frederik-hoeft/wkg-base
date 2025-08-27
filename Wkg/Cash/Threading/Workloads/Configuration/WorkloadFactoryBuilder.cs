using Cash.Threading.Workloads.Queuing.Classification;

namespace Cash.Threading.Workloads.Configuration;

public static class WorkloadFactoryBuilder
{
    public static WorkloadFactoryBuilder<THandle> Create<THandle>() where THandle : unmanaged => new();
}
