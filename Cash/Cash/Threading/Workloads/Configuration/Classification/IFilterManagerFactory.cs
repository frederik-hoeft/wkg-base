using Cash.Threading.Workloads.Queuing.Classification;

namespace Cash.Threading.Workloads.Configuration.Classification;

public interface IFilterManagerFactory
{
    IFilterManager CreateFilterManager();

    IFilterManager CreateFilterManager(Action<IFilterManager>? configure);
}

public sealed class DefaultFilterManagerFactory : IFilterManagerFactory
{
    public IFilterManager CreateFilterManager() => new FilterManager();

    public IFilterManager CreateFilterManager(Action<IFilterManager>? configure)
    {
        FilterManager manager = new();
        if (configure is not null)
        {
            configure.Invoke(manager);
        }
        else
        {
            manager.AddMatchAll();
        }
        return manager;
    }

    public static DefaultFilterManagerFactory Instance { get; } = new();
}