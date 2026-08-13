// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.ContinuousProfiling;

/// <summary>
/// Drives CPU-busy work that the continuous-profiling session samples and drains. Trace/span correlation
/// (<c>SetTraceContext</c>) is pushed by the wrapper boundary keyed by the calling OS thread ONLY -- it is
/// never propagated to threads spawned from within an instrumented method. So correlation can only be
/// observed on a sample of the SAME thread that is currently executing inside the instrumented
/// [Transaction]/[Trace] method; work handed off to background/worker threads runs with no trace context
/// at all and can never render a link.
/// </summary>
[Library]
public class ContinuousProfilingExerciser
{
    // Allocations per pass before the live-reference window is released (see CorrelatedAllocate). Sized so a
    // pass moves a few MB: the CLR raises AllocationTick only once per ~100 KB allocated, so real allocation
    // VOLUME -- hundreds of MB -- is what produces sampled ticks inside a short test window. Raising the
    // configured samples/minute budget alone does not: the tick rate is what the native sub-sampler paces
    // against, and the delivered rate is capped downstream of it at one sample per drain interval.
    private const int AllocationsPerPass = 256;

    // Rotated so the captured samples carry a MIX of allocated sizes (and therefore a mix of
    // allocated_space values), rather than one repeated constant. All below the 85 KB large-object
    // threshold on purpose: this should look like ordinary gen0 pressure from a real application, not one
    // giant LOH/pinned buffer.
    private static readonly int[] AllocationSizes = { 1024, 4 * 1024, 16 * 1024, 48 * 1024, 64 * 1024 };

    /// <summary>
    /// Spins up <paramref name="threadCount"/> background threads that each run CPU-busy transactions for
    /// <paramref name="runSeconds"/> seconds. With a small sampling interval (e.g. 1000 ms) this comfortably
    /// spans several capture+drain cycles. Useful for exercising sampling/drain/render generally, but each
    /// worker thread's own transaction is short-lived (~20ms bursts) and the busy loop itself runs on those
    /// spawned threads, so correlated samples are not reliably observable this way -- see
    /// <see cref="RunCorrelatedBusyWork"/> for that.
    /// </summary>
    [LibraryMethod]
    public void RunBusyWork(int threadCount, int runSeconds)
    {
        ConsoleMFLogger.Info($"[ContinuousProfilingExerciser] Starting {threadCount} thread(s) of busy work for {runSeconds}s.");

        var deadline = DateTime.UtcNow.AddSeconds(runSeconds);
        var threads = new Thread[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() => WorkUntil(deadline)) { IsBackground = true };
            threads[i].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        ConsoleMFLogger.Info("[ContinuousProfilingExerciser] Busy work complete.");
    }

