using Cash.Threading.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Cash.Threading.Workloads.Queuing.Classification;

public interface IFilterManager : IFilter
{
    void AddFilter(IFilter filter);

    bool TryAddFilter(IFilter filter);

    bool TryRemoveFilter(int id);
}

public class FilterManager : IFilterManager, IDisposable
{
    private static readonly Comparer<IFilter> s_filterPriorityComparer = Comparer<IFilter>.Create(static (x, y) => x.Priority.CompareTo(y.Priority));
    private readonly List<IFilter> _filters = [];
    private readonly ConcurrentDictionary<int, IFilter> _filterIndex = [];
    private readonly ReaderWriterLockSlim _lock = new();
    private bool _disposedValue;

    public int Id => 0;

    public int Priority => 0;

    public void AddFilter(IFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (!TryAddFilter(filter))
        {
            throw new ArgumentException("A filter with the same ID already exists.", nameof(filter));
        }
    }

    public bool TryAddFilter(IFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (_filterIndex.ContainsKey(filter.Id))
        {
            return false;
        }
        using ILockOwnership writeLock = _lock.AcquireWriteLock();
        if (!_filterIndex.TryAdd(filter.Id, filter))
        {
            return false;
        }
        if (filter.Priority == int.MaxValue)
        {
            _filters.Add(filter);
            return true;
        }
        for (int i = 0; i < _filters.Count; i++)
        {
            if (filter.Priority < _filters[i].Priority)
            {
                _filters.Insert(i, filter);
                return true;
            }
        }
        _filters.Add(filter);
        return true;
    }

    public bool TryRemoveFilter(int id)
    {
        if (!_filterIndex.ContainsKey(id))
        {
            return false;
        }
        using ILockOwnership writeLock = _lock.AcquireWriteLock();
        if (!_filterIndex.TryRemove(id, out IFilter? filter))
        {
            return false;
        }
        int index = _filters.BinarySearch(filter, s_filterPriorityComparer);
        if (index >= 0)
        {
            _filters.RemoveAt(index);
        }
        else
        {
            Debug.Fail("Filter index not found in list.");
        }
        return true;
    }

    public bool Match(object? state)
    {
        using ILockOwnership readLock = _lock.AcquireReadLock();
        foreach (IFilter filter in _filters)
        {
            if (filter.Match(state))
            {
                return true;
            }
        }
        return false;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _lock.Dispose();
                _filters.Clear();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public static class FilterManagerExtensions
{
    public static void AddFilter(this IFilterManager manager, int id, int priority, Predicate<object?> filter)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(filter);
        manager.AddFilter(new AnonymousFilter(id, priority, filter));
    }

    public static void AddFilter(this IFilterManager manager, int id, Predicate<object?> filter) =>
        AddFilter(manager, id, int.MaxValue, filter);

    public static void AddNamedFilter(this IFilterManager manager, string name, int priority, Predicate<object?> filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddFilter(manager, name.GetHashCode(StringComparison.Ordinal), priority, filter);
    }

    public static void AddNamedFilter(this IFilterManager manager, string name, Predicate<object?> filter) =>
        AddNamedFilter(manager, name, int.MaxValue, filter);

    public static void AddFilter<TState>(this IFilterManager manager, int id, int priority, Predicate<TState> filter)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(filter);
        manager.AddFilter(new AnonymousFilter<TState>(id, priority, filter));
    }

    public static void AddNamedFilter<TState>(this IFilterManager manager, string name, int priority, Predicate<TState> filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddFilter(manager, name.GetHashCode(StringComparison.Ordinal), priority, filter);
    }

    public static void AddNamedFilter<TState>(this IFilterManager manager, string name, Predicate<TState> filter) =>
        AddNamedFilter(manager, name, int.MaxValue, filter);

    public static bool TryAddFilter(this IFilterManager manager, int id, int priority, Predicate<object?> filter)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(filter);
        return manager.TryAddFilter(new AnonymousFilter(id, priority, filter));
    }

    public static bool TryAddFilter(this IFilterManager manager, int id, Predicate<object?> filter) =>
        TryAddFilter(manager, id, int.MaxValue, filter);

    public static bool TryAddNamedFilter(this IFilterManager manager, string name, int priority, Predicate<object?> filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return TryAddFilter(manager, name.GetHashCode(StringComparison.Ordinal), priority, filter);
    }

    public static bool TryAddNamedFilter(this IFilterManager manager, string name, Predicate<object?> filter) =>
        TryAddNamedFilter(manager, name, int.MaxValue, filter);

    public static bool TryAddFilter<TState>(this IFilterManager manager, int id, int priority, Predicate<TState> filter)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(filter);
        return manager.TryAddFilter(new AnonymousFilter<TState>(id, priority, filter));
    }

    public static bool TryAddFilter<TState>(this IFilterManager manager, int id, Predicate<TState> filter) =>
        TryAddFilter(manager, id, int.MaxValue, filter);

    public static bool TryAddNamedFilter<TState>(this IFilterManager manager, string name, int priority, Predicate<TState> filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return TryAddFilter(manager, name.GetHashCode(StringComparison.Ordinal), priority, filter);
    }

    public static bool TryAddNamedFilter<TState>(this IFilterManager manager, string name, Predicate<TState> filter) =>
        TryAddNamedFilter(manager, name, int.MaxValue, filter);

    public static bool TryRemoveNamedFilter(this IFilterManager manager, string name)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentException.ThrowIfNullOrEmpty(name);
        return manager.TryRemoveFilter(name.GetHashCode(StringComparison.Ordinal));
    }
}

public interface IFilter
{
    int Id { get; }

    int Priority { get; }

    bool Match(object? state);
}

public abstract class FilterBase(int id, int priority) : IFilter
{
    public int Id => id;

    public int Priority => priority;

    public abstract bool Match(object? state);
}

internal sealed class AnonymousFilter(int id, int priority, Predicate<object?> filter) : FilterBase(id, priority)
{
    public override bool Match(object? state) => filter(state);
}

internal sealed class AnonymousFilter<TState>(int id, int priority, Predicate<TState> filter) : FilterBase(id, priority)
{
    public override bool Match(object? state) => state is TState typedState && filter(typedState);
}