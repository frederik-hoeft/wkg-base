using Cash.Diagnostic;
using Cash.Threading.Workloads.DependencyInjection;
using Cash.Threading.Workloads.Queuing;
using System.Diagnostics;

namespace Cash.Threading.Workloads.Scheduling;

using CommonFlags = WorkloadStatus.CommonFlags;

internal class WorkloadSchedulerWithDI(IQdisc rootQdisc, int maximumConcurrencyLevel, IWorkloadServiceProviderFactory serviceProviderFactory) 
    : WorkloadScheduler(rootQdisc, maximumConcurrencyLevel)
{
    private readonly IWorkloadServiceProviderFactory _serviceProviderFactory = serviceProviderFactory;

    internal protected async override void WorkerLoop(object? state)
    {
        int workerId = (int)state!;
        DebugLog.WriteInfo($"Started worker {workerId}");

        using IWorkloadServiceProvider serviceProvider = _serviceProviderFactory.GetInstance();
        bool previousExecutionFailed = false;
        // check for disposal before and after each dequeue (volatile read)
        AbstractWorkloadBase? workload = null;
        int previousWorkerId = workerId;
        while (!_disposed && TryDequeueOrExitSafely(ref workerId, previousExecutionFailed, out workload) && !_disposed)
        {
            previousWorkerId = workerId;
            workload.RegisterServiceProvider(serviceProvider);
            bool successfulExecution = workload switch
            {
                AsyncWorkload asyncWorkload => await asyncWorkload.TryRunAsynchronously().ConfigureAwait(continueOnCapturedContext: false),
                _ => workload.TryRunSynchronously(),
            };
            previousExecutionFailed = !successfulExecution;
            Debug.Assert(workload.Status.IsOneOf(CommonFlags.Completed));
            workload.InternalRunContinuations(workerId);
        }
        OnWorkerTerminated(ref workerId, previousWorkerId, workload);
    }
}
