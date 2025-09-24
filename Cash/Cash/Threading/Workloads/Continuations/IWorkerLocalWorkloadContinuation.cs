using Cash.Threading.Workloads.Scheduling;

namespace Cash.Threading.Workloads.Continuations;

internal interface IWorkerLocalWorkloadContinuation
{
    void Invoke(AbstractWorkloadBase workload, WorkerContext? worker);
}
