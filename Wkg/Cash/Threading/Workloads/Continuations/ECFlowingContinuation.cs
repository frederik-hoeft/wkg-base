namespace Cash.Threading.Workloads.Continuations;

internal sealed class ECFlowingContinuation(IWorkloadContinuation innerContinuation, bool flowExecutionContext) 
    : ECContinuationBase(innerContinuation, flowExecutionContext)
{
    protected override void PostContinuation(Action<object?> callback, object? state) => 
        ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(callback), state);
}
