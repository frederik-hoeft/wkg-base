using Cash.Threading.Workloads.Queuing.Classful;
using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.WorkloadTypes;
using System.Runtime.CompilerServices;

namespace Cash.Threading.Workloads.Factories;

public class ClassfulWorkloadFactoryWithDI<THandle> : AbstractClasslessWorkloadFactoryWithDI<THandle>, 
    IWorkloadFactory<THandle, ClassfulWorkloadFactoryWithDI<THandle>>, 
    IClassfulWorkloadFactory<THandle> 
    where THandle : unmanaged
{
    private ClassfulWorkloadFactoryWithDI(IClassfulQdisc<THandle> root, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options) 
        : base(root, pool, options)
    {
    }

    public IClassfulQdisc<THandle> Root => Unsafe.As<IClassifyingQdisc<THandle>, IClassfulQdisc<THandle>>(ref RootRef);

    static ClassfulWorkloadFactoryWithDI<THandle> IWorkloadFactory<THandle, ClassfulWorkloadFactoryWithDI<THandle>>
        .Create(IClassifyingQdisc<THandle> root, AnonymousWorkloadPoolManager? pool, WorkloadContextOptions? options) => 
            new((IClassfulQdisc<THandle>)root, pool, options);
}
