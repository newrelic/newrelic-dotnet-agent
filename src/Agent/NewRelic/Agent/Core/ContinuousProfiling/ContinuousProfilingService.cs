// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Owns the continuous-profiling session lifecycle. Reacts to configuration: when enabled it
/// starts the native sampler and schedules a periodic drain; when disabled it stops both; when
/// the sampling interval changes while running it retunes the drain schedule.
///
/// Each drain reads one batch from the <see cref="ISampleSource"/> into a reused buffer, parses it,
/// builds an OTLP profile, and hands it to the <see cref="IProfilesTransport"/>. All drain work is
/// wrapped so a failure is logged and metered but never propagates into the customer's application.
/// </summary>
public class ContinuousProfilingService : ConfigurationBasedService, IContinuousProfilingSessionControl
{
    // Generous fixed buffer reused across drains; a batch of stack samples is well under this.
    private const int DrainBufferSize = 1024 * 1024;

    // How long to wait before re-attempting a start that was deferred because a thread-profiling
    // session was in-flight. Thread-profiling sessions are short and time-boxed, so a modest retry
    // interval reconciles the two profilers without busy-waiting.
    private static readonly TimeSpan DeferredStartRetryInterval = TimeSpan.FromSeconds(15);

    private const string SupportabilityDrainMetric = "Supportability/DotNET/ContinuousProfiling/Drain";
    private const string SupportabilitySamplesMetric = "Supportability/DotNET/ContinuousProfiling/Samples";
    private const string SupportabilityErrorMetric = "Supportability/DotNET/ContinuousProfiling/Error";

    private readonly ISampleSource _sampleSource;
    private readonly INativeContinuousProfiler _native;
    private readonly IProfilesTransport _transport;
    private readonly IScheduler _scheduler;
    private readonly IAgentHealthReporter _agentHealthReporter;

    // Managed->native trace-context push seam. Armed while a session is active (published as the process-wide
    // ContinuousProfilingContext.Instance so the wrapper hot path can reach it), disarmed when it stops.
    private readonly ContinuousProfilingContext _continuousProfilingContext = new ContinuousProfilingContext();

    // Stable delegate reference: ExecuteEvery and StopExecuting must be handed the same instance.
    private readonly Action _drainAction;

    // Single reused drain buffer. Safe because drains never overlap: the Scheduler disables a recurring
    // timer for the duration of each callback (see Scheduler.CreateExecuteEveryTimer), so DrainOnce can't
    // re-enter itself. The only theoretical overlap is a retune (StopLocked then StartLocked reuse this
    // buffer + delegate) if an old drain were still in-flight when the new timer first fires — practically
    // impossible (drains are fast, interval >= 1000 ms). If that ever changes, give each session its own buffer.
    private readonly byte[] _drainBuffer = new byte[DrainBufferSize];

    // Locking posture (deliberately minimal — this type runs inside every instrumented process):
    //   * _lifecycleLock is the ONLY lock, and it guards ONLY the rare lifecycle transitions
    //     (StartIfEnabled / ApplyConfigChange / Dispose). Start/StopLocked run under it (the *Locked
    //     naming = "caller holds the lock").
    //   * The hot path, DrainOnce (fires every 1-60 s), takes NO lock — no steady-state contention.
    //   * Lock ordering is always _lifecycleLock -> Scheduler's internal semaphore, never the reverse.
    private readonly object _lifecycleLock = new object();

    // volatile: read lock-free by ThreadProfilingService's forward guard on a different (collector) thread,
    // written under _lifecycleLock on the scheduler thread. volatile gives the cross-thread visibility the
    // mutual-exclusion guard needs without adding a lock to the read path.
    private volatile bool _isActive;
    private int _activeIntervalMs;
    private long _lastDrainTimestamp;

    public bool IsActive => _isActive;

    /// <summary>
    /// Read-only view of the thread profiler's session state, wired after construction by
    /// <c>AgentManager</c>. Continuous profiling defers its start while a thread-profiling session is
    /// in-flight so the two profilers never run concurrently. Nullable: no seam wired == no deferral.
    /// This is a settable seam (not a constructor dependency) deliberately, to avoid a constructor
    /// cycle with the thread-profiling service, which holds a reference back to this service.
    /// </summary>
    public IThreadProfilingStatus ThreadProfilingStatus { get; set; }

    public ContinuousProfilingService(ISampleSource sampleSource, INativeContinuousProfiler native, IProfilesTransport transport, IScheduler scheduler, IAgentHealthReporter agentHealthReporter)
    {
        _sampleSource = sampleSource;
        _native = native;
        _transport = transport;
        _scheduler = scheduler;
        _agentHealthReporter = agentHealthReporter;
        _drainAction = DrainOnce;
    }

