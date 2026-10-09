// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.OpenTelemetry;

/// <summary>
/// Starts an activity on a thread whose thread-local transaction (B) differs from the transaction (A)
/// that owns the current ExecutionContext. Port of the NR-631176 repro.
/// </summary>
[Library]
public class ActivityParentIndexExerciser
{
    public const string ActivitySourceName = "ActivityParentIndexExerciser";
    private const int SegmentsInTransactionA = 40;
    private static readonly ActivitySource Source = new(ActivitySourceName);

    [LibraryMethod]
    public void RunWithTransactionAFinished(int iterations) => Run("Finished", iterations, keepTransactionAOpen: false);

    [LibraryMethod]
    public void RunWithTransactionARunning(int iterations) => Run("Running", iterations, keepTransactionAOpen: true);

    private static void Run(string prefix, int iterations, bool keepTransactionAOpen)
    {
        for (var i = 1; i <= iterations; i++)
        {
            var iteration = i;
            var (capturedFromA, consumerA) = Task.Run(() => RunTransactionAAsync(prefix, iteration, keepTransactionAOpen)).GetAwaiter().GetResult();

            using (Source.StartActivity($"{prefix}-B-consume-{iteration}", ActivityKind.Consumer))
            {
                AttachCurrentTransactionToAsync().GetAwaiter().GetResult();

                ExecutionContext.Run(capturedFromA, _ =>
                {
                    using var orphan = Source.StartActivity($"{prefix}-B-orphan-{iteration}", ActivityKind.Consumer);
                }, null);
            }

            consumerA?.Dispose();
            ConsoleMFLogger.Info($"[ActivityParentIndexExerciser] {prefix} iteration {iteration} completed");
        }
    }

    private static async Task<(ExecutionContext, Activity)> RunTransactionAAsync(string prefix, int iteration, bool keepOpen)
    {
        var consumerA = Source.StartActivity($"{prefix}-A-consume-{iteration}", ActivityKind.Consumer);
        var captured = await BuildDeepCallStackAsync(prefix, iteration);
        if (keepOpen)
            return (captured, consumerA);

        consumerA?.Dispose();
        return (captured, null);
    }

    // [Trace] on an async method makes the agent call AttachToAsync for transaction A.
    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<ExecutionContext> BuildDeepCallStackAsync(string prefix, int iteration)
    {
        await Task.Yield();

        for (var i = 0; i < SegmentsInTransactionA; i++)
        {
            using var child = Source.StartActivity($"{prefix}-A-child-{iteration}-{i}", ActivityKind.Internal);
        }

        using var deep = Source.StartActivity($"{prefix}-A-deep-{iteration}", ActivityKind.Internal);
        return ExecutionContext.Capture();
    }

    // Completes synchronously so the caller stays on its thread; [Trace] attaches B to async storage.
    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task AttachCurrentTransactionToAsync()
    {
        await Task.CompletedTask;
    }
}

#endif
