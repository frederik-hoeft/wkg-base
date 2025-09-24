using Cash.Threading.Workloads.Configuration.Classful.Custom;
using Cash.Threading.Workloads.Configuration.Classless;
using Cash.Threading.Workloads.Queuing.Classful;
using Cash.Threading.Workloads.Queuing.Classless;
using Cash.Threading.Workloads.Exceptions;
using Cash.Threading.Workloads.Queuing.Classification;
using System.Diagnostics.CodeAnalysis;
using Cash.Threading.Workloads.Configuration.Classification;

namespace Cash.Threading.Workloads.Configuration.Classful;

public sealed class ClassfulBuilder<THandle, TQdisc>
    where THandle : unmanaged
    where TQdisc : ClassfulQdiscBuilder<TQdisc>, IClassfulQdiscBuilder<TQdisc>
{
    private readonly QdiscBuilderContext _context;
    private readonly THandle _handle;
    private readonly List<IClassifyingQdisc<THandle>> _children = [];
    private readonly IFilterManagerFactory _filterManagerFactory = DefaultFilterManagerFactory.Instance;
    private IFilterManager? _filters;
    private TQdisc? _qdiscBuilder;

    internal ClassfulBuilder(THandle handle, QdiscBuilderContext context)
    {
        _handle = handle;
        _context = context;
    }

    public ClassfulBuilder(THandle handle, IQdiscBuilderContext context) : this(handle, (QdiscBuilderContext)context) => Pass();

    public ClassfulBuilder<THandle, TQdisc> AddClasslessChild<TChild>(THandle childHandle)
        where TChild : ClasslessQdiscBuilder<TChild>, IClasslessQdiscBuilder<TChild> => AddClasslessChildCore<TChild>(childHandle, null, null);

    public ClassfulBuilder<THandle, TQdisc> AddClasslessChild<TChild>(THandle childHandle, Action<TChild> configureChild)
        where TChild : ClasslessQdiscBuilder<TChild>, IClasslessQdiscBuilder<TChild> => AddClasslessChildCore(childHandle, null, configureChild);

    public ClassfulBuilder<THandle, TQdisc> AddClasslessChild<TChild>(THandle childHandle, Action<IFilterManager> configureFilters)
        where TChild : ClasslessQdiscBuilder<TChild>, IClasslessQdiscBuilder<TChild> => AddClasslessChildCore<TChild>(childHandle, configureFilters, null);

    public ClassfulBuilder<THandle, TQdisc> AddClasslessChild<TChild>(THandle childHandle, Action<IFilterManager> configureFilters, Action<TChild> configureChild)
        where TChild : ClasslessQdiscBuilder<TChild>, IClasslessQdiscBuilder<TChild> => AddClasslessChildCore(childHandle, configureFilters, configureChild);

    [SuppressMessage(RELIABILITY, CA2000_DISPOSE_OBJECT, Justification = JUSTIFY_CA2000_OWNERSHIP_TRANSFER_TO_PROXY)]
    private ClassfulBuilder<THandle, TQdisc> AddClasslessChildCore<TChild>(THandle childHandle, Action<IFilterManager>? configureFilters, Action<TChild>? configureChild)
        where TChild : ClasslessQdiscBuilder<TChild>, IClasslessQdiscBuilder<TChild>
    {
        TChild childBuilder = TChild.CreateBuilder(_context);
        if (configureChild is not null)
        {
            configureChild(childBuilder);
        }
        IFilterManager filters = _filterManagerFactory.CreateFilterManager(configureFilters);
        IClassifyingQdisc<THandle> child = childBuilder.Build(childHandle, filters);
        _children.Add(child);
        return this;
    }

    public ClassfulBuilder<THandle, TQdisc> AddClassfulChild<TChild>(THandle childHandle)
        where TChild : ClassfulQdiscBuilder<TChild>, IClassfulQdiscBuilder<TChild>
    {
        ClassfulBuilder<THandle, TChild> childBuilder = new(childHandle, _context);
        IClassfulQdisc<THandle> child = childBuilder.Build();
        _children.Add(child);
        return this;
    }

    public ClassfulBuilder<THandle, TQdisc> AddClassfulChild<TChild>(THandle childHandle, Action<TChild> configureChild)
        where TChild : CustomClassfulQdiscBuilder<THandle, TChild>, ICustomClassfulQdiscBuilder<THandle, TChild>
    {
        TChild childBuilder = TChild.CreateBuilder(childHandle, _context);
        configureChild(childBuilder);
        IClassfulQdisc<THandle> child = childBuilder.Build();
        _children.Add(child);
        return this;
    }

    public ClassfulBuilder<THandle, TQdisc> AddClassfulChild<TChild>(THandle childHandle, Action<ClassfulBuilder<THandle, TChild>> configureChild)
        where TChild : ClassfulQdiscBuilder<TChild>, IClassfulQdiscBuilder<TChild>
    {
        ClassfulBuilder<THandle, TChild> childBuilder = new(childHandle, _context);
        configureChild(childBuilder);
        IClassfulQdisc<THandle> child = childBuilder.Build();
        _children.Add(child);
        return this;
    }

    public ClassfulBuilder<THandle, TQdisc> ConfigureFilters(Action<IFilterManager> classifier)
    {
        _filters ??= new FilterManager();
        classifier(_filters);
        return this;
    }

    public ClassfulBuilder<THandle, TQdisc> ConfigureQdisc(Action<TQdisc> configureQdisc)
    {
        if (_qdiscBuilder is not null)
        {
            throw new WorkloadSchedulingException("Qdisc has already been configured.");
        }

        _qdiscBuilder = TQdisc.CreateBuilder(_context);
        configureQdisc(_qdiscBuilder);
        return this;
    }

    internal IClassfulQdisc<THandle> Build()
    {
        _qdiscBuilder ??= TQdisc.CreateBuilder(_context);
        MatchAllFilter.Instance.ApplyIfUninitialized(ref _filters);
        IClassfulQdisc<THandle> qdisc = _qdiscBuilder.Build(_handle, _filters);
        foreach (IClassifyingQdisc<THandle> child in _children)
        {
            qdisc.TryAddChild(child);
        }
        return qdisc;
    }
}