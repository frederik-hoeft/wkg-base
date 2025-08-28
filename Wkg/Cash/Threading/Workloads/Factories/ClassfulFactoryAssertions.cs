using Cash.Threading.Workloads.Queuing.Classful;
using Cash.Threading.Workloads.Scheduling;
using System.Diagnostics.CodeAnalysis;

namespace Cash.Threading.Workloads.Factories;

internal static class ClassfulFactoryAssertions
{
    internal static void AssertHasClassfulRoot<THandle>(this IWorkloadScheduler<THandle> scheduler) where THandle : unmanaged
    {
        if (scheduler.Root is not IClassfulQdisc<THandle>)
        {
            ThrowArgumentException("The scheduler's root queueing discipline must be classful.", nameof(scheduler));
        }
    }

    [DoesNotReturn]
    private static void ThrowArgumentException(string message, string paramName) => throw new ArgumentException(message, paramName);
}