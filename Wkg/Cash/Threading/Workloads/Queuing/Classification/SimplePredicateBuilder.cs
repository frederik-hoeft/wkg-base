namespace Cash.Threading.Workloads.Queuing.Classification;

public class SimplePredicateBuilder : IPredicateBuilder
{
    private readonly List<IFilter> _predicates = [];

    public SimplePredicateBuilder AddPredicate<TState>(Predicate<TState> predicate)
    {
        _predicates.Add(new PredicateWrapper<TState>(predicate));
        return this;
    }

    public Predicate<object?>? Compile() => _predicates.Count switch
    {
        0 => null,
        1 => new Predicate<object?>(_predicates[0].Match),
        _ => new Predicate<object?>(new CompiledPredicates(this).Match),
    };

    private sealed class CompiledPredicates(SimplePredicateBuilder predicates) : IFilter
    {
        private readonly IFilter[] _predicates = [.. predicates._predicates];

        public bool Match(object? state)
        {
            for (int i = 0; i < _predicates.Length; i++)
            {
                if (_predicates[i].Match(state))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

