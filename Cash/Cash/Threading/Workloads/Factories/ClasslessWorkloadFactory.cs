using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.Scheduling;
using Cash.Threading.Workloads.WorkloadTypes;

namespace Cash.Threading.Workloads.Factories;

public class ClasslessWorkloadFactory<THandle> : AbstractClasslessWorkloadFactory<THandle>, IWorkloadFactory<THandle, ClasslessWorkloadFactory<THandle>>, IClasslessWorkloadFactory<THandle>
    where THandle : unmanaged
{
    internal ClasslessWorkloadFactory(IWorkloadScheduler<THandle> scheduler, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options) 
        : base(scheduler, pool, options)
    {
    }

    public new IClassifyingQdisc<THandle> Root => base.Root;

    static ClasslessWorkloadFactory<THandle> IWorkloadFactory<THandle, ClasslessWorkloadFactory<THandle>>
        .Create(IWorkloadScheduler<THandle> scheduler, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options) => 
            new(scheduler, pool, options);
}
