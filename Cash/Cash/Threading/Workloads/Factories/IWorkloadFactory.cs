using Cash.Threading.Workloads.Scheduling;
using Cash.Threading.Workloads.WorkloadTypes;

namespace Cash.Threading.Workloads.Factories;

public interface IWorkloadFactory<THandle, TFactory>
    where THandle : unmanaged
    where TFactory : AbstractClasslessWorkloadFactory<THandle>, IWorkloadFactory<THandle, TFactory>
{
    internal static abstract TFactory Create(IWorkloadScheduler<THandle> scheduler, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options);
}
