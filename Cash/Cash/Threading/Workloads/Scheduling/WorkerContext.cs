using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Scheduling;

/// <summary>
/// Represents the context of a worker thread in a workload scheduler and provides worker-local storage.
/// When a worker thread is terminated, its associated context (and storage) is cleared and may be reused for a new worker thread.
/// </summary>
public sealed class WorkerContext
{
    private readonly Dictionary<int, object> _data = [];

    public int WorkerId { get; }

    internal WorkerContext(int id) => WorkerId = id;

    public bool TryGetData<T>(int key, [NotNullWhen(true)] out T? value)
    {
        if (_data.TryGetValue(key, out object? obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public bool TryGetData<TOwner, TData>([DisallowNull] TOwner owner, [NotNullWhen(true)] out TData? value)
        where TOwner : class, IWorkerDataOwner
    {
        ArgumentNullException.ThrowIfNull(owner);
        return TryGetData(owner.InstanceHash, out value);
    }

    public bool HasData(int key) => _data.ContainsKey(key);

    public bool HasData<TOwner>([DisallowNull] TOwner owner) where TOwner : class, IWorkerDataOwner
    {
        ArgumentNullException.ThrowIfNull(owner);
        return HasData(owner.InstanceHash);
    }

    public void SetData<T>(int key, [DisallowNull] T value) => _data[key] = value;

    public void SetData<TOwner, TData>([DisallowNull] TOwner owner, [DisallowNull] TData value) where TOwner : class, IWorkerDataOwner
    {
        ArgumentNullException.ThrowIfNull(owner);
        SetData(owner.InstanceHash, value);
    }

    internal void Clear() => _data.Clear();

    public override string ToString() => WorkerId.ToString();
}