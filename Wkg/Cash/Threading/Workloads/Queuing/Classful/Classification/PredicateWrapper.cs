namespace Cash.Threading.Workloads.Queuing.Classful.Classification;

internal sealed class PredicateWrapper<TState>(Predicate<TState> _predicate) : IPredicate
{
    public bool Invoke(object? state) => state is TState s && _predicate(s);
}