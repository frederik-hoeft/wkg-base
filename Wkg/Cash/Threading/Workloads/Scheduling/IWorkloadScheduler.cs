using Cash.Threading.Workloads.Queuing.Classless;

namespace Cash.Threading.Workloads.Scheduling;

public interface IWorkloadScheduler<THandle> : IDisposable where THandle : unmanaged
{
    IClassifyingQdisc<THandle> Root { get; }

    void Schedule(THandle handle, AbstractWorkloadBase workload);

    void Schedule(AbstractWorkloadBase workload);

    void Classify(object? state, AbstractWorkloadBase workload);
}
