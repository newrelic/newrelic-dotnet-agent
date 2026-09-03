// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport.ContinuousProfiling;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.ThreadProfiling;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Owns the continuous-profiling session lifecycle: starts/stops/retunes the native sampler and
/// drain schedule based on configuration. Drains are gated on <see cref="OnAgentConnected"/> (the
/// profiles endpoint is only known post-preconnect). Repeated send failures pause native sampling
/// and retry via a single-attempt backoff probe (see <see cref="OnSendResult"/>); a reconnect
/// resumes immediately instead of waiting out the remaining delay (see <see cref="ResumeAfterReconnect"/>).
/// </summary>
public class ContinuousProfilingService : ConfigurationBasedService, IContinuousProfilingSessionControl, IContinuousProfilingCommandTarget
{
    // Must be >= native's MaxBufferBytes (Profiler/ContinuousProfiler/ContinuousProfiler.h) -- native
    // already caps a batch at that ceiling (truncate + count on its own side), but ReadThreadSamples
    // copies min(available, len) and frees the native slot regardless of fit, so a smaller managed
    // buffer here would silently lose the tail of any batch between the two sizes (BatchStats is
    // written last, so it's the first casualty). If you change either constant, check the other file.
    private const int DrainBufferSize = 4 * 1024 * 1024;

    // Retry interval for a start deferred by an in-flight thread-profiling session; modest since
    // those sessions are short and time-boxed.
    private static readonly TimeSpan DeferredStartRetryInterval = TimeSpan.FromSeconds(15);

    private const string SupportabilityDrainMetric = "Supportability/DotNET/ContinuousProfiling/Drain";
    private const string SupportabilitySamplesMetric = "Supportability/DotNET/ContinuousProfiling/Samples";
    private const string SupportabilityErrorMetric = "Supportability/DotNET/ContinuousProfiling/Error";
    private const string SupportabilityDrainBufferBoundaryMetric = "Supportability/DotNET/ContinuousProfiling/DrainBufferBoundary";

    // Send-failure backoff schedule, matching ConnectionManager's reconnect backoff values. Unlike that
    // sequence, this one resets fully to index 0 on a single successful send -- a dropped profile can't
    // be recovered later like a held-over harvest cycle can.
    private static readonly TimeSpan[] SendBackoffSequence = new[]
    {
        TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(300)
    };

    // Consecutive failures tolerated before pausing sampling, but only the first time a streak starts
    // (_backoffIndex == 0); an already-escalated streak re-trips on a single failure.
    private const int SendFailureGraceCount = 2;

    // 1970-01-01T00:00:00Z in DateTime ticks; netstandard2.0 has no DateTime.UnixEpoch.
    private static readonly long UnixEpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    private readonly ISampleSource _sampleSource;
    private readonly INativeContinuousProfiler _native;
    private readonly IProfilesTransport _transport;
    private readonly IScheduler _scheduler;
    private readonly IAgentHealthReporter _agentHealthReporter;

    // volatile: set on the event-bus thread (OnAgentConnected), read lock-free by DrainOnce. Monotonic
    // true after first connect; never reset on a later disconnect.
    private volatile bool _isConnected;

    // _sendBackoffActive: DrainOnce's gate check is its only lock-free read; every write is under
    // _lifecycleLock (see the locking-posture note on that field below). _consecutiveSendFailures/
    // _backoffIndex are likewise always accessed under _lifecycleLock, so no volatile is needed there.
    private volatile bool _sendBackoffActive;
    private int _consecutiveSendFailures;
    private int _backoffIndex;

    // Generation counter for the current backoff round, bumped under _lifecycleLock whenever a round
    // starts, is superseded, or abandoned. IScheduler can't cancel a scheduled probe, so a stale probe
    // checks its captured generation against this before resuming sampling.
    private int _backoffGeneration;

    // Managed->native trace-context push seam. Armed while a session is active (published as the process-wide
    // ContinuousProfilingContext.Instance so the wrapper hot path can reach it), disarmed when it stops.
    private readonly ContinuousProfilingContext _continuousProfilingContext = new ContinuousProfilingContext();

    // Stable delegate reference: ExecuteEvery and StopExecuting must be handed the same instance.
    private readonly Action _drainAction;

    // Task tracking whichever drain most recently won the _drainInFlight guard below; published only
    // from inside DrainOnce after that CompareExchange succeeds. A tick that loses the guard must return
    // without touching this field, or it would overwrite the handle to the real in-flight drain with an
    // already-completed skip-task. StopLocked reads it to wait for an in-flight drain before tearing down
    // native sampling -- see _drainShutdownWaitTimeout. Interlocked.Exchange, not a plain assignment,
    // since concurrent ticks can race this write for the guard itself.
    private Task _lastDrainTask = Task.CompletedTask;

    // Bounds StopLocked's wait for an in-flight drain. Sized above OtlpProfilesHttpDispatcher's
    // TotalSendTimeoutWithRetries (45s) with margin for the read/parse/build steps and scheduling
    // jitter; a timeout here is logged and teardown proceeds anyway. Test-injectable via the
    // constructor's optional parameter so unit tests don't have to wait out the real 60s default.
    private readonly TimeSpan _drainShutdownWaitTimeout;

    // Single reused drain buffer, allocated on session start (StartLocked) and released on stop
    // (StopLocked) so a process that never enables continuous profiling pays nothing -- this is a
    // multi-MB Large Object Heap block (see DrainBufferSize) and the feature is off by default.
    // Overlapping drains would tear it, so DrainOnce is guarded by _drainInFlight below. volatile:
    // written under _lifecycleLock in StartLocked/StopLocked, read lock-free by DrainOnce on a pool
    // thread (which snapshots it into a local exactly once -- see DrainOnce).
    private volatile byte[] _drainBuffer;

    // Interlocked reentrancy guard: 0 = idle, 1 = in flight. Normally DrainOnce can't re-enter itself
    // (the Scheduler disarms its timer for the callback's duration), but a retune's StopExecuting-
    // without-wait followed immediately by a new timer (see ApplyConfigChange) can let an old,
    // still-in-flight drain overlap the new timer's first tick -- both would otherwise race the single
    // shared _drainBuffer.
    private int _drainInFlight;

