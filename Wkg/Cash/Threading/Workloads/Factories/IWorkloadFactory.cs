using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.WorkloadTypes;

namespace Cash.Threading.Workloads.Factories;

public interface IWorkloadFactory<THandle, TFactory>
    where THandle : unmanaged
    where TFactory : AbstractClasslessWorkloadFactory<THandle>, IWorkloadFactory<THandle, TFactory>
{
    internal static abstract TFactory Create(IClassifyingQdisc<THandle> root, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options);
}
