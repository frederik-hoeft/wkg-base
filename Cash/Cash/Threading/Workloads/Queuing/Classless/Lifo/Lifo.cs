using Cash.Threading.Workloads.Configuration;
using Cash.Threading.Workloads.Configuration.Classless;
using Cash.Threading.Workloads.Queuing.Classification;

namespace Cash.Threading.Workloads.Queuing.Classless.Lifo;

/// <summary>
/// A qdisc that implements the Last-In-First-Out (LIFO) scheduling algorithm.
/// </summary>
public sealed class Lifo : ClasslessQdiscBuilder<Lifo>, IClasslessQdiscBuilder<Lifo>
{
    public static Lifo CreateBuilder(IQdiscBuilderContext context) => new();

    protected override IClassifyingQdisc<THandle> BuildInternal<THandle>(THandle handle, IFilterManager filters) => 
        new LifoQdisc<THandle>(handle, filters);
}
