using Cash.Threading.Workloads.Configuration;
using Cash.Threading.Workloads.Configuration.Classless;

namespace Cash.Threading.Workloads.Queuing.Classless.LatestOnly;

/// <summary>
/// A qdisc that implements the Latest-Only scheduling algorithm. This means that new workloads will replace existing ones and the replaced workload will be cancelled.
/// </summary>
public class LatestOnly : ClasslessQdiscBuilder<LatestOnly>, IClasslessQdiscBuilder<LatestOnly>
{
    private LatestOnly() => Pass();

    public static LatestOnly CreateBuilder(IQdiscBuilderContext context) => new();

    protected override IClassifyingQdisc<THandle> BuildInternal<THandle>(THandle handle, Predicate<object?>? predicate) => 
        new LatestOnlyQdisc<THandle>(handle, predicate);
}
