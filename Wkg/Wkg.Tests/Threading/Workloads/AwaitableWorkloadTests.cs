using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cash.Threading.Workloads.Factories;
using Cash.Threading.Workloads.Configuration;
using Cash.Threading.Workloads.Queuing.Classless.Fifo;
using System.Diagnostics;
using Cash.Threading.Workloads.Exceptions;
using Cash.Threading.Workloads;
using Cash.Threading.Workloads.Configuration.Dispatcher;
using Cash.Threading.Workloads.Scheduling.Dispatchers;

namespace Cash.Tests.Threading.Workloads;

[TestClass]
public sealed class AwaitableWorkloadTests
{
    private static ClasslessWorkloadFactory<int> CreateDefaultFactory() => WorkloadFactoryBuilder.Create<int>()
        .UseAnonymousWorkloadPooling(4)
        .UseWorkloadDispatcher<BoundedWorkloadDispatcherFactory>(static dispatcher => dispatcher.UseMaximumConcurrency(1))
        .UseClasslessRoot<Fifo>(1);

    [TestMethod]
    public async Task TestAwait1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload<int> workload = factory.ScheduleAsync(_ =>
        {
            Thread.Sleep(100);
            return 1;
        });

        WorkloadResult<int> result = await workload;
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Result);
    }

    [TestMethod]
    public async Task TestAwait2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload workload = factory.ScheduleAsync(_ => Thread.Sleep(100));

        WorkloadResult result = await workload;
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public void TestBlockingWaitImplicit1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload<int> workload = factory.ScheduleAsync(_ =>
        {
            Thread.Sleep(100);
            return 1;
        });
        WorkloadResult<int> result = workload.GetAwaiter().GetResult();
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Result);
    }

    [TestMethod]
    public void TestBlockingWaitImplicit2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload workload = factory.ScheduleAsync(_ => Thread.Sleep(100));

        WorkloadResult result = workload.GetAwaiter().GetResult();
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public void TestBlockingWaitImplicit3()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload<int> workload = factory.ScheduleAsync(_ =>
        {
            Thread.Sleep(100);
            return 1;
        });
        WorkloadResult<int> result = workload.Result;
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Result);
    }

    [TestMethod]
    public void TestBlockingWaitImplicit4()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload workload = factory.ScheduleAsync(_ => Thread.Sleep(100));

        WorkloadResult result = workload.Result;
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public void TestContinueWith1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload<int> workload = factory.ScheduleAsync(_ =>
        {
            Thread.Sleep(100);
            return 1;
        });
        using ManualResetEventSlim mres = new(false);
        workload.ContinueWith(result =>
        {
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Result);
            Assert.IsTrue(new StackTrace().GetFrames().All(frame => frame.GetMethod()?.Name != nameof(BoundedWorkloadDispatcher.WorkerLoop)));
            // ensure that the continuation is invoked inline
            Assert.IsTrue(new StackTrace().GetFrames().All(frame => frame.GetMethod()?.Name != nameof(TestContinueWith1)));
            mres.Set();
        });
        mres.Wait();
    }

    [TestMethod]
    public void TestContinueWith2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload workload = factory.ScheduleAsync(_ => Thread.Sleep(100));
        using ManualResetEventSlim mres = new(false);
        workload.ContinueWith(result =>
        {
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(new StackTrace().GetFrames().All(frame => frame.GetMethod()?.Name != nameof(BoundedWorkloadDispatcher.WorkerLoop)));
            // ensure that the continuation is invoked inline
            Assert.IsTrue(new StackTrace().GetFrames().All(frame => frame.GetMethod()?.Name != nameof(TestContinueWith2)));
            mres.Set();
        });
        mres.Wait();
    }

    [TestMethod]
    public void TestContinueWithInline1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload<int> workload = factory.ScheduleAsync(_ => 1);
        Thread.Sleep(100);
        using ManualResetEventSlim mres = new(false);
        workload.ContinueWith(result =>
        {
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Result);
            Assert.IsTrue(new StackTrace().GetFrames().All(frame => frame.GetMethod()?.Name != nameof(BoundedWorkloadDispatcher.WorkerLoop)));
            // ensure that the continuation is invoked inline
            Assert.IsTrue(new StackTrace().GetFrames().Any(frame => frame.GetMethod()?.Name == nameof(TestContinueWithInline1)));
            mres.Set();
        });
        mres.Wait();
    }

    [TestMethod]
    public void TestContinueWithInline2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload workload = factory.ScheduleAsync(Pass);
        Thread.Sleep(100);
        using ManualResetEventSlim mres = new(false);
        workload.ContinueWith(result =>
        {
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(new StackTrace().GetFrames().All(frame => frame.GetMethod()?.Name != nameof(BoundedWorkloadDispatcher.WorkerLoop)));
            // ensure that the continuation is invoked inline
            Assert.IsTrue(new StackTrace().GetFrames().Any(frame => frame.GetMethod()?.Name == nameof(TestContinueWithInline2)));
            mres.Set();
        });
        mres.Wait();
    }

    [TestMethod]
    public void TestBlockingWaitExplicit1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload<int> workload = factory.ScheduleAsync(_ =>
        {
            Thread.Sleep(100);
            return 1;
        });
        workload.Wait();
        WorkloadResult<int> result = workload.Result;
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Result);
    }

    [TestMethod]
    public void TestBlockingWaitExplicit2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload workload = factory.ScheduleAsync(_ => Thread.Sleep(100));
        workload.Wait();
        WorkloadResult result = workload.Result;
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public void TestBlockingWaitExplicitWithTimeout1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload<int> workload = factory.ScheduleAsync(_ =>
        {
            Thread.Sleep(1000);
            return 1;
        });
        bool waitResult = workload.Wait(TimeSpan.FromMilliseconds(100));
        Assert.IsFalse(waitResult);
        WorkloadResult<int> result = workload.GetAwaiter().GetResult();
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Result);
    }

    [TestMethod]
    public void TestBlockingWaitExplicitWithTimeout2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload workload = factory.ScheduleAsync(_ => Thread.Sleep(1000));
        bool waitResult = workload.Wait(TimeSpan.FromMilliseconds(100));
        Assert.IsFalse(waitResult);
        WorkloadResult result = workload.GetAwaiter().GetResult();
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public void TestBlockingWaitExplicitWithTimeout3()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload<int> workload = factory.ScheduleAsync(_ =>
        {
            Thread.Sleep(100);
            return 1;
        });
        bool waitResult = workload.Wait(TimeSpan.FromMilliseconds(250));
        Assert.IsTrue(waitResult);
        WorkloadResult<int> result = workload.Result;
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Result);
    }

    [TestMethod]
    public void TestBlockingWaitExplicitWithTimeout4()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload workload = factory.ScheduleAsync(_ => Thread.Sleep(100));
        bool waitResult = workload.Wait(TimeSpan.FromMilliseconds(250));
        Assert.IsTrue(waitResult);
        WorkloadResult result = workload.GetAwaiter().GetResult();
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task TestCancellationDuringScheduling1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using ManualResetEventSlim mres = new(false);
        factory.Schedule(mres.Wait);
        Workload<int> workload = factory.ScheduleAsync(_ =>
        {
            Thread.Sleep(100);
            return 1;
        });
        bool cancellationResult = workload.TryCancel();
        Assert.IsTrue(cancellationResult);
        mres.Set();
        WorkloadResult<int> result = await workload;
        Assert.IsTrue(result.IsCanceled);
        Assert.IsFalse(result.TryGetResult(out _));
    }

    [TestMethod]
    public async Task TestCancellationDuringScheduling2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using ManualResetEventSlim mres = new(false);
        factory.Schedule(mres.Wait);
        Workload workload = factory.ScheduleAsync(_ => Thread.Sleep(100));
        bool cancellationResult = workload.TryCancel();
        Assert.IsTrue(cancellationResult);
        mres.Set();
        WorkloadResult result = await workload;
        Assert.IsTrue(result.IsCanceled);
    }

    [TestMethod]
    public async Task TestCancellationDuringExecution1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using ManualResetEventSlim mres = new(false);
        Workload<int> workload = factory.ScheduleAsync(token =>
        {
            mres.Wait();
            token.ThrowIfCancellationRequested();
            return 1;
        });
        bool cancellationResult = workload.TryCancel();
        Assert.IsTrue(cancellationResult);
        mres.Set();
        WorkloadResult<int> result = await workload;
        Assert.IsTrue(result.IsCanceled);
        Assert.IsFalse(result.TryGetResult(out _));
    }

    [TestMethod]
    public async Task TestCancellationDuringExecution2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using ManualResetEventSlim mres = new(false);
        Workload workload = factory.ScheduleAsync(token =>
        {
            mres.Wait();
            token.ThrowIfCancellationRequested();
        });
        bool cancellationResult = workload.TryCancel();
        Assert.IsTrue(cancellationResult);
        mres.Set();
        WorkloadResult result = await workload;
        Assert.IsTrue(result.IsCanceled);
    }

    [TestMethod]
    public async Task TestCancellationDuringExecution3()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using ManualResetEventSlim mres = new(false);
        using ManualResetEventSlim isRunningMres = new(false);
        Workload<int> workload = factory.ScheduleAsync(token =>
        {
            isRunningMres.Set();
            mres.Wait();
            return token.IsCancellationRequested ? 1 : 2;
        });
        isRunningMres.Wait();
        bool cancellationResult = workload.TryCancel();
        Assert.IsTrue(cancellationResult);
        mres.Set();
        WorkloadResult<int> result = await workload;
        // workload did not honor cancellation and returned a result
        Assert.AreEqual<WorkloadStatus>(WorkloadStatus.RanToCompletion | WorkloadStatus.ContinuationsInvoked, workload.Status);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Result);
    }

    [TestMethod]
    public async Task TestCancellationDuringExecution4()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using ManualResetEventSlim mres = new(false);
        Workload<int> workload = factory.ScheduleAsync(token =>
        {
            mres.Wait();
            if (token.IsCancellationRequested)
            {
                token.MarkCanceled();
                return default;
            }
            return 1;
        });
        bool cancellationResult = workload.TryCancel();
        Assert.IsTrue(cancellationResult);
        mres.Set();
        WorkloadResult<int> result = await workload;
        // workload honored cancellation and returned a result
        Assert.IsTrue(result.IsCanceled);
    }

    [TestMethod]
    public async Task TestCancellationUsingCancellationTokenDuringScheduling1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using CancellationTokenSource cts = new();
        using ManualResetEventSlim mres = new(false);
        factory.Schedule(mres.Wait);
        Workload<int> workload = factory.ScheduleAsync(_ => 1, cts.Token);
        await cts.CancelAsync();
        mres.Set();
        WorkloadResult<int> result = await workload;
        Assert.IsTrue(result.IsCanceled);
        Assert.IsFalse(result.TryGetResult(out _));
    }

    [TestMethod]
    public async Task TestCancellationUsingCancellationTokenDuringScheduling2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using CancellationTokenSource cts = new();
        using ManualResetEventSlim mres = new(false);
        await cts.CancelAsync();
        Workload workload = factory.ScheduleAsync(_ => { }, cts.Token);
        Assert.IsTrue(workload.IsCompleted);
        Assert.AreEqual<WorkloadStatus>(WorkloadStatus.Canceled | WorkloadStatus.ContinuationsInvoked, workload.Status);
        WorkloadResult result = await workload;
        Assert.IsTrue(result.IsCanceled);
    }

    [TestMethod]
    public async Task TestCancellationUsingCancellationTokenDuringExecution1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using CancellationTokenSource cts = new();
        using ManualResetEventSlim mres = new(false);
        Workload<int> workload = factory.ScheduleAsync(token =>
        {
            mres.Wait();
            token.ThrowIfCancellationRequested();
            return 1;
        }, cts.Token);
        await cts.CancelAsync();
        mres.Set();
        WorkloadResult<int> result = await workload;
        Assert.IsTrue(result.IsCanceled);
        Assert.IsFalse(result.TryGetResult(out _));
    }

    [TestMethod]
    public async Task TestCancellationUsingCancellationTokenDuringExecution2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using CancellationTokenSource cts = new();
        using ManualResetEventSlim mres = new(false);
        Workload workload = factory.ScheduleAsync(token =>
        {
            mres.Wait();
            token.ThrowIfCancellationRequested();
        }, cts.Token);
        await cts.CancelAsync();
        mres.Set();
        WorkloadResult result = await workload;
        Assert.IsTrue(result.IsCanceled);
    }

    [TestMethod]
    public async Task TestCancellationUsingCancellationTokenDuringExecution3()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using CancellationTokenSource cts = new();
        using ManualResetEventSlim mres = new(false);
        using ManualResetEventSlim isRunningMres = new(false);
        Workload<int> workload = factory.ScheduleAsync(token =>
        {
            isRunningMres.Set();
            mres.Wait();
            return token.IsCancellationRequested ? 1 : 2;
        }, cts.Token);
        isRunningMres.Wait();
        await cts.CancelAsync();
        mres.Set();
        WorkloadResult<int> result = await workload;
        // workload did not honor cancellation and returned a result
        Assert.AreEqual<WorkloadStatus>(WorkloadStatus.RanToCompletion | WorkloadStatus.ContinuationsInvoked, workload.Status);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Result);
    }

    [TestMethod]
    public async Task TestCancellationUsingCancellationTokenDuringExecution4()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using CancellationTokenSource cts = new();
        using ManualResetEventSlim mres = new(false);
        Workload<int> workload = factory.ScheduleAsync(token =>
        {
            mres.Wait();
            if (token.IsCancellationRequested)
            {
                token.MarkCanceled();
                return default;
            }
            return 1;
        }, cts.Token);
        await cts.CancelAsync();
        mres.Set();
        WorkloadResult<int> result = await workload;
        // workload honored cancellation and returned a result
        Assert.IsTrue(result.IsCanceled);
    }

    [TestMethod]
    public async Task TestCancellationUsingCancellationTokenDuringExecution5()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        using CancellationTokenSource cts = new();
        using ManualResetEventSlim mres = new(false);
        using ManualResetEventSlim isRunningMres = new(false);
        Workload<int> workload = factory.ScheduleAsync(token =>
        {
            isRunningMres.Set();
            mres.Wait();
            if (token.IsCancellationRequested)
            {
                token.MarkCanceled();
                return default;
            }
            return 1;
        }, cts.Token);
        isRunningMres.Wait();
        await cts.CancelAsync();
        Assert.AreEqual(WorkloadStatus.CancellationRequested, workload.Status);
        mres.Set();
        WorkloadResult<int> result = await workload;
        // workload honored cancellation and returned a result
        Assert.AreEqual<WorkloadStatus>(WorkloadStatus.Canceled | WorkloadStatus.ContinuationsInvoked, workload.Status);
        Assert.IsTrue(result.IsCanceled);
    }

    [TestMethod]
    public async Task TestSelfCancellationWithException()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        Workload<int> workload = factory.ScheduleAsync<int>(_ => throw new WorkloadCanceledException());
        WorkloadResult<int> result = await workload;
        Assert.IsTrue(result.IsCanceled);
        Assert.IsFalse(result.TryGetResult(out _));
    }

    [TestMethod]
    public async Task TestFaultedWorkload1()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        const string MESSAGE = "Test exception.";
        Workload<int> workload = factory.ScheduleAsync<int>(_ => throw new InvalidOperationException(MESSAGE));
        WorkloadResult<int> result = await workload;
        Assert.AreEqual<WorkloadStatus>(WorkloadStatus.Faulted | WorkloadStatus.ContinuationsInvoked, workload.Status);
        Assert.IsTrue(result.IsFaulted);
        Assert.IsFalse(result.TryGetResult(out _));
        Assert.IsNotNull(result.Exception);
        Assert.AreEqual(MESSAGE, result.Exception!.Message);
    }

    [TestMethod]
    public async Task TestFaultedWorkload2()
    {
        using ClasslessWorkloadFactory<int> factory = CreateDefaultFactory();
        const string MESSAGE = "Test exception.";
        Workload workload = factory.ScheduleAsync(_ => throw new InvalidOperationException(MESSAGE));
        WorkloadResult result = await workload;
        Assert.AreEqual<WorkloadStatus>(WorkloadStatus.Faulted | WorkloadStatus.ContinuationsInvoked, workload.Status);
        Assert.IsTrue(result.IsFaulted);
        Assert.IsNotNull(result.Exception);
        Assert.AreEqual(MESSAGE, result.Exception!.Message);
    }
}