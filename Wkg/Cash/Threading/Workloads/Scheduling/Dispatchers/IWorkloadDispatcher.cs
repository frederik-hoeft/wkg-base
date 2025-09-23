namespace Cash.Threading.Workloads.Scheduling.Dispatchers;

public interface IWorkloadDispatcher : IDisposable
{
    internal protected void OnWorkScheduled();

    internal protected void CriticalNotifyCallerIsWaiting();
}