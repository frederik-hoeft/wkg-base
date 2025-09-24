using Cash.Threading.Extensions;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Queuing.Classification;

public interface IFilterCollection : IFilter, IEnumerable<IFilter>, IDisposable
{
    void AddFilter(IFilter filter);

    bool TryAddFilter(IFilter filter);

    bool TryGetFilter(int id, [NotNullWhen(true)] out IFilter? filter);

    bool TryRemoveFilter(int id);

    int Count { get; }
}

public interface IFilterManager : IFilterCollection;

public sealed class FilterManager() : FilterCollection(id: 0, priority: 0), IFilterManager
{
    public static FilterManager MatchNothing() => new();
}

public class FilterCollection : IFilterCollection
{
    private static readonly Comparer<IFilter> s_filterPriorityComparer = Comparer<IFilter>.Create(static (x, y) => x.Priority.CompareTo(y.Priority));
    private readonly List<IFilter> _filters;
    private readonly ConcurrentDictionary<int, IFilter> _filterIndex;
    private readonly ReaderWriterLockSlim _lock;
    private bool _disposedValue;
    private int _count;

    public int Id { get; }

    public int Priority { get; }

    public FilterCollection(int id, int priority)
    {
        Id = id;
        Priority = priority;
        _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        _filters = [];
        _filterIndex = [];
        _filterIndex[id] = this;
    }

    public int Count => Volatile.Read(ref _count);

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
        Interlocked.Increment(ref _count);
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
        if (id == Id || !_filterIndex.ContainsKey(id))
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
            Interlocked.Decrement(ref _count);
        }
        else
        {
            Debug.Fail("Filter index not found in list.");
        }
        return true;
    }

    public bool TryGetFilter(int id, [NotNullWhen(true)] out IFilter? filter)
    {
        if (_filterIndex.TryGetValue(id, out filter))
        {
            return true;
        }
        foreach (IFilter child in this)
        {
            if (child is IFilterCollection collection && collection.TryGetFilter(id, out filter))
            {
                return true;
            }
        }
        filter = null;
        return false;
    }

    public bool Match(object? state)
    {
        if (Count == 0)
        {
            return false;
        }
        foreach (IFilter filter in this)
        {
            if (filter.Match(state))
            {
                return true;
            }
        }
        return false;
    }

    public IEnumerator<IFilter> GetEnumerator()
    {
        using ILockOwnership readLock = _lock.AcquireReadLock();
        foreach (IFilter filter in _filters)
        {
            yield return filter;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => 
        $"filter_collection(Id: {Id}, Count: {Count}, Filters: [{string.Join(", ", this.Select(static filter => filter.ToString()))}]";

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _lock.Dispose();
                _filters.Clear();
                _filterIndex.Clear();
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

public static partial class FilterManagerExtensions
{
    public static void ApplyIfUninitialized(this MatchAllFilter matchAll, [NotNull] ref IFilterManager? manager)
    {
        ArgumentNullException.ThrowIfNull(matchAll);
        if (manager is null)
        {
            manager = new FilterManager();
            manager.AddFilter(matchAll);
        }
    }

    public static IFilterCollection AddMatchAll(this IFilterCollection manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        manager.AddFilter(MatchAllFilter.Instance);
        return manager;
    }

    public static IFilterCollection AddFilter(this IFilterCollection manager, int id, int priority, Predicate<object?> filter)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(filter);
        manager.AddFilter(new AnonymousFilter(id, priority, filter));
        return manager;
    }

    public static IFilterCollection AddFilter(this IFilterCollection manager, int id, Predicate<object?> filter) =>
        AddFilter(manager, id, int.MaxValue, filter);

    public static IFilterCollection AddNamedFilter(this IFilterCollection manager, string name, int priority, Predicate<object?> filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddFilter(manager, name.GetHashCode(StringComparison.OrdinalIgnoreCase), priority, filter);
        return manager;
    }

    public static IFilterCollection AddNamedFilter(this IFilterCollection manager, string name, Predicate<object?> filter) =>
        AddNamedFilter(manager, name, int.MaxValue, filter);

    public static IFilterCollection AddFilter<TState>(this IFilterCollection manager, int id, int priority, Predicate<TState> filter)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(filter);
        manager.AddFilter(new AnonymousFilter<TState>(id, priority, filter));
        return manager;
    }

    public static IFilterCollection AddNamedFilter<TState>(this IFilterCollection manager, string name, int priority, Predicate<TState> filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddFilter(manager, name.GetHashCode(StringComparison.OrdinalIgnoreCase), priority, filter);
        return manager;
    }

    public static IFilterCollection AddNamedFilter<TState>(this IFilterCollection manager, string name, Predicate<TState> filter) =>
        AddNamedFilter(manager, name, int.MaxValue, filter);

    public static IFilterCollection AddTypeFilter<TState>(this IFilterCollection manager, int id, int priority)
    {
        ArgumentNullException.ThrowIfNull(manager);
        manager.AddFilter(new TypeFilter<TState>(id, priority));
        return manager;
    }

    public static IFilterCollection AddTypeFilter<TState>(this IFilterCollection manager, int id) =>
        AddTypeFilter<TState>(manager, id, int.MaxValue);

    public static IFilterCollection AddNamedTypeFilter<TState>(this IFilterCollection manager, string name, int priority)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return AddTypeFilter<TState>(manager, name.GetHashCode(StringComparison.OrdinalIgnoreCase), priority);
    }

    public static IFilterCollection AddNamedTypeFilter<TState>(this IFilterCollection manager, string name) =>
        AddNamedTypeFilter<TState>(manager, name, int.MaxValue);

    public static bool TryAddMatchAll(this IFilterCollection manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return manager.TryAddFilter(MatchAllFilter.Instance);
    }

    public static bool TryAddFilter(this IFilterCollection manager, int id, int priority, Predicate<object?> filter)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(filter);
        return manager.TryAddFilter(new AnonymousFilter(id, priority, filter));
    }

    public static bool TryAddFilter(this IFilterCollection manager, int id, Predicate<object?> filter) =>
        TryAddFilter(manager, id, int.MaxValue, filter);

    public static bool TryAddNamedFilter(this IFilterCollection manager, string name, int priority, Predicate<object?> filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return TryAddFilter(manager, name.GetHashCode(StringComparison.OrdinalIgnoreCase), priority, filter);
    }

    public static bool TryAddNamedFilter(this IFilterCollection manager, string name, Predicate<object?> filter) =>
        TryAddNamedFilter(manager, name, int.MaxValue, filter);

    public static bool TryAddFilter<TState>(this IFilterCollection manager, int id, int priority, Predicate<TState> filter)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(filter);
        return manager.TryAddFilter(new AnonymousFilter<TState>(id, priority, filter));
    }

    public static bool TryAddFilter<TState>(this IFilterCollection manager, int id, Predicate<TState> filter) =>
        TryAddFilter(manager, id, int.MaxValue, filter);

    public static bool TryAddNamedFilter<TState>(this IFilterCollection manager, string name, int priority, Predicate<TState> filter)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return TryAddFilter(manager, name.GetHashCode(StringComparison.OrdinalIgnoreCase), priority, filter);
    }

    public static bool TryAddNamedFilter<TState>(this IFilterCollection manager, string name, Predicate<TState> filter) =>
        TryAddNamedFilter(manager, name, int.MaxValue, filter);

    public static bool TryGetNamedFilter(this IFilterCollection manager, string name, [NotNullWhen(true)] out IFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentException.ThrowIfNullOrEmpty(name);
        return manager.TryGetFilter(name.GetHashCode(StringComparison.OrdinalIgnoreCase), out filter);
    }

    public static bool TryRemoveNamedFilter(this IFilterCollection manager, string name)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentException.ThrowIfNullOrEmpty(name);
        return manager.TryRemoveFilter(name.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }
}

