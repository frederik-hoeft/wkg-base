using Cash.Common.Extensions;
using Cash.Threading.Workloads.Configuration.Classful;
using Cash.Threading.Workloads.Configuration.Classful.Custom;
using Cash.Threading.Workloads.Configuration.Classification;
using Cash.Threading.Workloads.Configuration.Classless;
using Cash.Threading.Workloads.Configuration.Dispatcher;
using Cash.Threading.Workloads.Factories;
using Cash.Threading.Workloads.Queuing.Classful;
using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.Scheduling;
using Cash.Threading.Workloads.Scheduling.Dispatchers;
using Cash.Threading.Workloads.WorkloadTypes;
using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Configuration;

public abstract class WorkloadFactoryBuilderBase<THandle, TSelf> 
    where THandle : unmanaged
    where TSelf : WorkloadFactoryBuilderBase<THandle, TSelf>
{
    private protected readonly QdiscBuilderContext _context;
    private protected readonly IFilterManagerFactory _filterManagerFactory = DefaultFilterManagerFactory.Instance;

    private protected WorkloadFactoryBuilderBase(QdiscBuilderContext? context = null)
    {
        if (!GetType().Equals(typeof(TSelf)))
        {
            throw new InvalidOperationException($"The type parameter {typeof(TSelf).Name} does not match the type of the current builder {GetType().Name}.");
        }
        _context ??= context ?? new QdiscBuilderContext();
    }

    public TSelf UseWorkloadDispatcher(IWorkloadDispatcherFactory dispatcherFactory)
    {
        ArgumentNullException.ThrowIfNull(dispatcherFactory);
        _context.WorkloadDispatcherFactory = dispatcherFactory;
        return this.To<TSelf>();
    }

    public TSelf UseWorkloadDispatcher<TDispatcherFactory>(Action<TDispatcherFactory>? configureDispatcher = null)
        where TDispatcherFactory : class, IWorkloadDispatcherFactory, new()
    {
        TDispatcherFactory factory = new();
        configureDispatcher?.Invoke(factory);
        _context.WorkloadDispatcherFactory = factory;
        return this.To<TSelf>();
    }

    public TSelf UseWorkloadContextOptions(WorkloadContextOptions options)
    {
        _context.ContextOptions = options;
        return this.To<TSelf>();
    }

    public TSelf FlowExecutionContextToContinuations(bool flowExecutionContext = true)
    {
        _context.ContextOptions = _context.ContextOptions with { FlowExecutionContext = flowExecutionContext };
        return this.To<TSelf>();
    }

    public TSelf RunContinuationsOnCapturedContext(bool continueOnCapturedContext = true)
    {
        _context.ContextOptions = _context.ContextOptions with { ContinueOnCapturedContext = continueOnCapturedContext };
        return this.To<TSelf>();
    }

    public TSelf UseAnonymousWorkloadPooling(int poolSize = 64)
    {
        _context.PoolSize = poolSize;
        return this.To<TSelf>();
    }

    [SuppressMessage(RELIABILITY, CA2000_DISPOSE_OBJECT, Justification = JUSTIFY_CA2000_OWNERSHIP_TRANSFER_TO_CALLER)]
    private protected TWorkloadFactory UseClasslessRootCore<TWorkloadFactory, TRoot>(THandle rootHandle, Action<TRoot> rootConfiguration, Action<IFilterManager>? configureFilters = null)
        where TRoot : ClasslessQdiscBuilder<TRoot>, IClasslessQdiscBuilder<TRoot>
        where TWorkloadFactory : AbstractClasslessWorkloadFactory<THandle>, IWorkloadFactory<THandle, TWorkloadFactory>
    {
        ArgumentNullException.ThrowIfNull(rootConfiguration);
        TRoot rootBuilder = TRoot.CreateBuilder(_context);
        rootConfiguration(rootBuilder);
        IFilterManager filters = _filterManagerFactory.CreateFilterManager(configureFilters);
        IClassifyingQdisc<THandle> root = rootBuilder.Build(rootHandle, filters);
        IWorkloadDispatcherFactory dispatcherFactory = GetDispatcherFactoryOrThrow();
        IWorkloadDispatcher dispatcher = dispatcherFactory.CreateDispatcher(root);
        IWorkloadScheduler<THandle> scheduler = new WorkloadScheduler<THandle>(root, dispatcher);
        root.InternalInitialize(scheduler);
        AnonymousWorkloadPoolManager? pool = null;
        if (_context.UsePooling)
        {
            pool = new AnonymousWorkloadPoolManager(_context.PoolSize);
        }
        return TWorkloadFactory.Create(scheduler, pool, _context.ContextOptions);
    }

    [SuppressMessage(RELIABILITY, CA2000_DISPOSE_OBJECT, Justification = JUSTIFY_CA2000_OWNERSHIP_TRANSFER_TO_CALLER)]
    private protected TWorkloadFactory UseClassfulRootCore<TWorkloadFactory, TRoot>(THandle rootHandle, Action<ClassfulBuilder<THandle, TRoot>> rootClassConfiguration)
        where TRoot : ClassfulQdiscBuilder<TRoot>, IClassfulQdiscBuilder<TRoot>
        where TWorkloadFactory : AbstractClasslessWorkloadFactory<THandle>, IWorkloadFactory<THandle, TWorkloadFactory>
    {
        ArgumentNullException.ThrowIfNull(rootClassConfiguration);
        ClassfulBuilder<THandle, TRoot> rootClassBuilder = new(rootHandle, _context);
        rootClassConfiguration(rootClassBuilder);
        IClassfulQdisc<THandle> root = rootClassBuilder.Build();
        IWorkloadDispatcherFactory dispatcherFactory = GetDispatcherFactoryOrThrow();
        IWorkloadDispatcher dispatcher = dispatcherFactory.CreateDispatcher(root);
        IWorkloadScheduler<THandle> scheduler = new WorkloadScheduler<THandle>(root, dispatcher);
        root.InternalInitialize(scheduler);
        AnonymousWorkloadPoolManager? pool = null;
        if (_context.UsePooling)
        {
            pool = new AnonymousWorkloadPoolManager(_context.PoolSize);
        }
        return TWorkloadFactory.Create(scheduler, pool, _context.ContextOptions);
    }

    [SuppressMessage(RELIABILITY, CA2000_DISPOSE_OBJECT, Justification = JUSTIFY_CA2000_OWNERSHIP_TRANSFER_TO_CALLER)]
    private protected TWorkloadFactory UseClassfulRootCore<TWorkloadFactory, TRoot>(THandle rootHandle, Action<TRoot> rootConfiguration)
        where TRoot : CustomClassfulQdiscBuilder<THandle, TRoot>, ICustomClassfulQdiscBuilder<THandle, TRoot>
        where TWorkloadFactory : AbstractClasslessWorkloadFactory<THandle>, IWorkloadFactory<THandle, TWorkloadFactory>
    {
        ArgumentNullException.ThrowIfNull(rootConfiguration);
        TRoot rootClassBuilder = TRoot.CreateBuilder(rootHandle, _context);
        rootConfiguration(rootClassBuilder);
        IClassfulQdisc<THandle> root = rootClassBuilder.Build();
        IWorkloadDispatcherFactory dispatcherFactory = GetDispatcherFactoryOrThrow();
        IWorkloadDispatcher dispatcher = dispatcherFactory.CreateDispatcher(root);
        IWorkloadScheduler<THandle> scheduler = new WorkloadScheduler<THandle>(root, dispatcher);
        AnonymousWorkloadPoolManager? pool = null;
        if (_context.UsePooling)
        {
            pool = new AnonymousWorkloadPoolManager(_context.PoolSize);
        }
        return TWorkloadFactory.Create(scheduler, pool, _context.ContextOptions);
    }

    private protected IWorkloadDispatcherFactory GetDispatcherFactoryOrThrow()
    {
        if (_context.WorkloadDispatcherFactory is { } factory)
        {
            return factory;
        }
        throw new InvalidOperationException("A workload dispatcher factory must be configured before building the workload factory. Use the UseWorkloadDispatcher method to configure a dispatcher factory.");
    }
}

