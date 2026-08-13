// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
/// TWO INDEPENDENT SAMPLERS, ONE DRAIN. The timer-driven thread sampler
/// (<see cref="INativeContinuousProfiler"/> + <see cref="ISampleSource"/>, state: <see cref="_isActive"/> /
/// <see cref="_activeIntervalMs"/>) and the AllocationTick-driven allocation sampler
/// (<see cref="IAllocationSampleSource"/>, state: <see cref="_allocationActive"/> /
/// <see cref="_allocationMaxSamplesPerMinute"/>) start and stop on their own config flags, in either
/// combination. They SHARE one recurring drain tick, whose arming is therefore refcounted on
/// "either sampler is running" (<see cref="ArmDrainScheduleLocked"/> /
/// <see cref="DisarmDrainScheduleIfIdleLocked"/>) rather than owned by the thread-sampling path -- as is the
/// managed-&gt;native trace-context seam, which both samplers correlate through. They also share the
/// send-failure backoff, since one drain produces one request carrying both sample types.
///
/// Each drain reads one batch from each active source into a reused buffer, parses them,
/// builds a single OTLP request carrying both sample types, and hands it to the
/// <see cref="IProfilesTransport"/>. All drain work is
/// wrapped so a failure is logged and metered but never propagates into the customer's application.
///
/// Drains are gated on the agent having connected (<see cref="OnAgentConnected"/>): the profiles
/// endpoint is only known post-preconnect, so a drain before that point does nothing rather than
/// building a profile with nowhere to send it.
///
/// Repeated send failures pause native sampling and retry via a single-attempt probe on a backoff
/// schedule (<see cref="OnSendResult"/>/<see cref="TripBackoffAndScheduleProbeLocked"/>/<see cref="EndBackoffProbeIfCurrent"/>)
/// rather than retrying the send itself -- a dropped profile can't be held over like a harvest payload can.
/// A reconnect that arrives while backing off resumes immediately instead of waiting out the remaining
/// delay (<see cref="ResumeAfterReconnect"/>), since the reconnect itself is the likely fix.
/// </summary>
public class ContinuousProfilingService : ConfigurationBasedService, IContinuousProfilingSessionControl, IContinuousProfilingCommandTarget
{
    // Must be >= native's MaxBufferBytes (Profiler/ContinuousProfiler/ContinuousProfiler.h) -- native
    // already caps a batch at that ceiling (truncate + count on its own side), but ReadThreadSamples
    // copies min(available, len) and frees the native slot regardless of fit, so a smaller managed
    // buffer here would silently lose the tail of any batch between the two sizes (BatchStats is
    // written last, so it's the first casualty). If you change either constant, check the other file.
    private const int DrainBufferSize = 4 * 1024 * 1024;

    // How long to wait before re-attempting a start that was deferred because a thread-profiling
    // session was in-flight. Thread-profiling sessions are short and time-boxed, so a modest retry
    // interval reconciles the two profilers without busy-waiting.
    private static readonly TimeSpan DeferredStartRetryInterval = TimeSpan.FromSeconds(15);

    private const string SupportabilityDrainMetric = "Supportability/DotNET/ContinuousProfiling/Drain";
    private const string SupportabilitySamplesMetric = "Supportability/DotNET/ContinuousProfiling/Samples";
    private const string SupportabilityErrorMetric = "Supportability/DotNET/ContinuousProfiling/Error";
    private const string SupportabilityDrainBufferBoundaryMetric = "Supportability/DotNET/ContinuousProfiling/DrainBufferBoundary";
    // Allocation samples are counted separately from thread samples: the two come from different native
    // sources at wildly different cadences (timer-driven sweep vs. subsampled AllocationTick), so a combined
    // count would tell you nothing about either. This is also the only observation path for allocation
    // sampling actually producing data in the field.
    private const string SupportabilityAllocationSamplesMetric = "Supportability/DotNET/ContinuousProfiling/AllocationSamples";

    // Send-failure backoff, generally modeled on ConnectionManager.ConnectionRetryBackoffSequence (same
    // values as the collector-response-handling reconnect schedule) -- but, unlike that sequence, this one
    // resets fully to index 0 on a single successful send: a dropped profile can't be recovered later like
    // a held-over harvest cycle can, so there's no reason to stay pessimistic once sending works again.
    private static readonly TimeSpan[] SendBackoffSequence = new[]
    {
        TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(300)
    };

    // Consecutive send failures tolerated before pausing sampling THE FIRST TIME a failure streak starts
    // -- a single blip doesn't trip it. Once already escalated (see OnSendResult), a single failure
    // re-trips immediately; this grace is not paid again on every retry.
    private const int SendFailureGraceCount = 2;

    private readonly ISampleSource _sampleSource;
    private readonly INativeContinuousProfiler _native;

    // The allocation sampler: an INDEPENDENT native sampler with its own config gate, its own EventPipe
    // session and its own buffer queue, drained through the SAME drain tick as the thread sampler so both
    // sample types ride one wire payload per drain (see OtlpProfileBuilder.Build's allocationSamples
    // parameter). Its Shutdown() is terminal -- see StopAllocationLocked/Dispose.
    private readonly IAllocationSampleSource _allocationSampleSource;

    private readonly IProfilesTransport _transport;
    private readonly IScheduler _scheduler;
    private readonly IAgentHealthReporter _agentHealthReporter;

    // volatile: set on the event-bus thread (OnAgentConnected), read on the scheduler thread
    // (DrainOnce) -- gives the cross-thread visibility DrainOnce's early-out needs without a lock.
    // Monotonic true after the first successful connect; never reset on a later disconnect.
    private volatile bool _isConnected;

    // volatile: DrainOnce reads this every tick as its gate, lock-free, and that unlocked read is the only
    // one -- which is what the volatile buys. Every WRITE is under _lifecycleLock, from three directions:
    // EndBackoffProbe and ResumeAfterReconnect (scheduled/event-bus callbacks on other threads) and
    // DrainOnce's own OnSendResult -> TripBackoffAndScheduleProbeLocked chain. Serializing that last one is
    // what stops a reconnect landing between the gate-set and the native stop.
    //
    // _consecutiveSendFailures/_backoffIndex need no volatile: every read and write of them is under
    // _lifecycleLock, so the monitor publishes them. Writing the gate LAST (see StartLocked/
    // ResumeAfterReconnect) is belt-and-braces on top of that -- a volatile write publishes everything
    // written before it, so a thread seeing the cleared gate also sees the zeroed counters if anything ever
    // starts reading them without the lock.
    private volatile bool _sendBackoffActive;
    private int _consecutiveSendFailures;
    private int _backoffIndex;

    // Identifies the current backoff round. Bumped (under _lifecycleLock) whenever a round is started,
    // superseded, or abandoned -- a trip schedules a probe carrying the round's generation, and the probe
    // no-ops if that generation is no longer current when it fires. IScheduler has no cancellation, so a
    // probe scheduled by an earlier round always fires; this counter is what stops a stale probe from
    // resuming sampling (and clearing _sendBackoffActive) in the middle of a later, legitimate round.
    private int _backoffGeneration;