    /// <summary>
    /// Starts the drain schedule if continuous profiling is enabled in the current configuration.
    /// Safe to call more than once; a no-op while already active.
    /// </summary>
    public void StartIfEnabled()
    {
        lock (_lifecycleLock)
        {
            if (!_configuration.ContinuousProfilingEnabled)
                return;

            StartLocked(_configuration.ContinuousProfilingSamplingIntervalMs);
        }
    }

    /// <summary>
    /// Reconciles the running session with the current configuration: start, stop, or retune the
    /// drain schedule as needed. Invoked off the config-update event via the scheduler so the event
    /// handler itself never does synchronous work (see <see cref="OnConfigurationUpdated"/>).
    /// </summary>
    public void ApplyConfigChange()
    {
        lock (_lifecycleLock)
        {
            var enabled = _configuration.ContinuousProfilingEnabled;

            if (!enabled)
            {
                if (_isActive)
                    StopLocked();
                return;
            }

            var intervalMs = _configuration.ContinuousProfilingSamplingIntervalMs;

            if (!_isActive)
            {
                StartLocked(intervalMs);
                return;
            }

            if (intervalMs != _activeIntervalMs)
            {
                // Retune: stop the current recurrence and reschedule at the new interval.
                StopLocked();
                StartLocked(intervalMs);
            }
        }
    }

    private void StartLocked(int intervalMs)
    {
        if (_isActive)
            return;

        // Reverse mutual-exclusion guard: never start while a thread-profiling session is in-flight.
        // Defer instead of running concurrently, and schedule a retry so the session starts once the
        // (short, time-boxed) thread-profiling session completes. The retry re-reads configuration via
        // ApplyConfigChange, so a disable-while-deferred simply causes the retry to no-op.
        //
        // NOTE: this check and ThreadProfilingService's forward guard (IsActive) are read/written under
        // different locks (this service's _lifecycleLock vs. no explicit lock on the thread-profiling
        // side), so a narrow window exists where both services could decide to start concurrently. These
        // managed guards are a cooperative, coarse gate on top of the real enforcement backstop: Plan B's
        // native SuspendMutex (Profiler/ContinuousProfiler/SuspendMutex.h) serializes both profilers'
        // suspend/walk, so even if this window lets both managed sessions start, the shared native mutex
        // still prevents them from suspending/walking threads at the same time.
        if (ThreadProfilingStatus?.IsThreadProfilingActive == true)
        {
            Log.Info("[ContinuousProfiling] Start deferred: a thread-profiling session is active; retrying in {0} ms.", (int)DeferredStartRetryInterval.TotalMilliseconds);
            _scheduler.ExecuteOnce(ApplyConfigChange, DeferredStartRetryInterval);
            return;
        }

        try
        {
            // Start native sampling first, then begin draining it. Both run under _lifecycleLock, which is
            // fine: lifecycle transitions are rare (config-driven), so the native call here does not touch
            // the lock-free hot path (DrainOnce).
            _native.Start(intervalMs);
            _scheduler.ExecuteEvery(_drainAction, TimeSpan.FromMilliseconds(intervalMs));
            _activeIntervalMs = intervalMs;
            _isActive = true;
            _lastDrainTimestamp = Stopwatch.GetTimestamp();

            // Arm trace-context correlation only now that native sampling is running, and publish the seam so
            // the wrapper hot path starts pushing the current trace/span on each app thread.
            _continuousProfilingContext.Enable(_native);
            ContinuousProfilingContext.Instance = _continuousProfilingContext;

            Log.Info("[ContinuousProfiling] Session started; draining every {0} ms.", intervalMs);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to start the drain schedule.");
        }
    }

    private void StopLocked()
    {
        try
        {
            // Disarm correlation first so the wrapper hot path stops pushing before native sampling stops.
            // Restore the inert default instance so IsEnabled is false again everywhere.
            _continuousProfilingContext.Disable();
            ContinuousProfilingContext.Instance = new ContinuousProfilingContext();

            _scheduler.StopExecuting(_drainAction);
            _native.Stop();
            Log.Info("[ContinuousProfiling] Session stopped.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to stop the drain schedule.");
        }
        finally
        {
            _isActive = false;
            _activeIntervalMs = 0;
        }
    }

