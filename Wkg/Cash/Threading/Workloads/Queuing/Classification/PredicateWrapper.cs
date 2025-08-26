namespace Cash.Threading.Workloads.Queuing.Classification;

internal sealed class PredicateWrapper<TState>(Predicate<TState> _predicate) : IFilter
{
    public bool Match(object? state) => state is TState s && _predicate(s);
}