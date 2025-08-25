using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Cash.Threading.Workloads.Continuations;

internal abstract class TypedWorkloadContinuation<TWorkload> : IWorkloadContinuation
    where TWorkload : AbstractWorkloadBase
{
    public void Invoke(AbstractWorkloadBase workload)
    {
        Debug.Assert(workload is TWorkload);
        InvokeInternal(Unsafe.As<TWorkload>(workload));
    }

    public void InvokeInline(AbstractWorkloadBase workload)
    {
        Debug.Assert(workload is TWorkload);
        InvokeInternal(Unsafe.As<TWorkload>(workload));
    }

    protected abstract void InvokeInternal(TWorkload workload);
}
