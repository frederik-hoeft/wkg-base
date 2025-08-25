using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.WorkloadTypes;

namespace Cash.Threading.Workloads.Factories;

public class ClasslessWorkloadFactory<THandle> : AbstractClasslessWorkloadFactory<THandle>, IWorkloadFactory<THandle, ClasslessWorkloadFactory<THandle>>, IClasslessWorkloadFactory<THandle>
    where THandle : unmanaged
{
    internal ClasslessWorkloadFactory(IClassifyingQdisc<THandle> root, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options) 
        : base(root, pool, options)
    {
    }

    public IClassifyingQdisc<THandle> Root => RootRef;

    static ClasslessWorkloadFactory<THandle> IWorkloadFactory<THandle, ClasslessWorkloadFactory<THandle>>
        .Create(IClassifyingQdisc<THandle> root, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options) => 
            new(root, pool, options);
}
