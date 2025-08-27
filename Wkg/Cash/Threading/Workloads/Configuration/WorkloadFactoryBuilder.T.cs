using Cash.Threading.Workloads.DependencyInjection.Implementations;
using Cash.Threading.Workloads.Factories;
using Cash.Threading.Workloads.Configuration.Classful.Custom;
using Cash.Threading.Workloads.DependencyInjection.Configuration;
using Cash.Threading.Workloads.Configuration.Classless;
using Cash.Threading.Workloads.Scheduling;
using Cash.Threading.Workloads.Configuration.Classful;
using Cash.Threading.Workloads.Queuing.Classful;
using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.DependencyInjection;
using Cash.Threading.Workloads.WorkloadTypes;
using Cash.Common.Extensions;
using Cash.Threading.Workloads.Queuing.Classification;
using Cash.Threading.Workloads.Configuration.Classification;

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

    public TSelf UseMaximumConcurrency(int maximumConcurrency)
    {
        _context.MaximumConcurrency = maximumConcurrency;
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

    private protected TWorkloadFactory UseClasslessRootCore<TWorkloadFactory, TRoot>(THandle rootHandle, Action<TRoot> rootConfiguration, Action<IFilterManager>? configureFilters = null)
        where TRoot : ClasslessQdiscBuilder<TRoot>, IClasslessQdiscBuilder<TRoot>
        where TWorkloadFactory : AbstractClasslessWorkloadFactory<THandle>, IWorkloadFactory<THandle, TWorkloadFactory>
    {
        TRoot rootBuilder = TRoot.CreateBuilder(_context);
        rootConfiguration(rootBuilder);
        IFilterManager filters = _filterManagerFactory.CreateFilterManager(configureFilters);
        IClassifyingQdisc<THandle> qdisc = rootBuilder.Build(rootHandle, filters);
        WorkloadDispatcher scheduler = _context.ServiceProviderFactory is null
            ? new WorkloadDispatcher(qdisc, _context.MaximumConcurrency)
            : new WorkloadDispatcherWithDI(qdisc, _context.MaximumConcurrency, _context.ServiceProviderFactory);
        qdisc.InternalInitialize(scheduler);
        AnonymousWorkloadPoolManager? pool = null;
        if (_context.UsePooling)
        {
            pool = new AnonymousWorkloadPoolManager(_context.PoolSize);
        }
        return TWorkloadFactory.Create(qdisc, pool, _context.ContextOptions);
    }

    private protected TWorkloadFactory UseClassfulRootCore<TWorkloadFactory, TRoot>(THandle rootHandle, Action<ClassfulBuilder<THandle, TRoot>> rootClassConfiguration)
        where TRoot : ClassfulQdiscBuilder<TRoot>, IClassfulQdiscBuilder<TRoot>
        where TWorkloadFactory : AbstractClasslessWorkloadFactory<THandle>, IWorkloadFactory<THandle, TWorkloadFactory>
    {
        ClassfulBuilder<THandle, TRoot> rootClassBuilder = new(rootHandle, _context);
        rootClassConfiguration(rootClassBuilder);
        IClassfulQdisc<THandle> rootQdisc = rootClassBuilder.Build();

        WorkloadDispatcher scheduler = _context.ServiceProviderFactory is null
            ? new WorkloadDispatcher(rootQdisc, _context.MaximumConcurrency)
            : new WorkloadDispatcherWithDI(rootQdisc, _context.MaximumConcurrency, _context.ServiceProviderFactory);
        rootQdisc.InternalInitialize(scheduler);
        AnonymousWorkloadPoolManager? pool = null;
        if (_context.UsePooling)
        {
            pool = new AnonymousWorkloadPoolManager(_context.PoolSize);
        }
        return TWorkloadFactory.Create(rootQdisc, pool, _context.ContextOptions);
    }

    private protected TWorkloadFactory UseClassfulRootCore<TWorkloadFactory, TRoot>(THandle rootHandle, Action<TRoot> rootConfiguration)
        where TRoot : CustomClassfulQdiscBuilder<THandle, TRoot>, ICustomClassfulQdiscBuilder<THandle, TRoot>
        where TWorkloadFactory : AbstractClasslessWorkloadFactory<THandle>, IWorkloadFactory<THandle, TWorkloadFactory>
    {
        TRoot rootClassBuilder = TRoot.CreateBuilder(rootHandle, _context);
        rootConfiguration(rootClassBuilder);
        IClassfulQdisc<THandle> rootQdisc = rootClassBuilder.Build();

        WorkloadDispatcher scheduler = _context.ServiceProviderFactory is null
            ? new WorkloadDispatcher(rootQdisc, _context.MaximumConcurrency)
            : new WorkloadDispatcherWithDI(rootQdisc, _context.MaximumConcurrency, _context.ServiceProviderFactory);
        rootQdisc.InternalInitialize(scheduler);
        AnonymousWorkloadPoolManager? pool = null;
        if (_context.UsePooling)
        {
            pool = new AnonymousWorkloadPoolManager(_context.PoolSize);
        }
        return TWorkloadFactory.Create(rootQdisc, pool, _context.ContextOptions);
    }
}