public class WorkloadFactoryBuilder<THandle> : WorkloadFactoryBuilderBase<THandle, WorkloadFactoryBuilder<THandle>> 
    where THandle : unmanaged
{
    internal WorkloadFactoryBuilder(QdiscBuilderContext context) : base(context) => Pass();

    internal WorkloadFactoryBuilder() => Pass();

    public ClasslessWorkloadFactory<THandle> UseClasslessRoot<TRoot>(THandle rootHandle)
        where TRoot : ClasslessQdiscBuilder<TRoot>, IClasslessQdiscBuilder<TRoot> =>
            UseClasslessRootCore<ClasslessWorkloadFactory<THandle>, TRoot>(rootHandle, Pass);

    public ClasslessWorkloadFactory<THandle> UseClasslessRoot<TRoot>(THandle rootHandle, Action<TRoot> rootConfiguration)
        where TRoot : ClasslessQdiscBuilder<TRoot>, IClasslessQdiscBuilder<TRoot> =>
            UseClasslessRootCore<ClasslessWorkloadFactory<THandle>, TRoot>(rootHandle, rootConfiguration);

    public ClassfulWorkloadFactory<THandle> UseClassfulRoot<TRoot>(THandle rootHandle)
        where TRoot : ClassfulQdiscBuilder<TRoot>, IClassfulQdiscBuilder<TRoot> => 
            UseClassfulRootCore<ClassfulWorkloadFactory<THandle>, TRoot>(rootHandle, Pass);

    public ClassfulWorkloadFactory<THandle> UseClassfulRoot<TRoot>(THandle rootHandle, Action<ClassfulBuilder<THandle, TRoot>> rootClassConfiguration)
        where TRoot : ClassfulQdiscBuilder<TRoot>, IClassfulQdiscBuilder<TRoot> =>
            UseClassfulRootCore<ClassfulWorkloadFactory<THandle>, TRoot>(rootHandle, rootClassConfiguration);

    public ClassfulWorkloadFactory<THandle> UseClassfulRoot<TRoot>(THandle rootHandle, Action<TRoot> rootConfiguration)
        where TRoot : CustomClassfulQdiscBuilder<THandle, TRoot>, ICustomClassfulQdiscBuilder<THandle, TRoot> =>
            UseClassfulRootCore<ClassfulWorkloadFactory<THandle>, TRoot >(rootHandle, rootConfiguration);
}