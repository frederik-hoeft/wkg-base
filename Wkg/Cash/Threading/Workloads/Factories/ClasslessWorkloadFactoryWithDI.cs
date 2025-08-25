using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.WorkloadTypes;

namespace Cash.Threading.Workloads.Factories;

public class ClasslessWorkloadFactoryWithDI<THandle> : AbstractClasslessWorkloadFactoryWithDI<THandle>, 
    IWorkloadFactory<THandle, ClasslessWorkloadFactoryWithDI<THandle>>, 
    IClasslessWorkloadFactory<THandle>
    where THandle : unmanaged
{
    internal ClasslessWorkloadFactoryWithDI(IClassifyingQdisc<THandle> root, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options) 
        : base(root, pool, options)
    {
    }

    public IClassifyingQdisc<THandle> Root => RootRef;

    static ClasslessWorkloadFactoryWithDI<THandle> IWorkloadFactory<THandle, ClasslessWorkloadFactoryWithDI<THandle>>
        .Create(IClassifyingQdisc<THandle> root, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options) => 
            new(root, pool, options);
}