    // Locking posture (deliberately minimal -- this type runs inside every instrumented process):
    //   * _lifecycleLock is the ONLY lock, guarding lifecycle transitions (StartIfEnabled/
    //     ApplyConfigChange/Dispose) and the backoff state they share with the drain path
    //     (OnSendResult). *Locked-suffixed methods run under it ("caller holds the lock").
    //   * DrainOnce's gate check is a lock-free volatile read; OnSendResult is the only other
    //     drain-path code that takes _lifecycleLock. Contention is nil in steady state -- the only
    //     other contenders are config changes and teardown.
    //   * StopLocked bounds its wait for an in-flight drain via drainTask.Wait(...). Holding
    //     _lifecycleLock for the WHOLE wait would self-deadlock against that drain's own OnSendResult
    //     (which also takes the lock past its _stopSignaled fast-exit), so StopLocked temporarily drops
    //     it via Monitor.Exit/Monitor.Enter (not the `lock` statement) for exactly the wait's duration --
    //     legal because Monitor supports same-thread recursive acquire/release, so every caller's own
    //     `lock (_lifecycleLock) { StopLocked(); }` continues to work unchanged. _stopInProgress guards
    //     a second thread's Stop-triggering call landing in that reopened window: it waits for the first
    //     stop's real completion, then re-checks _isActive and performs its own stop if a start slipped
    //     in while it waited (see StopLocked).
    //   * Lock order is always _lifecycleLock -> ProfilingMutualExclusionGate.Acquire() -> Scheduler's
    //     internal semaphore, never the reverse.
    private readonly object _lifecycleLock = new object();

    // Lock-free early-exit for OnSendResult while a stop/retune tears down. Volatile: set under
    // _lifecycleLock near the top of StopLocked (before its bounded drain wait) and reset in
    // StartLocked; read lock-free by OnSendResult. Optimization only -- StopLocked's Monitor.Exit/Enter
    // drop (see the locking-posture note above) is what actually prevents the deadlock; this just saves
    // a stopping drain the cost of acquiring the lock.
    //
    // Deliberately not _isActive: _isActive only goes false once _native.Stop() has actually finished
    // (see StopLocked's finally), which ThreadProfilingService's forward guard depends on -- flipping it
    // any earlier would let a thread-profiling session start while CP's native sampler is still live.
    private volatile bool _stopSignaled;

    // Coordinates a second concurrent StopLocked call landing while another thread's StopLocked has
    // dropped _lifecycleLock during its bounded drain wait (see StopLocked). Null when no stop is in
    // progress. Plain field: every access happens while holding _lifecycleLock or immediately after
    // reacquiring it via Monitor.Enter, which is a full memory barrier.
    private TaskCompletionSource<bool> _stopInProgress;

    // volatile: read lock-free by ThreadProfilingService's forward guard on another thread; written
    // under _lifecycleLock on the scheduler thread.
    private volatile bool _isActive;

    // volatile: set under _lifecycleLock by Dispose; every lock-holding entry point checks it right
    // after acquiring the lock, so a deferred callback landing after Dispose (the retry timer, a
    // queued OnConfigurationUpdated) becomes a no-op instead of restarting a sampler Dispose already
    // joined.
    private volatile bool _disposed;

    // volatile: written under _lifecycleLock, read lock-free by DrainOnce -- without it DrainOnce
    // could read a stale 0 and emit a profile with period=0.
    private volatile int _activeIntervalMs;

    // Profile-type tokens ("cpu") currently owned by an agent command rather than config; ApplyConfigChange
    // must not start/stop/retune a type present here (see StartFromCommand/StopFromCommand). A set, not a
    // bool, because the command spec is per-type -- only "cpu" exists today, but this generalizes without
    // another redesign of the guard.
    private readonly HashSet<string> _commandControlledTypes = new HashSet<string>();

    // Profile-type tokens explicitly stopped via an agent command; ApplyConfigChange must not restart one
    // of these on config's say-so alone, or an operator's stop_continuous_profiler command would be
    // silently undone by the very next ConfigurationUpdatedEvent (e.g. a reconnect). Cleared only by a
    // matching start command (see StartFromCommand) -- a process restart clears it implicitly, since this
    // is in-memory instance state.
    private readonly HashSet<string> _commandStoppedTypes = new HashSet<string>();

    private static readonly IReadOnlyDictionary<string, string> EmptyCommandExceptions = new Dictionary<string, string>();

    // Mirrors DefaultConfiguration's ContinuousProfilingSamplingIntervalMs clamp (currently 1000/60000),
    // duplicated because a command-supplied interval never flows through IConfiguration.
    private const int MinCommandIntervalMs = 1000;
    private const int MaxCommandIntervalMs = 60000;

    // Accessed via Interlocked.Read/Exchange: written from DrainOnce's scheduler thread and from
    // EndBackoffProbeIfCurrent/ResumeAfterReconnect, so a plain read/write could tear on 32-bit.
    private long _lastDrainTimestamp;

    // While backing off from repeated send failures, native sampling is stopped (see
    // TripBackoffAndScheduleProbeLocked) so CP is not really consuming the suspend-mutex resource --
    // report false here so the thread-profiler's mutual-exclusion guard doesn't refuse a
    // start_profiler command for the full backoff window (up to SendBackoffSequence's max) while CP
    // produces nothing. Internal logic that needs "session is armed" reads the raw _isActive field.
    public bool IsActive => _isActive && !_sendBackoffActive;

    /// <summary>
    /// Thread profiler's session state, wired post-construction by <c>AgentManager</c> (settable to
    /// avoid a constructor cycle with the thread-profiling service). Continuous profiling defers its
    /// start while a thread-profiling session is in-flight; null means no seam wired, so no deferral.
    /// </summary>
    /// <remarks>
    /// Backed by a volatile field: the post-construction wiring in AgentManager.Initialize runs on the
    /// startup thread with no lock, while the reads in StartIfEnabled/ResumeAfterBackoff happen on the
    /// scheduler/command threads under ProfilingMutualExclusionGate.Acquire(). Without the release/acquire
    /// those reads could see a stale null, skip the "thread profiling is active" guard, and let both
    /// profilers arm concurrently. A property can't be marked volatile, so it wraps a volatile field.
    /// </remarks>
    public IThreadProfilingStatus ThreadProfilingStatus
    {
        get => _threadProfilingStatus;
        set => _threadProfilingStatus = value;
    }
    private volatile IThreadProfilingStatus _threadProfilingStatus;