    // Managed->native trace-context push seam. Armed while a session is active (published as the process-wide
    // ContinuousProfilingContext.Instance so the wrapper hot path can reach it), disarmed when it stops.
    private readonly ContinuousProfilingContext _continuousProfilingContext = new ContinuousProfilingContext();

    // Stable delegate reference: ExecuteEvery and StopExecuting must be handed the same instance.
    private readonly Action _drainAction;

    // Single reused drain buffer. Overlapping drains would tear it, so DrainOnce is guarded by
    // _drainInFlight below.
    private readonly byte[] _drainBuffer = new byte[DrainBufferSize];

    // Interlocked-managed reentrancy guard. Normally DrainOnce cannot re-enter itself (the Scheduler
    // disarms a recurring timer for the duration of its callback), but a retune's StopExecuting-without-
    // wait followed immediately by a new timer registration (see ApplyConfigChange) can let an old,
    // still-in-flight drain (e.g. blocked in a slow/hung synchronous send) overlap with the new timer's
    // first tick -- both would otherwise race over the single shared _drainBuffer. 0 = idle, 1 = in flight.
    private int _drainInFlight;

    // Locking posture (deliberately minimal — this type runs inside every instrumented process):
    //   * _lifecycleLock is the ONLY lock. It guards the rare lifecycle transitions (StartIfEnabled /
    //     ApplyConfigChange / Dispose) plus the backoff state they share with the drain path
    //     (OnSendResult). Start/StopLocked and TripBackoffAndScheduleProbeLocked run under it (the
    //     *Locked naming = "caller holds the lock").
    //   * DrainOnce (fires every 1-60 s) takes it once per drain, in OnSendResult, and never for the
    //     read/parse/build work — the gate check at the top is a lock-free volatile read. Contention is
    //     nil in steady state: the only other contenders are config changes and teardown.
    //   * Lock ordering is always _lifecycleLock -> Scheduler's internal semaphore, never the reverse.
    private readonly object _lifecycleLock = new object();

    // volatile: read lock-free by ThreadProfilingService's forward guard on a different (collector) thread,
    // written under _lifecycleLock on the scheduler thread. volatile gives the cross-thread visibility the
    // mutual-exclusion guard needs without adding a lock to the read path.
    private volatile bool _isActive;

    // volatile: Dispose sets this under _lifecycleLock; every other lock-holding entry point checks it
    // immediately after acquiring the lock, so a deferred callback that lands after Dispose (the 15s
    // thread-profiling-deferral retry, or a queued OnConfigurationUpdated) becomes a no-op instead of
    // restarting a native sampler whose worker thread Dispose already joined.
    private volatile bool _disposed;

    // volatile: written under _lifecycleLock (StartLocked/StopLocked), read lock-free by DrainOnce on the
    // scheduler thread. An int write is already atomic on every platform, so this is purely for cross-thread
    // visibility -- without it DrainOnce could read a stale 0 and emit a profile with period=0.
    //
    // Strictly the THREAD sampler's interval: it is the profile period for the cpu/off_cpu profiles, and 0
    // means "no thread sampling", which is what suppresses those profiles. It is deliberately NOT the drain
    // cadence (that is _drainIntervalMs), because the drain can be running for the allocation sampler alone.
    private volatile int _activeIntervalMs;

    // Whether the allocation sampler is currently started. Fully separate from _isActive: the two samplers
    // have independent lifecycles (independent config flags, and allocation sampling is AllocationTick-driven
    // so it needs no periodic thread walk), and either one alone is enough to keep the shared drain running.
    //
    // volatile for the same reason as _isActive/_activeIntervalMs: every write is under _lifecycleLock, but
    // DrainOnce reads it lock-free on the scheduler thread to decide whether to touch the allocation buffer.
    private volatile bool _allocationActive;

    // The budget the allocation sampler was last started with, so a backoff probe can resume it at the same
    // pacing and ApplyConfigChange can detect a live budget change. Read/written only under _lifecycleLock.
    private int _allocationMaxSamplesPerMinute;

    // The interval the recurring drain timer is currently armed at; 0 == not armed. The drain timer is SHARED
    // by both samplers, so its arm/disarm is refcounted on (_isActive || _allocationActive) rather than owned
    // by the thread-sampling path -- see ArmDrainScheduleLocked/DisarmDrainScheduleIfIdleLocked. Read/written
    // only under _lifecycleLock.
    private int _drainIntervalMs;

    // Profile-type tokens ("cpu") currently owned by an agent command rather than local/server config.
    // ApplyConfigChange must not start/stop/retune a type present here -- only a matching StopFromCommand
    // call or process restart releases it (see StartFromCommand/StopFromCommand below). Modeled as a set,
    // not a bool, because the command spec is per-type ("all"/"cpu"/"heap"): today only "cpu" can ever be
    // a member (heap/allocations isn't implemented), but this generalizes once a second independently
    // toggleable type exists, without another redesign of the guard.
    private readonly HashSet<string> _commandControlledTypes = new HashSet<string>();

    private static readonly IReadOnlyDictionary<string, string> EmptyCommandExceptions = new Dictionary<string, string>();

    // Allocation-free stand-ins for "this sweep read nothing of this type", so a drain that skips one source
    // still has a non-null, zero-count list to test and to hand to OtlpProfileBuilder.
    private static readonly IReadOnlyList<ManagedThreadSample> EmptyThreadSamples = new ManagedThreadSample[0];
    private static readonly IReadOnlyList<AllocationSample> EmptyAllocationSamples = new AllocationSample[0];

    // Command-provided interval bounds mirror DefaultConfiguration's ContinuousProfilingSamplingIntervalMs
    // clamp (DefaultConfiguration.cs: MinContinuousProfilingSamplingIntervalMs/MaxContinuousProfilingSamplingIntervalMs,
    // currently 1000/60000) -- duplicated here because a command-supplied interval is a runtime value that
    // never flows through IConfiguration, so it can't reuse that private clamp.
    private const int MinCommandIntervalMs = 1000;
    private const int MaxCommandIntervalMs = 60000;

    // Accessed exclusively via Interlocked.Read/Exchange. It's a 64-bit value written from DrainOnce's
    // scheduler thread AND from EndBackoffProbeIfCurrent/ResumeAfterReconnect (both under _lifecycleLock,
    // but DrainOnce's read+write is NOT), so a plain read/write could tear on 32-bit and races cross-thread.
    // Interlocked gives both atomicity and a full fence; volatile alone would not fix the 64-bit tearing.
    private long _lastDrainTimestamp;

