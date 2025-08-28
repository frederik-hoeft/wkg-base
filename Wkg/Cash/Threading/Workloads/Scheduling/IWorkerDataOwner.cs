namespace Cash.Threading.Workloads.Scheduling;

public interface IWorkerDataOwner
{
    internal protected int InstanceHash { get; }
}