    // Test-only observation seams. Null (a no-op) in production; never set outside unit tests. They make
    // three otherwise-invisible internal timing moments awaitable so the concurrency/dispose tests can
    // position a racy interleaving against a DETERMINISTIC barrier instead of a wall-clock Thread.Sleep --
    // a too-short sleep on a constrained runner would silently skip the intended race and still pass green
    // (lost regression coverage, not a visible flake). Same spirit as the drainShutdownWaitTimeout
    // constructor seam above: observation only, no production behavior change. Public (not internal +
    // InternalsVisibleTo, which this repo bans) and nullable so the null-conditional invoke is a cheap
    // null check on these paths.

    // Invoked by DrainOnce the instant a tick loses the _drainInFlight guard and is about to return as a
    // no-op. Lets a test await "all N guard-losing ticks have actually run and returned" rather than
    // sleeping and hoping.
    public Action DrainTickLostGuardForTesting { get; set; }

    // Invoked by StopLocked the instant a thread has dropped _lifecycleLock to enter its PRIMARY bounded
    // drain wait. Lets a test await "this stopper is now in the bounded wait (lock released)" before it
    // unblocks a racing drain that must reacquire the lock.
    public Action EnteredPrimaryDrainWaitForTesting { get; set; }

    // Invoked by StopLocked the instant a SECOND concurrent caller has dropped _lifecycleLock to park on
    // the _stopInProgress wait. Lets a test await "the second stopper is parked on the in-progress stop"
    // before it lets the first stop complete.
    public Action EnteredStopInProgressWaitForTesting { get; set; }

    // Test-only observation of the lazy drain-buffer lifecycle: false before a session starts and after it
    // stops (the LOH block is released), true while a session is armed. Lets the H2 lazy-allocation tests
    // assert a disabled/stopped process holds no buffer without widening any other member.
    public bool IsDrainBufferAllocatedForTesting => _drainBuffer != null;

    public ContinuousProfilingService(ISampleSource sampleSource, INativeContinuousProfiler native, IProfilesTransport transport, IScheduler scheduler, IAgentHealthReporter agentHealthReporter, TimeSpan? drainShutdownWaitTimeout = null)
    {
        _sampleSource = sampleSource;
        _native = native;
        _transport = transport;
        _scheduler = scheduler;
        _agentHealthReporter = agentHealthReporter;
        _drainShutdownWaitTimeout = drainShutdownWaitTimeout ?? TimeSpan.FromSeconds(60);

        // ExecuteEvery pauses the recurring timer only until this delegate returns; dispatching via
        // Task.Run frees the shared Scheduler thread immediately so a slow send never blocks
        // harvest/samplers/health-reporter. Doesn't touch _lastDrainTask here -- only DrainOnce
        // publishes to it, after winning the _drainInFlight guard.
        _drainAction = () => Task.Run(DrainOnce);

        _subscriptions.Add<AgentConnectedEvent>(OnAgentConnected);
    }

    /// <summary>
    /// Resolves the profiles endpoint from the collector's connection (post-preconnect) and arms
    /// <see cref="_isConnected"/> so drains start doing real work. Before this fires, <see cref="DrainOnce"/>
    /// drops every tick without touching the native sample buffer -- there is nowhere to send to yet.
    /// </summary>
    private void OnAgentConnected(AgentConnectedEvent agentConnectedEvent)
    {
        // Dispose only unsubscribes via base.Dispose(), so a connect can land after Dispose; skip it.
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

        // A (re)connect is itself evidence the send path may have changed; don't make CP wait out an
        // unrelated backoff window.
        if (_sendBackoffActive)
            ResumeAfterReconnect();
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
            if (_disposed)
                return;

            // An agent command owns the cpu bundle until a matching stop command or restart -- an
            // incidental config-update event must not override an operator's explicit command.
            if (_commandControlledTypes.Contains(ContinuousProfilingCommandTypes.Cpu))
                return;

            // A command-issued stop suppresses config-driven starts for that type until a matching start
            // command clears it -- otherwise this very reconciliation would undo the operator's stop the
            // moment a reconnect (or any other config-update event) fires.
            var enabled = _configuration.ContinuousProfilingEnabled && !_commandStoppedTypes.Contains(ContinuousProfilingCommandTypes.Cpu);

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
                // HSM disables CP unconditionally and that must hold for the command path too, even
                // though the command path otherwise bypasses ContinuousProfilingEnabled. Check before
                // touching _commandControlledTypes -- a rejected start must not claim ownership, or a
                // later legitimate start would find it locked out with no stop able to release it.
                if (_configuration.HighSecurityModeEnabled)
                {
                    exceptions[ContinuousProfilingCommandTypes.Cpu] = "not supported: high security mode enabled";
                }
                else
                {
                    _commandControlledTypes.Add(ContinuousProfilingCommandTypes.Cpu);
                    // An explicit start command is the only thing that lifts a prior stop command's
                    // suppression (see ApplyConfigChange) -- otherwise a stop followed by a start would
                    // leave the type unable to ever restart via config again.
                    _commandStoppedTypes.Remove(ContinuousProfilingCommandTypes.Cpu);

                    if (!_isActive)
                    {
                        var requested = cpuReportIntervalMs ?? sampleIntervalMs ?? _configuration.ContinuousProfilingSamplingIntervalMs;
                        var clamped = Math.Min(MaxCommandIntervalMs, Math.Max(MinCommandIntervalMs, requested));
                        StartLocked(clamped, () => RetryCommandStart(clamped), out var startError);
                        if (startError != null)
                            exceptions[ContinuousProfilingCommandTypes.Cpu] = startError;
                    }
                    // else: already running -- idempotent no-op per spec; a repeat start does not retune.
                }
            }

