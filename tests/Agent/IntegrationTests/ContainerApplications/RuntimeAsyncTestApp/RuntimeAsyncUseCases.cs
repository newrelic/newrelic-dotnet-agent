// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace RuntimeAsyncTestApp;

/// <summary>
/// The Linux counterpart of the host-run RuntimeAsyncApplication's use cases. Kept as a separate
/// copy rather than shared: container apps compile inside an isolated Docker context whose build
/// context is only the ContainerApplications folder, so a project reference reaching up into
/// Applications/ would not resolve inside the image.
/// </summary>
public class RuntimeAsyncUseCases
{
    // MethodImplAttributes.Async. The BCL has no named member for it, so the literal is the
    // only way to test for the flag - and it is exactly the flag the profiler keys on to
    // recognize a runtime-async method.
    private const MethodImplAttributes AsyncMethodImplAttribute = (MethodImplAttributes)0x2000;

    // The stranded-transaction failure only manifests when a continuation resumes on a
    // different thread than the one that created the transaction, which is up to the thread
    // pool. Measured at roughly one dropped segment per twenty nested calls, so the count has
    // to be high enough that a regression cannot slip through on a lucky run.
    public const int Iterations = 25;

    /// <summary>
    /// Throws unless the compiler actually emitted runtime-async methods. An SDK that does not
    /// understand runtime-async=on silently compiles ordinary state-machine async instead, in
    /// which case every assertion in the test would still pass while exercising none of the
    /// runtime-async code paths. Failing here turns that false green into a hard failure.
    /// </summary>
    public static void VerifyCompiledAsRuntimeAsync()
    {
        foreach (var methodName in new[] { nameof(OuterAsync), nameof(InnerAsync) })
        {
            var method = typeof(RuntimeAsyncUseCases).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Could not reflect over {methodName}.");

            if ((method.MethodImplementationFlags & AsyncMethodImplAttribute) == 0)
            {
                throw new InvalidOperationException(
                    $"{methodName} was not compiled as a runtime-async method - MethodImplAttributes.Async (0x2000) is absent. " +
                    "The SDK building this application does not honor <Features>runtime-async=on</Features>, so this test " +
                    "would exercise state-machine async instead and prove nothing about runtime-async.");
            }
        }
    }

    /// <summary>
    /// Returns Task&lt;int&gt;, so the agent has to synthesize a completed Task from an unwrapped
    /// int. Its two nested calls both happen after a suspension point, which is the shape that
    /// exposed nested segments being dropped: a runtime-async method's after-delegate does not
    /// fire until true completion, so its transaction stays in the creating thread's primary
    /// storage while these continuations resume on whatever thread the pool supplies.
    /// </summary>
    [Transaction]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task<int> OuterAsync(int value)
    {
        await Task.Delay(50);

        await InnerAsync(value);

        await Task.Delay(50);

        await InnerAsync(value + 1);

        return value * 2;
    }

    /// <summary>
    /// Returns a bare Task, so the agent has to synthesize Task.CompletedTask - the other half
    /// of the result normalization. Nested inside OuterAsync's transaction, this records a
    /// segment rather than starting a transaction of its own.
    /// </summary>
    [Transaction]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public async Task InnerAsync(int value)
    {
        await Task.Delay(25);

        Console.WriteLine($"InnerAsync({value}) completed on thread {Environment.CurrentManagedThreadId}");
    }

    /// <summary>
    /// A plain synchronous transaction, present purely as a positive control for the thread.id
    /// span attribute. The agent records thread.id only for non-async segments, so without a
    /// segment that is genuinely expected to carry it, asserting its absence on the async
    /// segments would pass just as happily if the attribute were never emitted at all.
    /// </summary>
    [Transaction]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SynchronousControl()
    {
        Console.WriteLine($"SynchronousControl ran on thread {Environment.CurrentManagedThreadId}");
    }
}