    /// <summary>
    /// Whether the THREAD sampler is running. Deliberately not widened to include allocation sampling: this
    /// is what <c>ThreadProfilingService</c>'s forward guard reads to avoid running concurrently with a
    /// profiler that suspends threads, and allocation sampling suspends nothing (its tick handler walks only
    /// the current thread, and try_locks the shared native SuspendMutex so it yields to a thread-profiling
    /// walk rather than colliding with it). Reporting allocation-only as "active" here would block thread
    /// profiling for no reason.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Read-only view of the thread profiler's session state, wired after construction by
    /// <c>AgentManager</c>. Continuous profiling defers its start while a thread-profiling session is
    /// in-flight so the two profilers never run concurrently. Nullable: no seam wired == no deferral.
    /// This is a settable seam (not a constructor dependency) deliberately, to avoid a constructor
    /// cycle with the thread-profiling service, which holds a reference back to this service.
    /// </summary>
    public IThreadProfilingStatus ThreadProfilingStatus { get; set; }

    public ContinuousProfilingService(ISampleSource sampleSource, INativeContinuousProfiler native, IAllocationSampleSource allocationSampleSource, IProfilesTransport transport, IScheduler scheduler, IAgentHealthReporter agentHealthReporter)
    {
        _sampleSource = sampleSource;
        _native = native;
        _allocationSampleSource = allocationSampleSource;
        _transport = transport;
        _scheduler = scheduler;
        _agentHealthReporter = agentHealthReporter;
        _drainAction = DrainOnce;

        _subscriptions.Add<AgentConnectedEvent>(OnAgentConnected);
    }

    /// <summary>
    /// Resolves the profiles endpoint from the collector's connection (post-preconnect) and arms
    /// <see cref="_isConnected"/> so drains start doing real work. Before this fires, <see cref="DrainOnce"/>
    /// drops every tick without touching the native sample buffer -- there is nowhere to send to yet.
    /// </summary>
    private void OnAgentConnected(AgentConnectedEvent agentConnectedEvent)
    {
        // Lock-free volatile read, matching DrainOnce's gate: Dispose only unsubscribes via base.Dispose(),
        // so a connect can still land in the window after Dispose. Skip it -- setting the endpoint and
        // flipping _isConnected true post-dispose would let a queued drain tick ship one last profile.
        if (_disposed)
            return;

        var endpoint = ProfilesEndpointResolver.ResolveFromConnectionInfo(agentConnectedEvent.ConnectInfo);
        if (endpoint == null)
        {
            Log.Debug("[ContinuousProfiling] AgentConnectedEvent had no usable connection info; profiles will not be sent.");
            return;
        }

        _transport.UpdateEndpoint(endpoint);
        _isConnected = true;
        Log.Debug("[ContinuousProfiling] Connected; profiles endpoint set to {0}.", endpoint);

        // A (re)connect is itself evidence the send path may have changed (e.g. a new redirect host) --
        // don't make CP wait out the rest of an unrelated backoff window when the most likely fix for it
        // just arrived.
        if (_sendBackoffActive)
            ResumeAfterReconnect();
    }

    /// <summary>
    /// Starts whichever samplers the current configuration enables, and the drain schedule they share.
    /// Safe to call more than once; a no-op for anything already active.
    /// </summary>
    public void StartIfEnabled()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            // Each sampler consults its OWN enabled flag, so a disabled thread sampler must not short-circuit
            // the allocation sampler (or vice versa). The thread sampler goes first so that, when both are
            // enabled, it is the one that arms the shared drain timer -- at ITS interval, which is also the
            // profile period -- instead of the allocation path arming it and the thread start having to retune
            // it a moment later.
            if (_configuration.ContinuousProfilingEnabled)
                StartLocked(_configuration.ContinuousProfilingSamplingIntervalMs);

            if (_configuration.ContinuousProfilingAllocationEnabled)
                StartAllocationLocked(_configuration.ContinuousProfilingAllocationMaxSamplesPerMinute);
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
            if (_disposed)
                return;

            // An agent command owns the cpu bundle until a matching stop command or process restart --
            // an incidental config-update event (an unrelated SSC push, a reconnect) must not silently
            // override an operator's explicit start/stop_continuous_profiler command. See
            // _commandControlledTypes and StartFromCommand/StopFromCommand.
            //
            // The guard covers the THREAD sampler only. Allocation sampling is not command-controllable yet,
            // so its config-driven reconciliation must still run when the cpu bundle is command-owned --
            // hence a guarded call rather than an early return out of the whole method. The thread sampler
            // is reconciled first for the same reason as in StartIfEnabled (it owns the drain cadence).
            if (!_commandControlledTypes.Contains(ContinuousProfilingCommandTypes.Cpu))
                ApplyThreadSamplingConfigChangeLocked();

