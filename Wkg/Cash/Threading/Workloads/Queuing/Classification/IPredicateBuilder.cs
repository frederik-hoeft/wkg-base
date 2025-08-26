namespace Cash.Threading.Workloads.Queuing.Classification;

public interface IPredicateBuilder
{
    Predicate<object?>? Compile();
}
