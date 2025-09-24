using Cash.Threading.Workloads;
using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.Scheduling;

namespace Cash.Tests.Threading.Workloads.Queuing.Classless;

internal readonly struct DummyScheduler(IClassifyingQdisc<int> root) : IWorkloadScheduler<int>
{
    public IClassifyingQdisc<int> Root => root;

    public void Classify(object? state, AbstractWorkloadBase workload) => Pass();
    public void Dispose() => Pass();
    public void Schedule(int handle, AbstractWorkloadBase workload) => Pass();
    public void Schedule(AbstractWorkloadBase workload) => Pass();
}