namespace Cash.Threading.Workloads.Queuing.Classful.Classification;

internal interface IPredicate
{
    bool Invoke(object? state);
}