            return BuildCommandResultLocked(exceptions);
        }
    }

    // Deferred-start retry for a command-driven start. Runs as the Scheduler's callback with no lock
    // held (unlike the *Locked-suffixed helpers below, which all assume the caller already holds
    // _lifecycleLock) -- hence no "Locked" suffix -- so it takes _lifecycleLock itself before re-entering
    // StartLocked directly rather than ApplyConfigChange, which would just return on the
    // _commandControlledTypes ownership check this call is exempt from. Re-checks ownership and
    // _disposed since this runs asynchronously from whatever StopFromCommand/Dispose did while the
    // retry was pending.
    private void RetryCommandStart(int intervalMs)
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            // A matching stop command released ownership while this retry was pending -- the operator
            // no longer wants cpu profiling running, so don't resurrect it.
            if (!_commandControlledTypes.Contains(ContinuousProfilingCommandTypes.Cpu))
                return;

            if (_isActive)
                return;

            // Failure here is only logged, not surfaced to a command response: this retry runs on the
            // scheduler's own callback, long after StartFromCommand's synchronous response was already sent.
            StartLocked(intervalMs, () => RetryCommandStart(intervalMs), out _);
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
            if (_disposed)
                return BuildCommandResultLocked(EmptyCommandExceptions);

            var exceptions = new Dictionary<string, string>();

            // An empty/absent include on a STOP command means "stop everything currently active or
            // command-controlled" -- asymmetric with START, where an empty include is a no-op query (see
            // StartFromCommand). Today that's just the cpu bundle, the only supported command-controlled
            // type, so there is nothing to classify.
            var stopCpuBundle = requestedTypes.Count == 0;

            if (!stopCpuBundle)
            {
                foreach (var token in requestedTypes)
                {
                    ContinuousProfilingCommandTypes.Classify(token, out var startsCpuBundle, out var requestsHeap);
                    stopCpuBundle |= startsCpuBundle;

                    if (requestsHeap)
                        exceptions[ContinuousProfilingCommandTypes.Heap] = "not supported";
                    else if (!startsCpuBundle)
                        exceptions[token] = "not supported"; // unrecognized token
                }
            }

            if (stopCpuBundle)
            {
                // Release command ownership regardless of whether it was actually active -- a stop always
                // hands the type back to config control, matching "stop while not profiling is a no-op
                // success". Record the suppression too, so config can't silently undo this stop (see
                // ApplyConfigChange); only a matching start command lifts it.
                _commandControlledTypes.Remove(ContinuousProfilingCommandTypes.Cpu);
                _commandStoppedTypes.Add(ContinuousProfilingCommandTypes.Cpu);

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

    private void StartLocked(int intervalMs) => StartLocked(intervalMs, ApplyConfigChange, out _);

    // deferredRetryAction: config-driven callers (StartIfEnabled, a retune) pass the default
    // ApplyConfigChange overload above to re-check live config on retry. StartFromCommand passes
    // RetryCommandStart instead, since ApplyConfigChange's ownership check would make a
    // command-driven retry a permanent no-op.
    //
    // startError: null on success (including a deferred-by-thread-profiling no-op, which isn't a
    // failure); set to the failure's message when either the native start (inside the Gate) or the
    // post-Gate schedule/seam setup throws. Only StartFromCommand's synchronous caller reads this -- a
    // deferred retry's failure has no command response left to attach to, so it stays log-only (see
    // RetryCommandStart).
    private void StartLocked(int intervalMs, Action deferredRetryAction, out string startError)
    {
        startError = null;

        // Never (re)start on a disposed service. Reachable even without a deferred callback: StopLocked
        // transiently drops _lifecycleLock during its bounded wait, so Dispose can run between a
        // retune's StopLocked/StartLocked pair and leave _isActive stuck true with nothing running.
        if (_disposed)
            return;

        if (_isActive)
            return;

        // The Gate covers ONLY the reverse mutual-exclusion guard and the arm-and-start of native
        // sampling -- never the unwind. Once _native.Start has been called (succeeded OR thrown),
        // _isActive is already true, so ThreadProfilingService's forward guard will refuse a concurrent
        // thread-profiling start whether or not the Gate is still held; nothing past that point needs it.
        // Everything after the Gate (the drain schedule, the timestamp/interval bookkeeping, the
        // trace-context seam) and any unwind via StopLocked therefore runs OUTSIDE the Gate. That is
        // deliberate: StopLocked can block for up to _drainShutdownWaitTimeout, and holding this
        // process-wide Gate across that wait would stall ThreadProfilingService.StartThreadProfilingSession
        // -- which runs synchronously on the agent-command thread -- and the whole command batch behind it.
        // StopLocked still runs under _lifecycleLock (this method's callers hold it for all of StartLocked)
        // and drops/reacquires it internally for its own bounded wait.
        //
        // Reverse mutual-exclusion guard: never start while a thread-profiling session is in-flight; defer
        // and retry (the retry re-reads config via ApplyConfigChange, so disable-while-deferred just
        // no-ops). Serialized against ThreadProfilingService's forward guard via
        // ProfilingMutualExclusionGate.Acquire(); this method holds _lifecycleLock, so lock order is
        // _lifecycleLock -> Gate, never reversed.
        Exception startException = null;
        using (ProfilingMutualExclusionGate.Acquire())
        {
            if (ThreadProfilingStatus?.IsThreadProfilingActive == true)
            {
                Log.Info("[ContinuousProfiling] Start deferred: a thread-profiling session is active; retrying in {0} ms.", (int)DeferredStartRetryInterval.TotalMilliseconds);
                _scheduler.ExecuteOnce(deferredRetryAction, DeferredStartRetryInterval);
                return;
            }

            // Clear backoff state left over from a previous session -- otherwise disabling CP while a
            // probe is pending and re-enabling later leaves _sendBackoffActive stuck true forever. Ints
            // written before the volatile flag, matching ResumeAfterReconnect's order, so the flag's
            // write publishes the zeroed counters too.
            _consecutiveSendFailures = 0;
            _backoffIndex = 0;
            // Supersede any probe still pending from a previous session so it no-ops instead of
            // resuming sampling this fresh session didn't schedule.
            _backoffGeneration++;
            _sendBackoffActive = false;
            _stopSignaled = false;

            // Arm the reverse-guard flag before starting native sampling, while still holding the Gate,
            // so ThreadProfilingService can't observe a stale "not active" value.
            _isActive = true;

            try
            {
                // Allocate the LOH drain buffer here rather than in the constructor so a process that never
                // enables continuous profiling pays no LOH. Reused across drains for this session and freed
                // in StopLocked; a retune's stop/start pair reallocates (acceptable -- retunes are rare, and
                // the freed same-size LOH block is reusable). An OOM here is captured like a native-start
                // failure and unwound via StopLocked below (outside the Gate), which nulls the field again.
                if (_drainBuffer == null)
                    _drainBuffer = new byte[DrainBufferSize];

                _native.Start(intervalMs);
            }
            catch (Exception ex)
            {
                // Capture, don't unwind here: StopLocked must run outside the Gate (see the block comment
                // above). _isActive stays true until that StopLocked's finally, so the reverse guard keeps
                // refusing a thread-profiling start throughout the unwind window.
                startException = ex;
            }
        }

        if (startException != null)
        {
            Log.Error(startException, "[ContinuousProfiling] Failed to start the drain schedule.");
            startError = startException.Message;

            // _isActive is armed before the first call that can throw, so a half-started session must be
            // unwound, or the flag lies to IsActive and permanently blocks thread profiling.
            StopLocked();
            return;
        }

        try
        {
            // trackAsAgentWork: false -- _drainAction is only `() => Task.Run(DrainOnce)` (see field
            // decl below), so this flag would tag the scheduler-thread dispatch, not the pool thread
            // that actually runs DrainOnce; it can't cover the drain body regardless of its value.
            // Most of DrainOnce's own CPU is covered anyway: OtlpProfileBuilder's frame-text filter
            // (IsAgentThreadSample) excludes NewRelic.Agent.Core.* frames from the profile whether or
            // not the sampling thread is tagged as agent work. The residual, accepted gap is the
            // window parked inside Send -- off-CPU, BCL-only frames, invisible to the frame-text filter.
            _scheduler.ExecuteEvery(_drainAction, TimeSpan.FromMilliseconds(intervalMs), trackAsAgentWork: false);
            _activeIntervalMs = intervalMs;
            Interlocked.Exchange(ref _lastDrainTimestamp, Stopwatch.GetTimestamp());

            // Arm trace-context correlation only now that native sampling is running, and publish the seam so
            // the wrapper hot path starts pushing the current trace/span on each app thread.
            _continuousProfilingContext.Enable(_native);
            ContinuousProfilingContext.Instance = _continuousProfilingContext;

            Log.Info("[ContinuousProfiling] Session started; draining every {0} ms.", intervalMs);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to start the drain schedule.");
            startError = ex.Message;

            // Native sampling did start (the Gate block succeeded) but a post-Gate setup step threw, so the
            // half-started session must still be unwound -- same reasoning as the native-start failure above.
            StopLocked();
        }
    }

    private void StopLocked()
    {
        // A second concurrent call can land while another thread's StopLocked has dropped
        // _lifecycleLock below during its own bounded drain wait; wait for that stop to actually finish
        // before proceeding (this wait also drops _lifecycleLock, for the same reason). A completed wait
        // doesn't necessarily satisfy this caller's own request -- a start can have run in between (a
        // retune's StopLocked/StartLocked pair holds the lock across both) -- so fall through and stop
        // again if _isActive is still true. The loop also drains a THIRD thread's newer _stopInProgress
        // that may appear in the gap before this thread reacquires the lock.
        while (true)
        {
            var inProgress = _stopInProgress;
            if (inProgress == null)
                break;

            Monitor.Exit(_lifecycleLock);
            bool completed;
            try
            {
                // Test-only observation point: this second caller has dropped the lock and is about to
                // park on the in-progress stop. Inside the try so the finally still reacquires the lock
                // even if a test hook were to throw.
                EnteredStopInProgressWaitForTesting?.Invoke();
                completed = inProgress.Task.Wait(_drainShutdownWaitTimeout);
            }
            finally
            {
                Monitor.Enter(_lifecycleLock);
            }

            if (!completed)
            {
                // That stop outran the bound -- wedged in the drain wait or in native. Piling a second stop
                // on top would make things worse, so this caller's own stop/retune request silently
                // no-ops (only the Warn below records it). Accepted: better than retrying onto a wedged
                // native session.
                Log.Warn("[ContinuousProfiling] Timed out after {0} waiting for a concurrent stop already in progress.", _drainShutdownWaitTimeout);
                return;
            }

            if (!_isActive)
                return;
        }

        var myStop = new TaskCompletionSource<bool>();
        _stopInProgress = myStop;

        try
        {
            // Disarm correlation first so the wrapper hot path stops pushing before native sampling stops.
            // Restore the inert default instance so IsEnabled is false again everywhere.
            _continuousProfilingContext.Disable();
            ContinuousProfilingContext.Instance = new ContinuousProfilingContext();

            _scheduler.StopExecuting(_drainAction);

            // Signal, lock-free, before the bounded wait below -- lets a racing drain's OnSendResult skip
            // its backoff bookkeeping cheaply (optimization only; see the fence below for what makes the
            // wait itself safe).
            _stopSignaled = true;

            // Full fence pairing with DrainOnce's own fence (its Interlocked.Exchange when publishing
            // _lastDrainTask). Without it, this thread's plain read of _lastDrainTask below and the
            // volatile write above could each reorder past the other relative to DrainOnce's
            // _stopSignaled recheck, letting "this thread reads a stale _lastDrainTask" and "DrainOnce
            // reads _stopSignaled == false" both be true at once -- leaving the bounded wait below with
            // nothing to wait on while a late drain still runs.
            Thread.MemoryBarrier();

            // StopExecuting only guarantees the scheduler considers _drainAction finished; since that
            // action just dispatches DrainOnce via Task.Run, wait for the real drain explicitly before
            // _native.Stop(), or a detached drain could still be touching the native profiler after
            // teardown.
            var drainTask = _lastDrainTask;
            if (!drainTask.IsCompleted)
            {
                // Drop _lifecycleLock for exactly this wait (legal: Monitor supports same-thread
                // recursive acquire/release, and the finally below reacquires unconditionally) -- see
                // the locking-posture note on _lifecycleLock for why holding it for the whole wait would
                // self-deadlock against a racing drain's OnSendResult.
                Monitor.Exit(_lifecycleLock);
                try
                {
                    // Test-only observation point: the lock is dropped and this thread is about to enter
                    // the bounded wait. Inside the try so the finally still reacquires the lock even if a
                    // test hook were to throw.
                    EnteredPrimaryDrainWaitForTesting?.Invoke();
                    if (!drainTask.Wait(_drainShutdownWaitTimeout))
                    {
                        Log.Warn("[ContinuousProfiling] Timed out after {0} waiting for an in-flight drain to finish before stopping native sampling.", _drainShutdownWaitTimeout);
                    }
                }
                finally
                {
                    Monitor.Enter(_lifecycleLock);
                }
            }

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
            // Release the LOH drain buffer: freeing it on stop is what keeps a disabled/stopped process at
            // zero cost. Safe even though a drain may have outrun this stop's bounded wait -- that drain
            // snapshotted the field into a local (see DrainOnce), which keeps the array alive for its own
            // duration, so nulling the field here cannot pull the buffer out from under it. A later retune
            // reallocates in StartLocked.
            _drainBuffer = null;
            _stopInProgress = null;
            myStop.SetResult(true);
        }
    }

    /// <summary>
    /// Drains at most one batch and ships it. Catches everything: a drain failure must never surface
    /// in the instrumented application.
    /// </summary>
    public void DrainOnce()
    {
        // Lock-free volatile read; OnSendResult below does take _lifecycleLock once a real send has
        // happened. Dispose has joined the native worker thread, so a drain landing after this check
        // would P/Invoke into a dead sampler. _stopSignaled catches the bare stop/retune case too,
        // fenced by StopLocked's MemoryBarrier (see StopLocked).
        if (_disposed || _stopSignaled)
            return;

        // Allocated BEFORE the guard is taken so that nothing which can throw sits between winning the
        // CompareExchange below and entering the try whose finally releases it. A throw in that gap (an
        // allocation failure here, for instance) would leave _drainInFlight stuck at 1 with no thread left
        // to reset it, permanently no-oping every later drain for the process lifetime.
        var drainCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (Interlocked.CompareExchange(ref _drainInFlight, 1, 0) != 0)
        {
            // Test-only observation point: this tick lost the guard and is about to return as a no-op.
            DrainTickLostGuardForTesting?.Invoke();
            return; // another drain is already in flight (retune overlap) -- skip this tick rather than race the shared buffer
        }

        try
        {
            // Publish a task tracking THIS drain only now that the guard above is actually won -- a tick
            // that lost it returned above without touching the field.
            Interlocked.Exchange(ref _lastDrainTask, drainCompletion.Task);

            try
            {
                // Narrows (doesn't close -- see StopLocked's fence) the same race the top-of-method gate
                // guards: bail before ReadBatch if _stopSignaled flipped true in between.
                if (_disposed || _stopSignaled)
                    return;

                // Nowhere to send yet: skip read/parse/build rather than doing the work and dropping the
                // result. Native sampling still runs (decoupled from connect); only the managed drain is
                // deferred. _sendBackoffActive: sampling itself is paused while backing off, so this is
                // mostly a cheap guard against the recurring timer's own ticks meanwhile.
                if (!_isConnected || _sendBackoffActive)
                    return;

                // Read once into a local: StopLocked can null the field while this drain is still running (it
                // releases the buffer in its finally, and its wait for an in-flight drain is bounded and can
                // time out). The local keeps the array alive for the rest of this drain. A null here means no
                // session is armed (buffer not yet allocated, or already released), so there is nothing to drain.
                var drainBuffer = _drainBuffer;
                if (drainBuffer == null)
                    return;

                var bytesRead = _sampleSource.ReadBatch(drainBuffer);
                if (bytesRead <= 0)
                    return;

                // Both checks below guard against native/managed drifting apart: `>` would mean
                // BufferParser walks off the buffer end (can't happen today -- native already caps at
                // DrainBufferSize), and `>=` flags an exact-fill batch as a tripwire for the two
                // constants ever diverging.
                if (bytesRead > drainBuffer.Length)
                {
                    Log.Debug("[ContinuousProfiling] ReadBatch reported {0} bytes, exceeding the {1}-byte buffer; discarding this drain.", bytesRead, drainBuffer.Length);
                    SafeReportError();
                    return;
                }

                if (bytesRead >= drainBuffer.Length)
                {
                    Log.Debug("[ContinuousProfiling] ReadBatch filled the entire {0}-byte drain buffer; the batch may have been truncated.", drainBuffer.Length);
                    _agentHealthReporter.ReportSupportabilityCountMetric(SupportabilityDrainBufferBoundaryMetric);
                }

                var samples = BufferParser.Parse(drainBuffer, bytesRead, out var batchStats, out var parseFailed);

                if (parseFailed)
                {
                    Log.Debug("[ContinuousProfiling] BufferParser rejected this drain's buffer (truncated header, unknown batch version, or a sample seen before StartBatch); discarding.");
                    SafeReportError();
                    return;
                }

                // Surface the native BatchStats for CP overhead/fidelity analysis (and OTel FinalStats parity):
                // microsSuspended = the stop-the-world window this sweep; skipped = threads/frames the walk missed.
                // onCpu/total is the live on-CPU classification signal, cheap to read at finest without
                // needing to inspect an actual sent OTLP payload.
                if (batchStats != null && Log.IsFinestEnabled)
                    Log.Finest("[ContinuousProfiling] batch stats: microsSuspended={0} threads={1} frames={2} skipped={3} onCpu={4}/{5}",
                        batchStats.MicrosSuspended, batchStats.Threads, batchStats.Frames, batchStats.Skipped, CountOnCpu(samples), samples.Count);

                if (samples.Count == 0)
                    return;

                // The sampling interval (ms) is the profile's period; convert to nanoseconds for
                // period_type=cpu/ns. Zero means no session interval is known any more -- StopLocked's
                // finally zeroes _activeIntervalMs, and a drain that outran its bounded wait can reach
                // here afterward. OtlpProfileBuilder emits no profiles at all without a period, so
                // continuing would POST an empty payload and then score its HTTP outcome as a send
                // success/failure in the backoff state machine for a drain that carried no data.
                var periodNanos = (long)_activeIntervalMs * 1_000_000L;
                if (periodNanos <= 0)
                {
                    Log.Debug("[ContinuousProfiling] Drain completed with no active sampling interval (session stopped); discarding {0} sample(s) rather than sending an empty profile.", samples.Count);
                    return;
                }

                var now = Stopwatch.GetTimestamp();
                var durationNano = ElapsedNanos(Interlocked.Read(ref _lastDrainTimestamp), now);
                // The window ends now; it started durationNano ago -- not the reverse. Getting this backwards
                // makes every profile appear to cover [now, now + duration) instead of the interval actually
                // sampled, [now - duration, now).
                var startUnixNano = ToUnixNano(DateTime.UtcNow) - durationNano;
                Interlocked.Exchange(ref _lastDrainTimestamp, now);

                // Exclude the agent's own threads/frames unless the undocumented appSettings opt-in is set.
                var request = OtlpProfileBuilder.Build(samples, startUnixNano, durationNano, ServiceName, _configuration.EntityGuid, _configuration.UtilizationHostName, periodNanos, _configuration.ContinuousProfilingIncludeAgentCode);

                // includeAgentCode:false can filter every sample this sweep caught (e.g. an all-agent-work
                // tick), leaving zero Profiles under the built ScopeProfiles -- don't POST an empty request.
                if (request.ResourceProfiles.Count == 0 || request.ResourceProfiles[0].ScopeProfiles.Count == 0
                    || request.ResourceProfiles[0].ScopeProfiles[0].Profiles.Count == 0)
                {
                    Log.Debug("[ContinuousProfiling] All {0} sample(s) this sweep were filtered (agent code excluded); nothing to send.", samples.Count);
                    return;
                }

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
                    _agentHealthReporter.ReportSupportabilityCountMetric(SupportabilitySamplesMetric, samples.Count);
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
            // Completes drainCompletion.Task -- the same task StopLocked may currently be Wait()-ing on
            // via _lastDrainTask -- so it is safe to signal after the guard release above.
            drainCompletion.SetResult(true);
        }
    }

    /// <summary>
    /// Tracks consecutive send failures. The grace period (tolerate one blip) applies only the first
    /// time a streak starts; an already-escalated streak re-trips on a single failure. A success fully
    /// resets both counters.
    /// </summary>
    private void OnSendResult(bool sent)
    {
        // Lock-free fast exit before _lifecycleLock: once a stop/retune has signaled, this drain's
        // outcome is moot. Also avoids racing StopLocked's drainTask.Wait, which already holds
        // _lifecycleLock while this thread's ordinary `lock` would otherwise contend for it (see the
        // locking-posture note above).
        if (_stopSignaled)
            return;

        // Under _lifecycleLock: mutates the same backoff state StartLocked/EndBackoffProbe/
        // ResumeAfterReconnect write under that lock, so a racing reconnect can't clear the gate with
        // native already stopped and leave DrainOnce spinning forever with nothing sampling.
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
    /// Pauses native sampling and schedules a single probe at the current backoff step; the drain
    /// timer keeps ticking (each tick a no-op via <see cref="_sendBackoffActive"/>) so no second timer
    /// is needed to resume sending once the probe fires. Caller already holds <see cref="_lifecycleLock"/>.
    /// </summary>
    private void TripBackoffAndScheduleProbeLocked(int failuresAtTrip)
    {
        _sendBackoffActive = true;
        _native.Stop();

        // Tag the probe with a new generation; a prior round's probe (IScheduler can't cancel it)
        // becomes stale and no-ops instead of resuming sampling mid-round.
        var generation = ++_backoffGeneration;
        var delay = SendBackoffSequence[_backoffIndex];
        Log.Info("[ContinuousProfiling] {0} consecutive send failures; pausing sampling, retrying in {1}s.",
            failuresAtTrip, delay.TotalSeconds);
        _scheduler.ExecuteOnce(() => EndBackoffProbeIfCurrent(generation), delay);
        _backoffIndex = Math.Min(_backoffIndex + 1, SendBackoffSequence.Length - 1);
    }

    private enum ResumeOutcome
    {
        SessionInactive,
        DeferredThreadProfilingActive,
        Resumed
    }

    /// <summary>
    /// Resumes native sampling under <see cref="_lifecycleLock"/> if the session is still active
    /// (<see cref="ResumeOutcome.SessionInactive"/> if disabled while backing off -- reviving would be
    /// wrong) and resets <see cref="_lastDrainTimestamp"/> so the first post-resume profile's duration
    /// doesn't span the paused window.
    ///
    /// Takes <see cref="ProfilingMutualExclusionGate.Acquire"/> around the actual resume -- same handshake
    /// <see cref="StartLocked(int, System.Action, out string)"/> uses -- so a resume can never land while a
    /// thread-profiling session is in-flight (see H1 in the 2026-08-31 review: without this,
    /// backoff/reconnect resume walked right through the mutual-exclusion guard because
    /// <see cref="IsActive"/> deliberately reports false during backoff). Caller already holds
    /// <see cref="_lifecycleLock"/>, so the nested lock order here matches
    /// <see cref="StartLocked(int, System.Action, out string)"/>'s documented _lifecycleLock -> Gate order.
    /// </summary>
    private ResumeOutcome TryResumeSamplingLocked()
    {
        if (!_isActive)
            return ResumeOutcome.SessionInactive;

        using (ProfilingMutualExclusionGate.Acquire())
        {
            if (ThreadProfilingStatus?.IsThreadProfilingActive == true)
                return ResumeOutcome.DeferredThreadProfilingActive;

            _native.Start(_activeIntervalMs);
        }

        Interlocked.Exchange(ref _lastDrainTimestamp, Stopwatch.GetTimestamp());
        return ResumeOutcome.Resumed;
    }

    /// <summary>
    /// Scheduled with the generation of the round that scheduled it; no-ops if that round is no
    /// longer current (IScheduler can't cancel a stale timer). Otherwise leaves
    /// <see cref="_consecutiveSendFailures"/>/<see cref="_backoffIndex"/> alone -- if the resumed send
    /// fails too, escalation continues from where the trip left off.
    /// </summary>
    private void EndBackoffProbeIfCurrent(int generation)
    {
        ResumeOutcome outcome;
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            // Stale probe from a superseded round -- resuming now would clear the gate for a round it
            // has no business ending.
            if (generation != _backoffGeneration)
                return;

            try
            {
                outcome = TryResumeSamplingLocked();
            }
            catch (Exception ex)
            {
                // A transient failure resuming native sampling (e.g. a P/Invoke blip in _native.Start)
                // must not wedge CP: this probe is the ONLY path that clears _sendBackoffActive, so an
                // escaping exception would leave it stuck true forever -- native stopped, every drain a
                // no-op, IsActive false -- with no further probe scheduled (the Scheduler's CatchAndLog
                // logs but does not retry). Reschedule under the SAME generation (still current -- checked
                // above), so a newer round that bumps _backoffGeneration still supersedes this retry, the
                // same generation-guarded reschedule the deferred-thread-profiling branch below uses.
                Log.Warn(ex, "[ContinuousProfiling] Backoff-probe resume threw; retrying in {0} ms.", (int)DeferredStartRetryInterval.TotalMilliseconds);
                _scheduler.ExecuteOnce(() => EndBackoffProbeIfCurrent(generation), DeferredStartRetryInterval);
                return;
            }

            // A thread-profiling session is in-flight; leave the backoff gate armed (drain keeps
            // no-oping) and retry this same probe later instead of resuming into the mutual-exclusion
            // violation H1 described. The generation guard above still protects this retry.
            if (outcome == ResumeOutcome.DeferredThreadProfilingActive)
            {
                Log.Info("[ContinuousProfiling] Backoff-probe resume deferred: a thread-profiling session is active; retrying in {0} ms.", (int)DeferredStartRetryInterval.TotalMilliseconds);
                _scheduler.ExecuteOnce(() => EndBackoffProbeIfCurrent(generation), DeferredStartRetryInterval);
                return;
            }

            if (outcome == ResumeOutcome.Resumed)
                _sendBackoffActive = false;
        }

        if (outcome == ResumeOutcome.Resumed)
            Log.Info("[ContinuousProfiling] Resuming sampling after backoff.");
    }

    /// <summary>
    /// Called when a reconnect arrives while backing off; fully resets backoff state (same as a
    /// successful send) since the reconnect itself is the likely fix.
    /// </summary>
    private void ResumeAfterReconnect()
    {
        ResumeOutcome outcome;
        lock (_lifecycleLock)
        {
            if (_disposed)
                return;

            // Re-check the gate under the lock. OnAgentConnected's check is a lock-free read, and a
            // deferred retry of this method (scheduled below) runs minutes later -- either way the round
            // this resume was meant to end may already have been ended by its own probe. _sendBackoffActive
            // is the authoritative "native sampling is currently paused" signal (_isActive stays true
            // throughout a backoff round), so without this a second resume would _native.Start an
            // already-running session and reset _lastDrainTimestamp again, understating the duration
            // window of the next profile sent.
            if (!_sendBackoffActive)
                return;

            outcome = TryResumeSamplingLocked();

            // A thread-profiling session is in-flight; leave the backoff state exactly as-is (still
            // backing off, same generation, pending probe still scheduled) and retry the reconnect
            // resume later instead of walking through the mutual-exclusion guard (H1).
            if (outcome == ResumeOutcome.DeferredThreadProfilingActive)
            {
                Log.Info("[ContinuousProfiling] Reconnect resume deferred: a thread-profiling session is active; retrying in {0} ms.", (int)DeferredStartRetryInterval.TotalMilliseconds);
                _scheduler.ExecuteOnce(ResumeAfterReconnect, DeferredStartRetryInterval);
                return;
            }

            if (outcome == ResumeOutcome.Resumed)
            {
                _consecutiveSendFailures = 0;
                _backoffIndex = 0;
                // Supersede the pending probe from the round this reconnect is ending early, so it can't
                // fire later and collapse a subsequent backoff round.
                _backoffGeneration++;
                _sendBackoffActive = false;
            }
        }

        if (outcome == ResumeOutcome.Resumed)
            Log.Info("[ContinuousProfiling] Reconnected while backing off; resuming sampling immediately.");
    }

    /// <summary>
    /// Counts samples classified as on-CPU. Public so unit tests can reach it without
    /// <c>InternalsVisibleTo</c>, which this repo bans.
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

            // Hard-reset backstop, independent of how StopLocked returned. StopLocked's concurrent-stop
            // timeout path (a second stop landing while another stop is wedged past the bounded wait)
            // returns early WITHOUT running the finally that clears _isActive/_activeIntervalMs and
            // disarms the trace-context seam. If Dispose relied on that unwind having run, a timed-out
            // stop would leave _isActive stuck true (permanently blocking ThreadProfilingService's
            // mutual-exclusion guard for the process lifetime) and ContinuousProfilingContext.Instance
            // armed (app threads keep P/Invoking SetTraceContext into the very sampler _native.Shutdown()
            // below is about to join). Disable() is idempotent, so running it again after a normal
            // StopLocked is harmless.
            _continuousProfilingContext.Disable();
            ContinuousProfilingContext.Instance = new ContinuousProfilingContext();
            _isActive = false;
            _activeIntervalMs = 0;

            // StopLocked already nulls the buffer when it runs; do it here too so a service that was
            // constructed but never started, or disposed while inactive, also drops the LOH reference.
            _drainBuffer = null;

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