public class WorkloadFactoryBuilder<THandle> : WorkloadFactoryBuilderBase<THandle, WorkloadFactoryBuilder<THandle>> 
    where THandle : unmanaged
{
    internal WorkloadFactoryBuilder(QdiscBuilderContext context) : base(context) => Pass();

    internal WorkloadFactoryBuilder() => Pass();

    public WorkloadFactoryBuilderWithDI<THandle> UseDependencyInjection(Action<WorkloadServiceProviderBuilder> configurationAction) =>
        UseDependencyInjection<SimpleWorkloadServiceProviderFactory>(configurationAction);

    public WorkloadFactoryBuilderWithDI<THandle> UseDependencyInjection<TServiceProviderFactory>(Action<WorkloadServiceProviderBuilder> configurationAction)
        where TServiceProviderFactory : class, IWorkloadServiceProviderFactory, new()
    {
        IWorkloadServiceProviderFactory factoryProvider = new TServiceProviderFactory();
        WorkloadServiceProviderBuilder builder = new(factoryProvider);
        configurationAction.Invoke(builder);
        _context.ServiceProviderFactory = builder.Build();
        return new WorkloadFactoryBuilderWithDI<THandle>(_context);
    }

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

public class WorkloadFactoryBuilderWithDI<THandle> : WorkloadFactoryBuilderBase<THandle, WorkloadFactoryBuilderWithDI<THandle>>
    where THandle : unmanaged
{
    internal WorkloadFactoryBuilderWithDI(QdiscBuilderContext context) : base(context) => Pass();

    public ClasslessWorkloadFactoryWithDI<THandle> UseClasslessRoot<TRoot>(THandle rootHandle)
        where TRoot : ClasslessQdiscBuilder<TRoot>, IClasslessQdiscBuilder<TRoot> =>
            UseClasslessRootCore<ClasslessWorkloadFactoryWithDI<THandle>, TRoot>(rootHandle, Pass);

    public ClasslessWorkloadFactoryWithDI<THandle> UseClasslessRoot<TRoot>(THandle rootHandle, Action<TRoot> rootConfiguration)
        where TRoot : ClasslessQdiscBuilder<TRoot>, IClasslessQdiscBuilder<TRoot> =>
            UseClasslessRootCore<ClasslessWorkloadFactoryWithDI<THandle>, TRoot>(rootHandle, rootConfiguration);

    public ClassfulWorkloadFactoryWithDI<THandle> UseClassfulRoot<TRoot>(THandle rootHandle)
        where TRoot : ClassfulQdiscBuilder<TRoot>, IClassfulQdiscBuilder<TRoot> =>
            UseClassfulRootCore<ClassfulWorkloadFactoryWithDI<THandle>, TRoot>(rootHandle, Pass);

    public ClassfulWorkloadFactoryWithDI<THandle> UseClassfulRoot<TRoot>(THandle rootHandle, Action<ClassfulBuilder<THandle, TRoot>> rootClassConfiguration)
        where TRoot : ClassfulQdiscBuilder<TRoot>, IClassfulQdiscBuilder<TRoot> =>
            UseClassfulRootCore<ClassfulWorkloadFactoryWithDI<THandle>, TRoot>(rootHandle, rootClassConfiguration);

    public ClassfulWorkloadFactoryWithDI<THandle> UseClassfulRoot<TRoot>(THandle rootHandle, Action<TRoot> rootConfiguration)
        where TRoot : CustomClassfulQdiscBuilder<THandle, TRoot>, ICustomClassfulQdiscBuilder<THandle, TRoot> =>
            UseClassfulRootCore<ClassfulWorkloadFactoryWithDI<THandle>, TRoot>(rootHandle, rootConfiguration);
}