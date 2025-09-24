namespace Cash.Threading.Workloads;

internal sealed class WorkloadResultBox<TResult>(TResult result)
{
    public TResult Result { get; } = result;
}