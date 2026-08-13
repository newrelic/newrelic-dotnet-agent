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
/// Each drain reads one batch from the <see cref="ISampleSource"/> into a reused buffer, parses it,
/// builds an OTLP profile, and hands it to the <see cref="IProfilesTransport"/>. All drain work is
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
    private volatile int _activeIntervalMs;

    // Profile-type tokens ("cpu") currently owned by an agent command rather than local/server config.
    // ApplyConfigChange must not start/stop/retune a type present here -- only a matching StopFromCommand
    // call or process restart releases it (see StartFromCommand/StopFromCommand below). Modeled as a set,
    // not a bool, because the command spec is per-type ("all"/"cpu"/"heap"): today only "cpu" can ever be
    // a member (heap/allocations isn't implemented), but this generalizes once a second independently
    // toggleable type exists, without another redesign of the guard.
    private readonly HashSet<string> _commandControlledTypes = new HashSet<string>();

    private static readonly IReadOnlyDictionary<string, string> EmptyCommandExceptions = new Dictionary<string, string>();

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

            // An agent command owns the cpu bundle until a matching stop command or process restart --
            // an incidental config-update event (an unrelated SSC push, a reconnect) must not silently
            // override an operator's explicit start/stop_continuous_profiler command. See
            // _commandControlledTypes and StartFromCommand/StopFromCommand.
            if (_commandControlledTypes.Contains(ContinuousProfilingCommandTypes.Cpu))
                return;

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
        // NOTE: this check and ThreadProfilingService's forward guard (IsActive) are read/written under
        // different locks (this service's _lifecycleLock vs. no explicit lock on the thread-profiling
        // side), so a narrow window exists where both services could decide to start concurrently. These
        // managed guards are a cooperative, coarse gate on top of the real enforcement backstop: the
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
            // A new session starts optimistic: clear any backoff state left over from a PREVIOUS session.
            // Without this, disabling CP while a probe is pending and re-enabling later would leave
            // _sendBackoffActive stuck true forever -- EndBackoffProbe's own !_isActive guard (below) means
            // the probe that fires while disabled never clears it, and nothing else ever will.
            //
            // Ints first, then the volatile flag last -- a volatile write publishes everything written
            // before it to another thread's next volatile read of the same field (release semantics). The
            // old order (flag first) published nothing. Matches ResumeAfterReconnect's order.
            _consecutiveSendFailures = 0;
            _backoffIndex = 0;
            // Supersede any probe still pending from a previous session (e.g. a retune's stop/start), so it
            // no-ops instead of resuming sampling this fresh session didn't schedule.
            _backoffGeneration++;
            _sendBackoffActive = false;

            // Arm the reverse-guard flag BEFORE starting native sampling (not after) -- narrows the
            // mutual-exclusion race window with ThreadProfilingService's forward guard, which reads this
            // flag under a different lock. A failed start unwinds it (see the catch below).
            _isActive = true;

            // Start native sampling first, then begin draining it. Both run under _lifecycleLock, which is
            // fine: lifecycle transitions are rare (config-driven), so the native call here does not touch
            // the lock-free hot path (DrainOnce).
            _native.Start(intervalMs);
            _scheduler.ExecuteEvery(_drainAction, TimeSpan.FromMilliseconds(intervalMs));
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

            // _isActive is armed before the first call that can throw, so a half-started session has to be
            // unwound here -- otherwise the flag stays true with nothing running, which both lies to
            // IsActive and permanently blocks thread profiling via the guard above.
            StopLocked();
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
                var durationNano = ElapsedNanos(Interlocked.Read(ref _lastDrainTimestamp), now);
                Interlocked.Exchange(ref _lastDrainTimestamp, now);

                // The sampling interval (ms) is the profile's period; convert to nanoseconds for period_type=cpu/ns.
                var periodNanos = (long)_activeIntervalMs * 1_000_000L;
                // Exclude the agent's own threads/frames unless the undocumented appSettings opt-in is set.
                var request = OtlpProfileBuilder.Build(samples, startUnixNano, durationNano, ServiceName, periodNanos, _configuration.ContinuousProfilingIncludeAgentCode);

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
        }
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
        if (!_isActive)
            return false;

        _native.Start(_activeIntervalMs);
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
