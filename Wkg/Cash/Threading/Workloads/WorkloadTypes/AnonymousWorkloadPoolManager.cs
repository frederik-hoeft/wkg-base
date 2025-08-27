using Cash.Data.Pooling;
using Cash.Diagnostic;
using Cash.Threading.Workloads.DependencyInjection;

namespace Cash.Threading.Workloads.WorkloadTypes;

internal sealed class AnonymousWorkloadPoolManager(int _capacity)
{
    private readonly Lock _lock = new();
    private ObjectPool<AnonymousWorkloadImpl>? _pool;
    private ObjectPool<AnonymousWorkloadImplWithDI>? _poolWithDI;

    private ObjectPool<AnonymousWorkloadImpl> Pool
    {
        get
        {
            ObjectPool<AnonymousWorkloadImpl>? pool = Volatile.Read(ref _pool);
            if (pool is null)
            {
                lock (_lock)
                {
                    pool = Volatile.Read(ref _pool);
                    if (pool is null)
                    {
                        DebugLog.WriteDiagnostic("Creating new anonymous workload pool.");
                        pool = new ObjectPool<AnonymousWorkloadImpl>(_capacity);
                        Volatile.Write(ref _pool, pool);
                    }
                }
            }
            return pool;
        }
    }

    private ObjectPool<AnonymousWorkloadImplWithDI> PoolWithDI
    {
        get
        {
            ObjectPool<AnonymousWorkloadImplWithDI>? pool = Volatile.Read(ref _poolWithDI);
            if (pool is null)
            {
                lock (_lock)
                {
                    pool = Volatile.Read(ref _poolWithDI);
                    if (pool is null)
                    {
                        DebugLog.WriteDiagnostic("Creating new anonymous workload pool with DI.");
                        pool = new ObjectPool<AnonymousWorkloadImplWithDI>(_capacity);
                        Volatile.Write(ref _poolWithDI, pool);
                    }
                }
            }
            return pool;
        }
    }

    public AnonymousWorkloadImpl Rent(Action action)
    {
        AnonymousWorkloadImpl workload = Pool.Rent();
        workload.Initialize(action);
        return workload;
    }

    public AnonymousWorkloadImplWithDI Rent(Action<IWorkloadServiceProvider> action)
    {
        AnonymousWorkloadImplWithDI workload = PoolWithDI.Rent();
        workload.Initialize(action);
        return workload;
    }
}