public interface IFilter
{
    int Id { get; }

    int Priority { get; }

    bool Match(object? state);

    string ToString();
}

public sealed class MatchAllFilter : IFilter
{
    public const string NAME = "MatchAll";

    private MatchAllFilter(int id, int priority)
    {
        Id = id;
        Priority = priority;
    }

    public int Id { get; }

    public int Priority { get; }

    public bool Match(object? state) => true;

    internal static MatchAllFilter Instance { get; } = new(id: NAME.GetHashCode(StringComparison.OrdinalIgnoreCase), priority: int.MinValue);

    public override string ToString() => $"match_all({nameof(Id)}: {Id})";
}

public abstract class FilterBase(int id, int priority) : IFilter
{
    public int Id => id;

    public int Priority => priority;

    public abstract bool Match(object? state);

    public abstract override string ToString();
}

internal sealed class AnonymousFilter(int id, int priority, Predicate<object?> filter) : FilterBase(id, priority)
{
    public override bool Match(object? state) => filter(state);

    public override string ToString() => $"lambda({nameof(Id)}: {Id})";
}

internal sealed class AnonymousFilter<TState>(int id, int priority, Predicate<TState> filter) : FilterBase(id, priority)
{
    public override bool Match(object? state) => state is TState typedState && filter(typedState);

    public override string ToString() => $"typed_lambda<{typeof(TState).Name}>({nameof(Id)}: {Id})";
}

internal sealed class TypeFilter<TState>(int id, int priority) : FilterBase(id, priority)
{
    public override bool Match(object? state) => state is TState;

    public override string ToString() => $"type_filter<{typeof(TState).Name}>({nameof(Id)}: {Id})";
}