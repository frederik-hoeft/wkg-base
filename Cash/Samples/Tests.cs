using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters.Csv;
using Cash.Data.Pooling;
using Cash.Threading.Workloads;
using Cash.Threading.Workloads.Configuration;
using Cash.Threading.Workloads.Configuration.Dispatcher;
using Cash.Threading.Workloads.Factories;
using Cash.Threading.Workloads.Queuing.Classful.PrioFast;
using Cash.Threading.Workloads.Queuing.Classless.Fifo;

namespace Samples;

[RPlotExporter]
[MaxRelativeError(0.01)]
[CsvMeasurementsExporter]
public class Tests
{
    //[Params(1000)]
    public int WorkloadCount => 1000;

    [Params(2, 4, 8, 16)]
    public int Concurrency;
    private const int MAX_CONCURRENCY = 16;

    [Params(1, 2, 3, 4, 5, 6, 7)]
    public int Depth;
    private const int MAX_DEPTH = 7;

    //[Params(4)]
    public int BranchingFactor => 4;

    //[Params(100000)]
    public int Spins => 16384;

    private static readonly Random s_bitmapRandom = new(42);
    private static readonly ManualResetEventSlim s_bitmapMres = new(false);

    private static readonly Random s_lockingBitmapRandom = new(42);
    private static readonly ManualResetEventSlim s_lockingBitmapMres = new(false);

    private ClassfulWorkloadFactory<int>[,] _bitmaps = null!;

    private ClassfulWorkloadFactory<int>[,] _lockingBitmaps = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _bitmaps = new ClassfulWorkloadFactory<int>[MAX_CONCURRENCY, MAX_DEPTH];
        _lockingBitmaps = new ClassfulWorkloadFactory<int>[MAX_CONCURRENCY, MAX_DEPTH];
        for (int concurrency = 0; concurrency < MAX_CONCURRENCY; concurrency++)
        {
            for (int depth = 0; depth < MAX_DEPTH; depth++)
            {
                // concurrency and depth are 1-based (obviously), so adjust array indices accordingly
                _bitmaps[concurrency, depth] = CreateBitmapFactory(concurrency + 1, depth + 1, BranchingFactor);
                _lockingBitmaps[concurrency, depth] = CreateLockingBitmapFactory(concurrency + 1, depth + 1, BranchingFactor);
            }
        }
    }

    public static ClassfulWorkloadFactory<int> CreateBitmapFactory(int concurrency, int depth, int branchingFactor)
    {
        HandleCounter handleCounter = new(2);
        return WorkloadFactoryBuilder.Create<int>()
            .UseWorkloadDispatcher<BoundedWorkloadDispatcherFactory>(dispatcher => dispatcher
                .UseMaximumConcurrency(concurrency))
            .UseClassfulRoot<PrioFast<int>>(1, root => ConfigureBitmapLevel(root, depth - 1, branchingFactor, handleCounter));
    }

    private static void ConfigureBitmapLevel(PrioFast<int> builder, int remainingDepth, int branchingFactor, HandleCounter nextHandle)
    {
        builder.OptimizeForHighContention(true);
        if (remainingDepth == 0)
        {
            for (int i = 0; i < branchingFactor; i++, nextHandle.Handle++)
            {
                builder.AddClasslessChild<Fifo>(nextHandle.Handle, i);
            }
        }
        else
        {
            for (int i = 0; i < branchingFactor; i++)
            {
                int handle = nextHandle.Handle;
                nextHandle.Handle++;
                builder.AddClassfulChild<PrioFast<int>>(handle, i, child => ConfigureBitmapLevel(child, remainingDepth - 1, branchingFactor, nextHandle));
            }
        }
    }

    public static ClassfulWorkloadFactory<int> CreateLockingBitmapFactory(int concurrency, int depth, int branchingFactor)
    {
        HandleCounter handleCounter = new(2);
        return WorkloadFactoryBuilder.Create<int>()
            .UseWorkloadDispatcher<BoundedWorkloadDispatcherFactory>(dispatcher => dispatcher
                .UseMaximumConcurrency(concurrency))
            .UseClassfulRoot<PrioFast<int>>(1, root => ConfigureLockingBitmapLevel(root, depth - 1, branchingFactor, handleCounter));
    }

    private static void ConfigureLockingBitmapLevel(PrioFast<int> builder, int remainingDepth, int branchingFactor, HandleCounter nextHandle)
    {
        builder.OptimizeForHighContention(false);
        if (remainingDepth == 0)
        {
            for (int i = 0; i < branchingFactor; i++, nextHandle.Handle++)
            {
                builder.AddClasslessChild<Fifo>(nextHandle.Handle, i);
            }
        }
        else
        {
            for (int i = 0; i < branchingFactor; i++)
            {
                int handle = nextHandle.Handle;
                nextHandle.Handle++;
                builder.AddClassfulChild<PrioFast<int>>(handle, i, child => ConfigureLockingBitmapLevel(child, remainingDepth - 1, branchingFactor, nextHandle));
            }
        }
    }

    public static int NodeCount(int depth, int branchingFactor) => (int)(Math.Pow(branchingFactor, depth + 1) - 1) / (branchingFactor - 1);

    [Benchmark]
    public async Task Bitmap()
    {
        PooledArray<AwaitableWorkload> workloads = ArrayPool.Rent<AwaitableWorkload>(WorkloadCount);
        ClassfulWorkloadFactory<int> bitmap = _bitmaps[Concurrency - 1, Depth - 1];
        s_bitmapMres.Reset();
        int totalNodes = NodeCount(Depth, BranchingFactor);
        for (int i = 0; i < Concurrency; i++)
        {
            bitmap.Schedule(s_bitmapMres.Wait);
        }
        for (int i = 0; i < workloads.Length; i++)
        {
            int handle = s_bitmapRandom.Next(1, totalNodes + 1);
            workloads.Array[i] = bitmap.ScheduleAsync(handle, Work);
        }
        s_bitmapMres.Set();
        await Workload.WhenAll(workloads.Array[..workloads.Length]);
        ArrayPool.Return(workloads);
    }

    [Benchmark]
    public async Task LockingBitmap()
    {
        PooledArray<AwaitableWorkload> workloads = ArrayPool.Rent<AwaitableWorkload>(WorkloadCount);
        ClassfulWorkloadFactory<int> lockingBitmap = _lockingBitmaps[Concurrency - 1, Depth - 1];
        int totalNodes = NodeCount(Depth, BranchingFactor);
        s_lockingBitmapMres.Reset();
        for (int i = 0; i < Concurrency; i++)
        {
            lockingBitmap.Schedule(s_lockingBitmapMres.Wait);
        }
        for (int i = 0; i < workloads.Length; i++)
        {
            int handle = s_lockingBitmapRandom.Next(1, totalNodes + 1);
            workloads.Array[i] = lockingBitmap.ScheduleAsync(handle, Work);
        }
        s_lockingBitmapMres.Set();
        await Workload.WhenAll(workloads.Array[..workloads.Length]);
        ArrayPool.Return(workloads);
    }

    private int Work(CancellationFlag flag) => ReliableSpinner.Spin(Spins);
}
