using Cash.Threading.Workloads.Configuration;
using Cash.Threading.Workloads.Configuration.Classless;
using Cash.Threading.Workloads.Queuing.Classification;

namespace Cash.Threading.Workloads.Queuing.Classless.WorkStealing;

/// <summary>
/// A qdisc that uses work stealing to optimize for throughput.
/// </summary>
public sealed class WorkStealing : ClasslessQdiscBuilder<WorkStealing>, IClasslessQdiscBuilder<WorkStealing>
{
    public static WorkStealing CreateBuilder(IQdiscBuilderContext context) => new();

    protected override IClassifyingQdisc<THandle> BuildInternal<THandle>(THandle handle, IFilterManager filters) => 
        new WorkStealingQdisc<THandle>(handle, filters);
}