    private void WorkUntil(DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            BusyTransaction();
            // Small yield so the sampler can walk stacks without the process being fully saturated.
            Thread.Sleep(5);
        }
    }

    /// <summary>
    /// A single instrumented transaction containing a nested traced segment doing CPU-bound work. Running the
    /// hot loop inside a transaction is what gives the continuous profiler a live trace/span context to
    /// correlate samples against.
    /// </summary>
    [Transaction]
    [Trace]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public void BusyTransaction()
    {
        BurnCpu();
    }

    [Trace]
    private void BurnCpu()
    {
        // Deterministic CPU-bound work; the result is consumed so the JIT cannot elide the loop.
        var sw = Stopwatch.StartNew();
        double acc = 0;
        var n = 0L;
        while (sw.ElapsedMilliseconds < 20)
        {
            acc += Math.Sqrt((n % 10000) + 1);
            n++;
        }

        if (acc < 0)
        {
            ConsoleMFLogger.Info($"[ContinuousProfilingExerciser] Unreachable {acc}.");
        }
    }

    /// <summary>
    /// Runs a SINGLE-THREADED, synchronous CPU-bound loop inline on the CALLING thread, inside one
    /// instrumented [Transaction]/[Trace] method, for <paramref name="runSeconds"/> seconds. Deliberately
    /// does NOT spawn any worker threads: <c>SetTraceContext</c> is keyed by the calling OS thread id, so
    /// the trace/span context pushed at this method's wrapper boundary is only visible to a continuous-
    /// profiling sample of THIS thread. Running the busy-loop here (rather than handing it to another
    /// thread, as <see cref="RunBusyWork"/> does) keeps this thread inside the instrumented method for the
    /// whole duration, so it stays sample-able-with-context across many sampling intervals -- this is what
    /// makes the trace/span link on a rendered sample reliably observable.
    /// </summary>
    [LibraryMethod]
    public void RunCorrelatedBusyWork(int runSeconds)
    {
        ConsoleMFLogger.Info($"[ContinuousProfilingExerciser] Starting single-threaded correlated busy work for {runSeconds}s.");

        CorrelatedBusyTransaction(runSeconds);

        ConsoleMFLogger.Info("[ContinuousProfilingExerciser] Correlated busy work complete.");
    }

    [Transaction]
    [Trace]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public void CorrelatedBusyTransaction(int runSeconds)
    {
        CorrelatedBurnCpu(runSeconds);
    }

    [Trace]
    private void CorrelatedBurnCpu(int runSeconds)
    {
        // Deterministic CPU-bound work run inline on the calling (traced) thread for the whole duration --
        // no Sleep, no handoff to another thread -- so the continuous profiler samples this thread many
        // times while its native TLS still holds the trace/span context pushed for this method.
        var deadline = DateTime.UtcNow.AddSeconds(runSeconds);
        double acc = 0;
        var n = 0L;
        while (DateTime.UtcNow < deadline)
        {
            acc += Math.Sqrt((n % 10000) + 1);
            n++;
        }

        if (acc < 0)
        {
            ConsoleMFLogger.Info($"[ContinuousProfilingExerciser] Unreachable {acc}.");
        }
    }

    /// <summary>
    /// The allocation-sampling counterpart to <see cref="RunCorrelatedBusyWork"/>: a SINGLE-THREADED,
    /// synchronous loop that runs inline on the CALLING thread, inside one instrumented
    /// [Transaction]/[Trace] method, allocating for <paramref name="runSeconds"/> seconds. Same
    /// no-thread-handoff shape and for the same reason -- allocation samples are captured on the
    /// allocating thread and read that thread's own trace context, so only allocations made while this
    /// thread is inside the instrumented method can render a trace/span link.
    /// </summary>
    [LibraryMethod]
    public void RunCorrelatedAllocatingWork(int runSeconds)
    {
        ConsoleMFLogger.Info($"[ContinuousProfilingExerciser] Starting single-threaded correlated allocating work for {runSeconds}s.");

        CorrelatedAllocatingTransaction(runSeconds);

        ConsoleMFLogger.Info("[ContinuousProfilingExerciser] Correlated allocating work complete.");
    }

    [Transaction]
    [Trace]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public void CorrelatedAllocatingTransaction(int runSeconds)
    {
        CorrelatedAllocate(runSeconds);
    }

    [Trace]
    private void CorrelatedAllocate(int runSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(runSeconds);

        // A short rolling window of live references, released once per pass. Two purposes: the arrays
        // actually survive briefly, so the GC sees real pressure (matching production allocation patterns)
        // rather than immediately-dead garbage; and the reference escaping into this list is what stops the
        // JIT from stack-allocating the array outright (.NET 9+ does that for small non-escaping arrays),
        // which would produce no AllocationTick at all.
        var live = new List<byte[]>(AllocationsPerPass);
        long consumed = 0;
        var n = 0;

        // The deadline is checked once per pass, not per allocation: DateTime.UtcNow costs more than the
        // allocations it would gate, and throttling the loop would starve it of the volume it needs.
        do
        {
            for (var i = 0; i < AllocationsPerPass; i++)
            {
                var buffer = new byte[AllocationSizes[n++ % AllocationSizes.Length]];

                // Write then read both ends so the allocation cannot be elided as unused.
                buffer[0] = (byte)n;
                buffer[buffer.Length - 1] = (byte)i;
                consumed += buffer[0] + buffer[buffer.Length - 1];

                live.Add(buffer);
            }

            live.Clear();
        }
        while (DateTime.UtcNow < deadline);

        if (consumed < 0)
        {
            ConsoleMFLogger.Info($"[ContinuousProfilingExerciser] Unreachable {consumed}.");
        }
    }
}
