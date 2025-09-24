using Cash.Threading.Workloads.Queuing;
using Cash.Threading.Workloads.Scheduling.Dispatchers;

namespace Cash.Threading.Workloads.Configuration.Dispatcher;

public interface IWorkloadDispatcherFactory
{
    internal IWorkloadDispatcher CreateDispatcher(IQdisc root);
}

public abstract class WorkloadDispatcherFactory : IWorkloadDispatcherFactory
{
    protected abstract IWorkloadDispatcher Create(IQdisc root);

    IWorkloadDispatcher IWorkloadDispatcherFactory.CreateDispatcher(IQdisc root) => Create(root);
}

public sealed class BoundedWorkloadDispatcherFactory : WorkloadDispatcherFactory
{
    private int _maxConcurrency = Environment.ProcessorCount;
    private bool _allowRecursiveScheduling;

    public BoundedWorkloadDispatcherFactory UseMaximumConcurrency(int maxConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        _maxConcurrency = maxConcurrency;
        return this;
    }

    public BoundedWorkloadDispatcherFactory AllowRecursiveScheduling(bool allow = true)
    {
        _allowRecursiveScheduling = allow;
        return this;
    }

    protected override IWorkloadDispatcher Create(IQdisc root) => new BoundedWorkloadDispatcher(root, _maxConcurrency, _allowRecursiveScheduling);
}