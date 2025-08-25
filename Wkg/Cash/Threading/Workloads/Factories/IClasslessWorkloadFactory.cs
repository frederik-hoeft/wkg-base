using Cash.Threading.Workloads.Queuing.Classless;

namespace Cash.Threading.Workloads.Factories;

public interface IClasslessWorkloadFactory<THandle> where THandle : unmanaged
{
    IClassifyingQdisc<THandle> Root { get; }
}
