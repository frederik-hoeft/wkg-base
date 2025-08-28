using BenchmarkDotNet.Attributes;
using System.Runtime.CompilerServices;

namespace Playground;

[RPlotExporter]
[MaxRelativeError(0.01)]
[CsvMeasurementsExporter]
public class ValueStore
{
    private readonly WorkerCwt _workerCwt = new();
    private readonly WorkerDictByObject _workerDictByObject = new();
    private readonly WorkerDictByHash _workerDictByHash = new();

    private readonly Cwt _cwt = new();
    private readonly DictByObject _dictByObject = new();
    private readonly DictByHash _dictByHash = new();

    [Params(1, 10, 100, 1000, 10000, 100000)]
    public int Iterations;

    [Benchmark]
    public int Cwt()
    {
        int iterations = Iterations;
        Cwt cwt = _cwt;
        WorkerCwt worker = _workerCwt;
        for (int i = 0; i < iterations; i++)
        {
            cwt.Test(worker, 1);
        }
        return cwt.Get(worker);
    }

    [Benchmark]
    public int DictByObject()
    {
        int iterations = Iterations;
        DictByObject dictByObject = _dictByObject;
        WorkerDictByObject worker = _workerDictByObject;
        for (int i = 0; i < iterations; i++)
        {
            dictByObject.Test(worker, 1);
        }
        return dictByObject.Get(worker);
    }

    [Benchmark]
    public int DictByHash()
    {
        int iterations = Iterations;
        DictByHash dictByHash = _dictByHash;
        WorkerDictByHash worker = _workerDictByHash;
        for (int i = 0; i < iterations; i++)
        {
            dictByHash.Test(worker, 1);
        }
        return dictByHash.Get(worker);
    }
}

internal sealed class WorkerCwt;

internal sealed class WorkerDictByObject
{
    public Dictionary<object, object> Data { get; } = [];
}

internal sealed class WorkerDictByHash
{
    public Dictionary<int, object> Data { get; } = [];
}

internal sealed class Box
{
    public int Value { get; set; }
}

internal sealed class Cwt
{
    private static readonly ConditionalWeakTable<WorkerCwt, Box> s_cwt = [];

    public void Test(WorkerCwt worker, int value)
    {
        if (s_cwt.TryGetValue(worker, out Box? box))
        {
            box.Value += value;
        }
        else
        {
            s_cwt.Add(worker, new Box() { Value = value });
        }
    }

    public int Get(WorkerCwt worker) => s_cwt.TryGetValue(worker, out Box? box) ? box.Value : 0;
}

internal sealed class DictByObject
{
    public void Test(WorkerDictByObject worker, int value)
    {
        if (worker.Data.TryGetValue(this, out object? obj) && obj is Box box)
        {
            box.Value += value;
        }
        else
        {
            worker.Data[this] = new Box() { Value = value };
        }
    }

    public int Get(WorkerDictByObject worker) => worker.Data.TryGetValue(this, out object? obj) && obj is Box box ? box.Value : 0;
}

internal sealed class DictByHash
{
    private readonly int _hash;

    public DictByHash() => _hash = RuntimeHelpers.GetHashCode(this);

    public void Test(WorkerDictByHash worker, int value)
    {
        if (worker.Data.TryGetValue(_hash, out object? obj) && obj is Box box)
        {
            box.Value += value;
        }
        else
        {
            worker.Data[_hash] = new Box() { Value = value };
        }
    }

    public int Get(WorkerDictByHash worker) => worker.Data.TryGetValue(_hash, out object? obj) && obj is Box box ? box.Value : 0;
}