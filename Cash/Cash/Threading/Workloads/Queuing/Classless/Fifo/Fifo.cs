using Cash.Threading.Workloads.Configuration;
using Cash.Threading.Workloads.Configuration.Classless;
using Cash.Threading.Workloads.Queuing.Classification;

namespace Cash.Threading.Workloads.Queuing.Classless.Fifo;

/// <summary>
/// A qdisc that implements the First-In-First-Out (FIFO) scheduling algorithm.
/// </summary>
public sealed class Fifo : ClasslessQdiscBuilder<Fifo>, IClasslessQdiscBuilder<Fifo>
{
    public static Fifo CreateBuilder(IQdiscBuilderContext context) => new();

    protected override IClassifyingQdisc<THandle> BuildInternal<THandle>(THandle handle, IFilterManager filters) => 
        new FifoQdisc<THandle>(handle, filters);
}