    /// <summary>
    /// Drains at most one batch and ships it. Catches everything: a drain failure must never surface
    /// in the instrumented application.
    /// </summary>
    public void DrainOnce()
    {
        try
        {
            var bytesRead = _sampleSource.ReadBatch(_drainBuffer);
            if (bytesRead <= 0)
                return;

            // Defensive clamp: a misbehaving native source could report more bytes than the buffer
            // holds. Never trust it far enough to hand an out-of-range length to BufferParser.Parse,
            // which would walk off the end of _drainBuffer.
            if (bytesRead > _drainBuffer.Length)
            {
                Log.Debug("[ContinuousProfiling] ReadBatch reported {0} bytes, exceeding the {1}-byte buffer; discarding this drain.", bytesRead, _drainBuffer.Length);
                SafeReportError();
                return;
            }

            var samples = BufferParser.Parse(_drainBuffer, bytesRead, out var batchStats);

            // Surface the native BatchStats for CP overhead/fidelity analysis (and OTel FinalStats parity):
            // microsSuspended = the stop-the-world window this sweep; skipped = threads/frames the walk missed.
            // onCpu/total is the live signal that the native on-CPU classification is working, since NR CP
            // is no-send-guarded and has no other observation path for it.
            if (batchStats != null && Log.IsFinestEnabled)
                Log.Finest("[ContinuousProfiling] batch stats: microsSuspended={0} threads={1} frames={2} skipped={3} onCpu={4}/{5}",
                    batchStats.MicrosSuspended, batchStats.Threads, batchStats.Frames, batchStats.Skipped, CountOnCpu(samples), samples.Count);

            if (samples.Count == 0)
                return;

            var now = Stopwatch.GetTimestamp();
            var startUnixNano = ToUnixNano(DateTime.UtcNow);
            var durationNano = ElapsedNanos(_lastDrainTimestamp, now);
            _lastDrainTimestamp = now;

            // The sampling interval (ms) is the profile's period; convert to nanoseconds for period_type=cpu/ns.
            var periodNanos = (long)_activeIntervalMs * 1_000_000L;
            // Exclude the agent's own threads/frames unless the undocumented appSettings opt-in is set.
            var request = OtlpProfileBuilder.Build(samples, startUnixNano, durationNano, ServiceName, periodNanos, _configuration.ContinuousProfilingIncludeAgentCode);
            _transport.Send(request);

            _agentHealthReporter.ReportSupportabilityCountMetric(SupportabilityDrainMetric);
            _agentHealthReporter.ReportSupportabilityCountMetric(SupportabilitySamplesMetric, samples.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Drain failed.");
            SafeReportError();
        }
    }

    /// <summary>
    /// Counts samples classified as on-CPU. Public (not internal) so unit tests can reach it without
    /// resorting to <c>InternalsVisibleTo</c>, which this repo bans.
    /// </summary>
    public static int CountOnCpu(IReadOnlyList<ManagedThreadSample> samples)
    {
        var onCpu = 0;
        for (var i = 0; i < samples.Count; i++)
            if (samples[i].OnCpu) onCpu++;
        return onCpu;
    }

    private void SafeReportError()
    {
        try
        {
            _agentHealthReporter.ReportSupportabilityCountMetric(SupportabilityErrorMetric);
        }
        catch (Exception ex)
        {
            Log.Finest(ex, "[ContinuousProfiling] Failed to report the drain-error metric.");
        }
    }

    private string ServiceName => _configuration.ApplicationNames?.FirstOrDefault() ?? string.Empty;

    // 1970-01-01T00:00:00Z in DateTime ticks; netstandard2.0 has no DateTime.UnixEpoch.
    private static readonly long UnixEpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    private static long ToUnixNano(DateTime utc) =>
        (utc.Ticks - UnixEpochTicks) * 100L; // 1 tick == 100 ns

    private static long ElapsedNanos(long fromTimestamp, long toTimestamp)
    {
        if (fromTimestamp <= 0 || toTimestamp <= fromTimestamp)
            return 0;

        var seconds = (toTimestamp - fromTimestamp) / (double)Stopwatch.Frequency;
        return (long)(seconds * 1_000_000_000L);
    }

    protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
    {
        // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
        // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).
        // Defer all start/stop/retune work to the scheduler so nothing runs synchronously on the config-update event.
        _scheduler.ExecuteOnce(ApplyConfigChange, TimeSpan.Zero);
    }

    public override void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_isActive)
                StopLocked();

            // Explicit, deterministic join of the native worker thread on normal teardown. The native
            // destructor also guards against a never-joined thread (defense in depth against
            // std::terminate), but Dispose is the clean path -- never let a failure here escape Dispose.
            try
            {
                _native.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ContinuousProfiling] Failed to shut down the native profiler.");
            }
        }

        base.Dispose();
    }
}
