using Cash.Threading.Workloads.Queuing.Classful;
using Cash.Threading.Workloads.Scheduling;
using Cash.Threading.Workloads.WorkloadTypes;

namespace Cash.Threading.Workloads.Factories;

public class ClassfulWorkloadFactory<THandle> : AbstractClasslessWorkloadFactory<THandle>, 
    IWorkloadFactory<THandle, ClassfulWorkloadFactory<THandle>>, 
    IClassfulWorkloadFactory<THandle> 
    where THandle : unmanaged
{
    internal ClassfulWorkloadFactory(IWorkloadScheduler<THandle> scheduler, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options)
        : base(scheduler, pool, options) => scheduler.AssertHasClassfulRoot();

    public new IClassfulQdisc<THandle> Root => (IClassfulQdisc<THandle>)base.Root;

    static ClassfulWorkloadFactory<THandle> IWorkloadFactory<THandle, ClassfulWorkloadFactory<THandle>>
        .Create(IWorkloadScheduler<THandle> scheduler, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options) => new(scheduler, pool, options);
}