            ApplyAllocationConfigChangeLocked();
        }
    }

    private void ApplyThreadSamplingConfigChangeLocked()
    {
        if (!_configuration.ContinuousProfilingEnabled)
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

    /// <summary>
    /// Reconciles the allocation sampler with the current configuration: start, stop, or re-pace it.
    /// </summary>
    private void ApplyAllocationConfigChangeLocked()
    {
        if (!_configuration.ContinuousProfilingAllocationEnabled)
        {
            if (_allocationActive)
                StopAllocationLocked();
            return;
        }

        var maxSamplesPerMinute = _configuration.ContinuousProfilingAllocationMaxSamplesPerMinute;

        if (!_allocationActive)
        {
            StartAllocationLocked(maxSamplesPerMinute);
            return;
        }

        // Without this, a live budget change would silently do nothing.
        if (maxSamplesPerMinute != _allocationMaxSamplesPerMinute)
            RepaceAllocationLocked(maxSamplesPerMinute);
    }

    /// <summary>
    /// Starts (or no-ops if already running) continuous profiling for the requested "include" tokens.
    /// See <see cref="IContinuousProfilingCommandTarget"/>.
    /// </summary>
    public ContinuousProfilingCommandResult StartFromCommand(IReadOnlyList<string> requestedTypes, int? sampleIntervalMs, int? cpuReportIntervalMs)
    {
        lock (_lifecycleLock)
        {
            if (_disposed || requestedTypes.Count == 0)
                return BuildCommandResultLocked(EmptyCommandExceptions);

            var exceptions = new Dictionary<string, string>();
            var startCpuBundle = false;

            foreach (var token in requestedTypes)
            {
                ContinuousProfilingCommandTypes.Classify(token, out var startsCpuBundle, out var requestsHeap);
                startCpuBundle |= startsCpuBundle;

                if (requestsHeap)
                    exceptions[ContinuousProfilingCommandTypes.Heap] = "not supported";
                else if (!startsCpuBundle)
                    exceptions[token] = "not supported"; // unrecognized token
            }

            if (startCpuBundle)
            {
                _commandControlledTypes.Add(ContinuousProfilingCommandTypes.Cpu);

                if (!_isActive)
                {
                    var requested = cpuReportIntervalMs ?? sampleIntervalMs ?? _configuration.ContinuousProfilingSamplingIntervalMs;
                    var clamped = Math.Min(MaxCommandIntervalMs, Math.Max(MinCommandIntervalMs, requested));
                    StartLocked(clamped);
                }
                // else: already running -- idempotent no-op per spec; a repeat start does not retune.
            }

            return BuildCommandResultLocked(exceptions);
        }
    }

    /// <summary>
    /// Stops (or no-ops if not running) continuous profiling for the requested "include" tokens, and
    /// releases command ownership of those types back to config control. See
    /// <see cref="IContinuousProfilingCommandTarget"/>.
    /// </summary>
    public ContinuousProfilingCommandResult StopFromCommand(IReadOnlyList<string> requestedTypes)
    {
        lock (_lifecycleLock)
        {
            if (_disposed || requestedTypes.Count == 0)
                return BuildCommandResultLocked(EmptyCommandExceptions);

            var exceptions = new Dictionary<string, string>();
            var stopCpuBundle = false;

            foreach (var token in requestedTypes)
            {
                ContinuousProfilingCommandTypes.Classify(token, out var startsCpuBundle, out var requestsHeap);
                stopCpuBundle |= startsCpuBundle;

                if (requestsHeap)
                    exceptions[ContinuousProfilingCommandTypes.Heap] = "not supported";
                else if (!startsCpuBundle)
                    exceptions[token] = "not supported"; // unrecognized token
            }

            if (stopCpuBundle)
            {
                // Release command ownership regardless of whether it was actually active -- a stop always
                // hands the type back to config control, matching "stop while not profiling is a no-op
                // success".
                _commandControlledTypes.Remove(ContinuousProfilingCommandTypes.Cpu);

                if (_isActive)
                    StopLocked();
            }

            return BuildCommandResultLocked(exceptions);
        }
    }

    private ContinuousProfilingCommandResult BuildCommandResultLocked(IReadOnlyDictionary<string, string> exceptions)
    {
        var activeTypes = _isActive
            ? new[] { ContinuousProfilingCommandTypes.Cpu }
            : Array.Empty<string>();
        var intervalMs = _isActive ? _activeIntervalMs : _configuration.ContinuousProfilingSamplingIntervalMs;

        return new ContinuousProfilingCommandResult(activeTypes, intervalMs, intervalMs, exceptions);
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
        // Serialized against ThreadProfilingService's forward guard (IsActive) via
        // ProfilingMutualExclusionGate.Lock, the same lock TP's StartThreadProfilingSession takes around
        // its own guard-check-and-arm -- so at most one profiler can decide "the other isn't active" and
        // arm itself at a time; the earlier narrow window this used to describe (checking/arming under
        // different, unsynchronized state -- this service's _lifecycleLock vs. no explicit lock on the
        // thread-profiling side) is closed. This method already runs under _lifecycleLock (held by every
        // caller of StartLocked); ThreadProfilingService never takes _lifecycleLock, so the lock order is
        // always _lifecycleLock -> ProfilingMutualExclusionGate.Lock, never the reverse -- no deadlock
        // risk. The native SuspendMutex (Profiler/ContinuousProfiler/SuspendMutex.h) remains the backstop
        // against concurrent suspend/walk, which this lock does not replace -- it only makes the two
        // profilers' *liveness* mutually exclusive as well.
        lock (ProfilingMutualExclusionGate.Lock)
        {
            if (ThreadProfilingStatus?.IsThreadProfilingActive == true)
            {
                Log.Info("[ContinuousProfiling] Start deferred: a thread-profiling session is active; retrying in {0} ms.", (int)DeferredStartRetryInterval.TotalMilliseconds);
                _scheduler.ExecuteOnce(ApplyConfigChange, DeferredStartRetryInterval);
                return;
            }

            try
            {
                var wasBackingOff = ClearSendBackoffForFreshStartLocked();

                // Arm the reverse-guard flag before starting native sampling, while still holding the gate
                // above -- ThreadProfilingService's forward guard can only observe this flag after acquiring
                // the same lock, so there is no window for it to see a stale "not active" value here.
                _isActive = true;

                // Clearing the gate above superseded the pending probe -- the only thing that would ever have
                // resumed the OTHER sampler if a trip had paused it. Resume it here or it stays Stop()'d forever
                // with _allocationActive still reading true, so nothing ever tries again and DrainOnce reads a dead
                // sampler indefinitely.
                //
                // ORDER IS LOAD-BEARING: this MUST come before this sampler's own native start, which can throw.
                // There is no data dependency between the two, and if the resume sat after the throwing call, an
                // own-start failure would skip it entirely -- leaving the other sampler stopped with its flag true
                // and the gate now permanently clear (no probe left to fire), which is unrecoverable short of a
                // stop command or a process restart. The catch below only unwinds THIS sampler, so it cannot repair
                // that. Debt to the other sampler is paid first, then this one takes its own risk.
                if (wasBackingOff && _allocationActive)
                    ResumeAllocationAfterBackoffLocked();

                // Start native sampling first, then begin draining it. Both run under _lifecycleLock, which is
                // fine: lifecycle transitions are rare (config-driven), so the native call here does not touch
                // the lock-free hot path (DrainOnce).
                _native.Start(intervalMs);
                _activeIntervalMs = intervalMs;

                // The thread sampler's interval is the authoritative drain cadence (it doubles as the profile
                // period), so this retunes a timer the allocation path may already have armed at the config
                // interval.
                ArmDrainScheduleLocked(intervalMs);

                Log.Info("[ContinuousProfiling] Session started; draining every {0} ms.", intervalMs);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ContinuousProfiling] Failed to start the drain schedule.");

                // _isActive is armed before the first call that can throw, so a half-started session has to be
                // unwound here -- otherwise the flag stays true with nothing running, which both lies to
                // IsActive and permanently blocks thread profiling via the guard above.
                StopLocked();
            }
        }
    }

    private void StopLocked()
    {
        try
        {
            // Cleared BEFORE the disarm below, not just in the finally: the shared drain schedule is released
            // only when NEITHER sampler needs it, so an _isActive still reading true at that point would make
            // the release a no-op and leave the timer armed with nothing running. (This also has to be right
            // on the StartLocked-unwind path, where StopLocked is called with _isActive still true.) The
            // finally is kept as a redundant guarantee that a throw anywhere below cannot leave them stale.
            _isActive = false;
            _activeIntervalMs = 0;

            DisarmDrainScheduleIfIdleLocked();
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
    /// Starts the allocation sampler, unless it is already running. A no-op while already active -- use
    /// <see cref="RepaceAllocationLocked"/> to change the budget of a running sampler.
    /// </summary>
    private void StartAllocationLocked(int maxSamplesPerMinute)
    {
        if (_allocationActive)
            return;

        // Deliberately NO equivalent of StartLocked's thread-profiling deferral. That guard exists because the
        // thread sampler suspends the runtime to walk every thread, which must not overlap a thread-profiling
        // session. Allocation sampling walks only the allocating thread and try_locks the same native
        // SuspendMutex, so a tick that collides with either walker is simply dropped -- the enforcement is
        // already in the native tick path, and deferring here would add a state machine that buys nothing.
        try
        {
            // Same optimistic reset the thread sampler's start performs, for the same reason and with the same
            // hazard if omitted: a probe pending from a previous session would otherwise leave the gate stuck
            // true forever (TryResumeSamplingLocked's "neither sampler active" guard means a probe firing while
            // disabled never clears it, and nothing else ever will), so allocation sampling would run with every
            // drain silently gated off.
            var wasBackingOff = ClearSendBackoffForFreshStartLocked();

            // Armed before the call that can throw, and unwound in the catch -- same shape as StartLocked, so
            // a half-started allocation sampler cannot leave the flag lying true with nothing sampling (which
            // would also pin the shared drain timer open forever).
            _allocationActive = true;
            _allocationMaxSamplesPerMinute = maxSamplesPerMinute;

            // Mirror of StartLocked, including the ordering requirement: this start just superseded the probe
            // that would have resumed the thread sampler, so resume it here -- BEFORE this sampler's own
            // throwing native call -- rather than risk leaving it paused forever with _isActive still true.
            // See the equivalent comment in StartLocked for why the order is load-bearing.
            if (wasBackingOff && _isActive)
                ResumeThreadSamplingAfterBackoffLocked();

            // Stop(), never Shutdown(), is the disable -- so Start() here is always legal to call again. See
            // IAllocationSampleSource.Shutdown for why getting that backwards is unrecoverable.
            _allocationSampleSource.Start(maxSamplesPerMinute);

            // Only arm the shared drain timer if nothing has armed it yet: when the thread sampler is running,
            // ITS interval is the authoritative cadence and must not be overwritten by the config interval
            // (which can differ -- an agent command can start the thread sampler at a command-supplied one).
            if (_drainIntervalMs == 0)
                ArmDrainScheduleLocked(_configuration.ContinuousProfilingSamplingIntervalMs);

            Log.Info("[ContinuousProfiling] Allocation sampling started; up to {0} samples/minute.", maxSamplesPerMinute);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to start allocation sampling.");
            StopAllocationLocked();
        }
    }

    /// <summary>
    /// Changes the budget of an ALREADY-RUNNING allocation sampler. Unlike the thread sampler's retune this
    /// needs no stop/start and never touches the shared drain timer: the native sampler's Start is idempotent
    /// and replaces its sub-sampler at the new budget without reopening its session, and the budget is a
    /// per-minute sample cap rather than a drain cadence.
    /// </summary>
    private void RepaceAllocationLocked(int maxSamplesPerMinute)
    {
        try
        {
            _allocationMaxSamplesPerMinute = maxSamplesPerMinute;

            // Deliberately does NOT clear the backoff gate the way a fresh start does: a budget edit is no
            // evidence that a broken send path has recovered, so collapsing a legitimate backoff round on one
            // would be wrong. While backing off, the sampler is intentionally paused and the drain is gated, so
            // re-arming it here would buy real stack walks on customer threads whose output is discarded.
            // Recording the budget is sufficient -- the probe resumes at _allocationMaxSamplesPerMinute.
            if (_sendBackoffActive)
            {
                Log.Debug("[ContinuousProfiling] Allocation budget recorded as {0} samples/minute; it takes effect when sampling resumes after the current send backoff.", maxSamplesPerMinute);
                return;
            }

            _allocationSampleSource.Start(maxSamplesPerMinute);
            Log.Info("[ContinuousProfiling] Allocation sampling re-paced to at most {0} samples/minute.", maxSamplesPerMinute);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to re-pace allocation sampling.");
            StopAllocationLocked();
        }
    }

    private void StopAllocationLocked()
    {
        try
        {
            // Cleared first, for the same reason as in StopLocked: the drain-schedule release is refcounted on
            // this flag.
            _allocationActive = false;
            _allocationMaxSamplesPerMinute = 0;

            DisarmDrainScheduleIfIdleLocked();

            // Stop(), NOT Shutdown(): this runs on every config-driven disable, and the native sampler's
            // Shutdown is a terminal latch that would refuse every later Start for the life of the process.
            _allocationSampleSource.Stop();
            Log.Info("[ContinuousProfiling] Allocation sampling stopped.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to stop allocation sampling.");
        }
        finally
        {
            _allocationActive = false;
            _allocationMaxSamplesPerMinute = 0;
        }
    }

    /// <summary>
    /// A sampler is starting, so the session becomes optimistic again: clears any send-failure backoff state
    /// left over from before this start and supersedes the pending probe. Returns whether a backoff was in fact
    /// in progress -- the caller MUST then resume the other sampler if that sampler is active, because the
    /// superseded probe was the only thing that would ever have done so.
    ///
    /// Shared by BOTH start paths so backoff entry and exit stay symmetric across the two samplers: the trip
    /// (<see cref="TripBackoffAndScheduleProbeLocked"/>) pauses both, so any start that cancels the recovery
    /// owes both a resume.
    /// </summary>
    private bool ClearSendBackoffForFreshStartLocked()
    {
        // Without this, disabling a sampler while a probe is pending and re-enabling it later would leave
        // _sendBackoffActive stuck true forever -- TryResumeSamplingLocked's "neither sampler active" guard
        // means the probe that fires while disabled never clears it, and nothing else ever will.
        //
        // Ints first, then the volatile flag last -- a volatile write publishes everything written
        // before it to another thread's next volatile read of the same field (release semantics). The
        // old order (flag first) published nothing. Matches ResumeAfterReconnect's order.
        _consecutiveSendFailures = 0;
        _backoffIndex = 0;
        // Supersede any probe still pending from a previous session (e.g. a retune's stop/start), so it
        // no-ops instead of resuming sampling this fresh session didn't schedule.
        _backoffGeneration++;

        var wasBackingOff = _sendBackoffActive;
        _sendBackoffActive = false;
        return wasBackingOff;
    }

    /// <summary>
    /// Resumes the allocation sampler that a backoff trip paused, on behalf of a thread-sampler start that has
    /// just superseded the probe. Self-contained error handling on purpose: a failure to resume allocation
    /// sampling must fail only allocation sampling, not unwind the thread-sampler start that called it.
    /// </summary>
    private void ResumeAllocationAfterBackoffLocked()
    {
        try
        {
            _allocationSampleSource.Start(_allocationMaxSamplesPerMinute);

            // Every resume path owns this reset (see TryResumeSamplingLocked/ArmDrainScheduleLocked): without
            // it the first profile after the resume reports a duration spanning the entire paused window --
            // up to the 300s backoff cap -- instead of one real drain interval. Kept here rather than relying
            // on a caller's trailing ArmDrainScheduleLocked, so the guarantee holds no matter who calls this.
            Interlocked.Exchange(ref _lastDrainTimestamp, Stopwatch.GetTimestamp());
            Log.Info("[ContinuousProfiling] Allocation sampling resumed after backoff; up to {0} samples/minute.", _allocationMaxSamplesPerMinute);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to resume allocation sampling after backoff.");
            StopAllocationLocked();
        }
    }

    /// <summary>
    /// The mirror of <see cref="ResumeAllocationAfterBackoffLocked"/>: resumes the paused thread sampler on
    /// behalf of an allocation start that has just superseded the probe, without letting a failure there unwind
    /// the allocation start.
    /// </summary>
    private void ResumeThreadSamplingAfterBackoffLocked()
    {
        try
        {
            _native.Start(_activeIntervalMs);

            // See the note in ResumeAllocationAfterBackoffLocked -- this path is the one that most needs it,
            // since the thread-sample profiles are the time-valued ones whose period/duration is read directly.
            Interlocked.Exchange(ref _lastDrainTimestamp, Stopwatch.GetTimestamp());
            Log.Info("[ContinuousProfiling] Thread sampling resumed after backoff.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to resume thread sampling after backoff.");
            StopLocked();
        }
    }

    /// <summary>
    /// Arms (or retunes) the drain timer both samplers share, and arms the managed-&gt;native trace-context
    /// push. Called after a sampler has actually started, so correlation is only ever armed while something
    /// is sampling.
    /// </summary>
    private void ArmDrainScheduleLocked(int intervalMs)
    {
        if (_drainIntervalMs != intervalMs)
        {
            // IScheduler has no "reschedule", so a cadence change is a stop followed by a fresh registration.
            // StopExecuting does not wait for an in-flight drain, which is exactly the overlap _drainInFlight
            // guards (see that field).
            if (_drainIntervalMs != 0)
                _scheduler.StopExecuting(_drainAction);

            // Marked un-armed for the duration of the swap, so if ExecuteEvery throws, this reads "no timer"
            // rather than still claiming the cadence it just stopped -- otherwise the next Arm call would see a
            // matching interval, skip re-registering, and leave the drain silently dead.
            _drainIntervalMs = 0;
            // trackAsAgentWork: false -- this action IS the CP drain itself (reads the native sample
            // pipeline this very flag exists to annotate). Marking this thread poisons its own read;
            // see follow-up #16 / Scheduler.CreateExecuteEveryTimer.
            _scheduler.ExecuteEvery(_drainAction, TimeSpan.FromMilliseconds(intervalMs), trackAsAgentWork: false);
            _drainIntervalMs = intervalMs;
        }

        Interlocked.Exchange(ref _lastDrainTimestamp, Stopwatch.GetTimestamp());

        // Publish the seam so the wrapper hot path starts pushing the current trace/span on each app thread.
        // Guarded on IsEnabled so the second sampler to start doesn't re-Enable an already-armed context: that
        // would bump the push-change-detection epoch for no reason (nothing cleared the native map), costing
        // one redundant push per app thread.
        //
        // The push target is the THREAD sampler's native seam even when only allocation sampling is running.
        // That is correct, not a shortcut: the native ContinuousProfiler owns the per-thread trace-context map
        // and AllocationSampler reads that same instance, and the native SetTraceContext is unconditional --
        // it does not require the thread sampler's session to be started. Without arming this, allocation
        // samples would silently lose all trace/span correlation whenever the thread sampler is stopped.
        if (!_continuousProfilingContext.IsEnabled)
        {
            _continuousProfilingContext.Enable(_native);
            ContinuousProfilingContext.Instance = _continuousProfilingContext;
        }
    }

    /// <summary>
    /// Releases the shared drain timer and the trace-context seam, but only once NEITHER sampler is running.
    /// Callers clear their own active flag first (see <see cref="StopLocked"/>/<see cref="StopAllocationLocked"/>).
    /// </summary>
    private void DisarmDrainScheduleIfIdleLocked()
    {
        if (_isActive || _allocationActive)
            return; // the other sampler still needs the shared drain

        // Disarm correlation first so the wrapper hot path stops pushing before native sampling stops.
        // Restore the inert default instance so IsEnabled is false again everywhere.
        _continuousProfilingContext.Disable();
        ContinuousProfilingContext.Instance = new ContinuousProfilingContext();

        if (_drainIntervalMs != 0)
        {
            _scheduler.StopExecuting(_drainAction);
            _drainIntervalMs = 0;
        }
    }

    /// <summary>
    /// Drains at most one batch and ships it. Catches everything: a drain failure must never surface
    /// in the instrumented application.
    /// </summary>
    public void DrainOnce()
    {
        // Lock-free volatile read (this path deliberately doesn't take _lifecycleLock -- see the locking-
        // posture note above). Dispose has joined the native worker thread, so a drain landing after it
        // would P/Invoke into a dead sampler and ship a profile through an already-disposed reporter.
        if (_disposed)
            return;

        if (Interlocked.CompareExchange(ref _drainInFlight, 1, 0) != 0)
            return; // another drain is already in flight (retune overlap) -- skip this tick rather than race the shared buffer

        try
        {
            try
            {
                // Nowhere to send yet: skip read/parse/build entirely rather than doing the work and
                // dropping the result. Native sampling still runs (StartLocked already started it,
                // decoupled from connect); only the managed drain is deferred.
                //
                // _sendBackoffActive: sampling itself is paused (native Stop()'d) while backing off, so this
                // is mostly a cheap no-op guard against the recurring timer's own ticks in the meantime.
                if (!_isConnected || _sendBackoffActive)
                    return;

                // Both sample types are read into the SAME buffer, sequentially. Deliberate: the buffer is
                // sized for native's 4 MB thread-batch cap, and a second dedicated buffer would double that
                // permanent per-process footprint for allocation batches native caps at 64 KB
                // (AllocationSampler::MaxAllocationBufferBytes). It is safe because the reads are strictly
                // sequential and each parse fully materializes its results before the next read overwrites the
                // bytes -- BufferParser copies every string out (Encoding.Unicode.GetString), so no parsed
                // object aliases the buffer -- and because _drainInFlight already makes this the only drain
                // touching it.
                var samples = EmptyThreadSamples;
                if (TryReadIntoDrainBuffer(_sampleSource, out var bytesRead))
                {
                    samples = BufferParser.Parse(_drainBuffer, bytesRead, out var batchStats);

                    // Surface the native BatchStats for CP overhead/fidelity analysis (and OTel FinalStats parity):
                    // microsSuspended = the stop-the-world window this sweep; skipped = threads/frames the walk missed.
                    // onCpu/total is the live signal that the native on-CPU classification is working, since NR CP
                    // is no-send-guarded and has no other observation path for it.
                    if (batchStats != null && Log.IsFinestEnabled)
                        Log.Finest("[ContinuousProfiling] batch stats: microsSuspended={0} threads={1} frames={2} skipped={3} onCpu={4}/{5}",
                            batchStats.MicrosSuspended, batchStats.Threads, batchStats.Frames, batchStats.Skipped, CountOnCpu(samples), samples.Count);
                }

                // Gated on _allocationActive, unlike the thread read above: the drain timer can be armed by
                // the thread sampler alone, and in that (default) case an ungated read would P/Invoke into a
                // never-started allocation sampler on every single tick for the life of the process.
                var allocationSamples = EmptyAllocationSamples;
                if (_allocationActive && TryReadIntoDrainBuffer(_allocationSampleSource, out var allocationBytesRead))
                    BufferParser.Parse(_drainBuffer, allocationBytesRead, out _, out allocationSamples);

                // Either sample type alone is worth a payload: an allocation-only sweep is normal whenever the
                // thread sampler is stopped, and a thread-only sweep is the common case. Only a sweep that
                // produced neither is dropped.
                if (samples.Count == 0 && allocationSamples.Count == 0)
                    return;

                var now = Stopwatch.GetTimestamp();
                var durationNano = ElapsedNanos(Interlocked.Read(ref _lastDrainTimestamp), now);
                // The window ends now; it started durationNano ago -- not the reverse. Getting this backwards
                // makes every profile appear to cover [now, now + duration) instead of the interval actually
                // sampled, [now - duration, now).
                var startUnixNano = ToUnixNano(DateTime.UtcNow) - durationNano;
                Interlocked.Exchange(ref _lastDrainTimestamp, now);

                // The THREAD sampling interval (ms) is the profile's period; convert to nanoseconds for
                // period_type=cpu/ns. Zero when the thread sampler is stopped, which is what suppresses the
                // cpu/off_cpu profiles on an allocation-only sweep; the allocation profiles carry no period
                // (they are event-driven, not time-valued) and so are emitted regardless.
                var periodNanos = (long)_activeIntervalMs * 1_000_000L;
                // Exclude the agent's own threads/frames unless the undocumented appSettings opt-in is set.
                // Both sample types go into ONE request, so a drain is still one wire payload.
                var request = OtlpProfileBuilder.Build(samples, startUnixNano, durationNano, ServiceName, periodNanos, _configuration.ContinuousProfilingIncludeAgentCode, allocationSamples);

                bool sent;
                try
                {
                    sent = _transport.Send(request);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[ContinuousProfiling] Send threw; treating as a failed send.");
                    sent = false;
                }
                OnSendResult(sent);

                // A dropped profile isn't a healthy drain: only count Drain/Samples when the send was actually
                // accepted, and route a failure to the same error metric the other defensive branches use.
                if (sent)
                {
                    _agentHealthReporter.ReportSupportabilityCountMetric(SupportabilityDrainMetric);

                    // Each count is reported only when that sample type actually contributed, so an
                    // allocation-only sweep doesn't emit a meaningless "0 thread samples" data point (and vice
                    // versa).
                    if (samples.Count > 0)
                        _agentHealthReporter.ReportSupportabilityCountMetric(SupportabilitySamplesMetric, samples.Count);
                    if (allocationSamples.Count > 0)
                        _agentHealthReporter.ReportSupportabilityCountMetric(SupportabilityAllocationSamplesMetric, allocationSamples.Count);
                }
                else
                {
                    SafeReportError();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ContinuousProfiling] Drain failed.");
                SafeReportError();
            }
        }
        finally
        {
            // Interlocked, matching the acquire side's CompareExchange above -- a plain store is not a
            // full barrier on every platform (arm64 permits reordering a plain store ahead of the writes
            // this drain just did to _drainBuffer), which could let the next tick's CompareExchange
            // succeed before those writes are visible to it. x86/x64 TSO happens to mask this, which is
            // why a plain store here was never observed to fail.
            Interlocked.Exchange(ref _drainInFlight, 0);
        }
    }

    /// <summary>
    /// Reads one batch from <paramref name="source"/> into the shared <see cref="_drainBuffer"/> and validates
    /// its length. Returns false -- with <paramref name="bytesRead"/> zeroed -- when there was nothing to read
    /// or the batch must be discarded, so a caller can never parse an unvalidated length.
    /// </summary>
    private bool TryReadIntoDrainBuffer(ISampleSource source, out int bytesRead)
    {
        bytesRead = source.ReadBatch(_drainBuffer);

        if (bytesRead <= 0)
        {
            bytesRead = 0;
            return false;
        }

        // Defensive clamp: a misbehaving native source could report more bytes than the buffer
        // holds. Never trust it far enough to hand an out-of-range length to BufferParser.Parse,
        // which would walk off the end of _drainBuffer.
        if (bytesRead > _drainBuffer.Length)
        {
            Log.Debug("[ContinuousProfiling] ReadBatch reported {0} bytes, exceeding the {1}-byte buffer; discarding this drain.", bytesRead, _drainBuffer.Length);
            SafeReportError();
            bytesRead = 0;
            return false;
        }

        // The managed buffer matches native's own cap (see DrainBufferSize), so this can't fire
        // today -- native already truncates to at most that many bytes before ReadThreadSamples
        // ever copies. It's a tripwire against the two constants drifting apart again: if native's
        // cap is ever raised past ours without a matching change here, this is what would catch
        // the resulting silent loss of BatchStats/tail samples instead of shipping corrupted data.
        if (bytesRead >= _drainBuffer.Length)
        {
            Log.Debug("[ContinuousProfiling] ReadBatch filled the entire {0}-byte drain buffer; the batch may have been truncated.", _drainBuffer.Length);
            _agentHealthReporter.ReportSupportabilityCountMetric(SupportabilityDrainBufferBoundaryMetric);
        }

        return true;
    }

    /// <summary>
    /// Tracks consecutive send failures. The <see cref="SendFailureGraceCount"/> grace (tolerate one blip)
    /// applies only the FIRST time a failure streak starts (<see cref="_backoffIndex"/> == 0) -- once
    /// already escalated by a prior trip, a single failure re-trips immediately rather than paying the
    /// grace again every retry, which would double the cost of every backoff round for no benefit. A
    /// single success fully resets both the failure count and the backoff index -- a dropped profile
    /// can't be recovered, so there's no reason to stay pessimistic once sending works again.
    /// </summary>
    private void OnSendResult(bool sent)
    {
        // Under _lifecycleLock: this mutates the same _consecutiveSendFailures/_backoffIndex/
        // _sendBackoffActive state StartLocked/EndBackoffProbe/ResumeAfterReconnect write under that lock.
        // Unlocked, a reconnect landing between the gate-set and the native stop in the trip below leaves
        // native stopped with the gate already cleared -- DrainOnce then passes its gate forever with
        // nothing sampling, so ReadBatch returns 0 every tick and this never runs again to recover.
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            if (sent)
            {
                _consecutiveSendFailures = 0;
                _backoffIndex = 0;
                return;
            }

            _consecutiveSendFailures++;

            var graceCount = _backoffIndex == 0 ? SendFailureGraceCount : 1;
            if (_consecutiveSendFailures < graceCount)
                return;

            var failuresAtTrip = _consecutiveSendFailures;
            _consecutiveSendFailures = 0; // grace consumed; the next round starts fresh
            TripBackoffAndScheduleProbeLocked(failuresAtTrip);
        }
    }

    /// <summary>
    /// Pauses native sampling (no stop-the-world cost while paused) and schedules a single probe at the
    /// current <see cref="SendBackoffSequence"/> step. The recurring drain timer keeps ticking throughout
    /// (each tick is a cheap no-op via the <see cref="_sendBackoffActive"/> gate in <see cref="DrainOnce"/>)
    /// so no second timer is needed to pick the real send back up once the probe resumes sampling.
    ///
    /// The caller (<see cref="OnSendResult"/>) already holds <see cref="_lifecycleLock"/> -- the *Locked
    /// suffix matches this file's convention (<c>StartLocked</c>/<c>StopLocked</c>).
    /// </summary>
    private void TripBackoffAndScheduleProbeLocked(int failuresAtTrip)
    {
        _sendBackoffActive = true;
        _native.Stop();

        // Pause allocation sampling too: both sample types ride the one request that just failed, so there is
        // nothing to gain from continuing to pay for allocation stack walks on customer threads while the drain
        // is gated off. Stop(), never Shutdown() -- the probe below has to be able to resume it.
        if (_allocationActive)
            _allocationSampleSource.Stop();

        // Open a new backoff round and tag the probe with its generation. Any probe from a prior round
        // (which IScheduler cannot cancel) is now stale and will no-op when it fires -- otherwise it could
        // resume sampling and clear _sendBackoffActive in the middle of this round, collapsing it early.
        var generation = ++_backoffGeneration;
        var delay = SendBackoffSequence[_backoffIndex];
        Log.Info("[ContinuousProfiling] {0} consecutive send failures; pausing sampling, retrying in {1}s.",
            failuresAtTrip, delay.TotalSeconds);
        _scheduler.ExecuteOnce(() => EndBackoffProbeIfCurrent(generation), delay);
        _backoffIndex = Math.Min(_backoffIndex + 1, SendBackoffSequence.Length - 1);
    }

    /// <summary>
    /// Re-checks <see cref="_isActive"/> and resumes native sampling under <see cref="_lifecycleLock"/> --
    /// the same lock <see cref="StartLocked"/>/<see cref="StopLocked"/> use, so a config-driven disable (or
    /// <see cref="Dispose"/>) racing a pending probe can no longer resurrect sampling with a stale/zeroed
    /// <see cref="_activeIntervalMs"/> after teardown. Also resets <see cref="_lastDrainTimestamp"/> so the
    /// first post-resume profile's duration doesn't span the whole paused window. Returns whether it
    /// actually resumed (false if the session was disabled while backing off, in which case it stays
    /// stopped -- reviving a session the config no longer wants would be wrong).
    /// </summary>
    private bool TryResumeSamplingLocked()
    {
        // Resume whatever is still wanted -- either sampler alone is enough to make resuming worthwhile, and
        // only "neither" means the session was disabled while backing off and must stay stopped.
        if (!_isActive && !_allocationActive)
            return false;

        if (_isActive)
            _native.Start(_activeIntervalMs);

        // Re-pacing at the same budget: the native sampler's Start re-arms the handler and resets the
        // sub-sampler without reopening its session, which is exactly right after a paused window.
        if (_allocationActive)
            _allocationSampleSource.Start(_allocationMaxSamplesPerMinute);

        Interlocked.Exchange(ref _lastDrainTimestamp, Stopwatch.GetTimestamp());
        return true;
    }

    /// <summary>
    /// Scheduled by <see cref="TripBackoffAndScheduleProbeLocked"/> with the generation of the backoff round
    /// that scheduled it. No-ops if that round is no longer current (a later trip, a reconnect resume, or a
    /// session (re)start has since bumped <see cref="_backoffGeneration"/>) -- IScheduler cannot cancel the
    /// stale timer, so this generation check is what prevents it from resuming sampling and clearing
    /// <see cref="_sendBackoffActive"/> in the middle of a different round. Otherwise deliberately leaves
    /// <see cref="_consecutiveSendFailures"/>/<see cref="_backoffIndex"/> alone -- if this probe's resumed
    /// send also fails, <see cref="OnSendResult"/> continues the escalation from where the trip left the
    /// index, rather than starting over.
    /// </summary>
    private void EndBackoffProbeIfCurrent(int generation)
    {
        bool resumed;
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            // Stale probe from a superseded round -- the round that scheduled it is gone, so resuming now
            // would clear the gate for a round it has no business ending.
            if (generation != _backoffGeneration)
                return;

            resumed = TryResumeSamplingLocked();
            if (resumed)
                _sendBackoffActive = false;
        }

        if (resumed)
            Log.Info("[ContinuousProfiling] Resuming sampling after backoff.");
    }

    /// <summary>
    /// Called from <see cref="OnAgentConnected"/> when a (re)connect arrives while backing off. Unlike
    /// <see cref="EndBackoffProbeIfCurrent"/>, this fully resets the backoff state -- same as a successful send --
    /// because the reconnect itself is the likely fix, and there's no reason to make CP wait out the rest
    /// of a delay picked for a problem that may no longer exist.
    /// </summary>
    private void ResumeAfterReconnect()
    {
        bool resumed;
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            resumed = TryResumeSamplingLocked();
            if (resumed)
            {
                _consecutiveSendFailures = 0;
                _backoffIndex = 0;
                // Supersede the pending probe from the round this reconnect is ending early, so it can't
                // fire later and collapse a subsequent backoff round.
                _backoffGeneration++;
                _sendBackoffActive = false;
            }
        }

        if (resumed)
            Log.Info("[ContinuousProfiling] Reconnected while backing off; resuming sampling immediately.");
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
            _disposed = true;

            if (_isActive)
                StopLocked();

            if (_allocationActive)
                StopAllocationLocked();

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

            // The ONLY place the allocation sampler may be shut down. Its native Shutdown closes the EventPipe
            // session, drains any in-flight tick handler, and then LATCHES: every subsequent Start is refused
            // for the life of the process. That is why nothing else in this class calls it -- a disable, a
            // retune or a backoff pause must use Stop, or allocation sampling would end permanently the first
            // time it was toggled. Unconditional (like _native.Shutdown above) so the native session is closed
            // deterministically even if this service never started sampling, and separately try/caught so a
            // failure in one shutdown cannot skip the other.
            try
            {
                _allocationSampleSource.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ContinuousProfiling] Failed to shut down the native allocation sampler.");
            }
        }

        base.Dispose();
    }
}
