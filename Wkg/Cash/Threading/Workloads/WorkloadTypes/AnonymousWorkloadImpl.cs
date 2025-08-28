using Cash.Data.Pooling;
using Cash.Threading.Workloads.Scheduling;

namespace Cash.Threading.Workloads.WorkloadTypes;

internal sealed class AnonymousWorkloadImpl : AnonymousWorkload, IPoolable<AnonymousWorkloadImpl>
{
    private readonly IPool<AnonymousWorkloadImpl>? _pool;
    private Action _action;

    private AnonymousWorkloadImpl(IPool<AnonymousWorkloadImpl> pool) : this(WorkloadStatus.Created, null!)
    {
        _pool = pool;
    }

    internal AnonymousWorkloadImpl(Action action) : this(WorkloadStatus.Created, action) => Pass();

    internal AnonymousWorkloadImpl(WorkloadStatus status, Action action) : base(status)
    {
        _action = action;
    }

    public static AnonymousWorkloadImpl Create(IPool<AnonymousWorkloadImpl> pool) =>
        new(pool);

    private protected override void ExecuteCore() => _action();

    internal override void InternalRunContinuations(WorkerContext? worker)
    {
        base.InternalRunContinuations(worker);

        if (_pool is not null)
        {
            Volatile.Write(ref _action, null!);
            Volatile.Write(ref _status, WorkloadStatus.Pooled);
            // this is usually very risky, but we should be the only ones with a reference to this workload
            // so we can safely do this. otherwise, this would be illegal as it would violate the allowed
            // state transitions of the workload continuations.
            Volatile.Write(ref _continuation, null);
            _pool.Return(this);
        }
    }

    internal void Initialize(Action action)
    {
        Volatile.Write(ref _action, action);
        Volatile.Write(ref _status, WorkloadStatus.Created);
    }

    internal override nint GetPayloadFunctionPointer() => _action.Method.MethodHandle.GetFunctionPointer();
}
