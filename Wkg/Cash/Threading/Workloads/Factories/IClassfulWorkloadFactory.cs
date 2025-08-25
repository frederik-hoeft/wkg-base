using Cash.Threading.Workloads.Queuing.Classful;

namespace Cash.Threading.Workloads.Factories;

public interface IClassfulWorkloadFactory<THandle> where THandle : unmanaged
{
    IClassfulQdisc<THandle> Root { get; }
}
