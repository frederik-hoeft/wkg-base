namespace Cash.Threading.Workloads.Continuations;

internal interface IWorkerLocalWorkloadContinuation
{
    void Invoke(AbstractWorkloadBase workload, int workerId);
}
