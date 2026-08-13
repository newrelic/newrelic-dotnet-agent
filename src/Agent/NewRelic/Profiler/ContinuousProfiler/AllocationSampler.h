/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <cstring>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>

#include <cor.h>
#include <corprof.h>

#include "../Logging/Logger.h"
#include "../ThreadProfiler/namecache.h"
#include "AllocationBatchAccumulator.h"
#include "AllocationSubSampler.h"
#include "FrameNameResolver.h"
#include "OsThreadName.h"
#include "SampleBufferQueue.h"
#include "SampleBufferWriter.h"
#include "SuspendMutex.h"
#include "TraceContextMap.h"

// AllocationSampler is the event-driven counterpart to ContinuousProfiler's timer-driven thread walker.
// It has NO worker thread of its own -- it is driven entirely by CLR EventPipe AllocationTick callbacks
// delivered ON THE ALLOCATING APP THREAD (ICorProfilerCallback10::EventPipeEventDelivered, routed here
// by CorProfilerCallbackImpl). Everything it does therefore runs on a customer thread in the middle of
// an allocation, which drives every design choice below:
//
//   * The CLR raises AllocationTick roughly every 100 KB allocated -- ~10^5/second in an allocation-heavy
//     app -- so the handler bails as early and as cheaply as possible. Order: session gate -> tick
//     try-lock -> sub-sample -> payload parse -> back-pressure -> stack walk -> encode.
//   * The TICK PATH never blocks an app thread. Every lock it takes there is a try_lock; a lost race
//     costs one allocation sample, which is statistically irrelevant to a subsampled profile and is never
//     a correctness issue. (The LIFECYCLE calls -- Start/Stop/Shutdown -- do block, by design; they run
//     on the agent's own threads. See Shutdown() for exactly how long it can wait and why.)
//   * It NEVER throws. This is a COM callback boundary; an escaping exception would propagate into the
//     runtime's EventPipe dispatch.
//   * Session teardown must never block agent shutdown INDEFINITELY -- the stop call is bounded by a
//     timeout and then abandoned. It is not instantaneous, though: see the Shutdown() comment for the
//     one in-flight sample it may still wait out.
//
// Concurrency model: allocation ticks arrive on MANY app threads at once, but almost all of this class's
// state (the sub-sampler's RNG, the name cache, the frame resolver's scratch frame, the encode buffer)
// is single-threaded-only. Rather than sprinkling locks over each, one try_lock'd _tickMutex makes the
// whole handler mutually exclusive: exactly one thread handles a tick at a time and the rest return
// immediately. That also makes this class the single producer SampleBufferQueue::HasFreeSlot assumes,
// and -- because nothing is ever QUEUED on a try_lock -- it is what lets Shutdown() drain in-flight
// handlers deterministically before the owner destroys this object.
//
// ONE CONSEQUENCE OF THAT WORTH SPELLING OUT: the managed drain thread also publishes, via the
// pending-batch flush in ReadAllocationSamples. SampleBufferQueue's "check HasFreeSlot, then TryPublish
// later" gating is only sound with a single producer, so that flush takes _tickMutex (try_lock) too.
// Every publish in this class -- from a tick handler or from the drain -- happens under _tickMutex, and
// anything added later that publishes must do the same.
//
// Lifecycle contract for the owner (Task 4): call Init() once, Start()/Stop() as configuration dictates,
// and Shutdown() EXPLICITLY while the process is still healthy. The destructor is only a diagnostic
// safety net -- it cannot stop the session, because it may run under process/DLL teardown (see there).
// Two guarantees, and they are different things:
//   * MUTUAL EXCLUSION between concurrent Start/Stop/Shutdown calls -- they serialize internally on
//     _lifecycleMutex, so the owner needs no lock of its own.
//   * Shutdown() is TERMINAL. A Start() arriving after Shutdown() has returned (not just racing it) is
//     refused rather than opening a fresh session on a torn-down object. Together with the in-flight drain
//     Shutdown() performs, that is what makes "Shutdown() then destroy" safe without the owner having to
//     prove no Start() is still in flight anywhere.
// Note the deliberate divergence from ContinuousProfiler::Shutdown, which is restartable (it resets its
// flags so a later Start() spins up a fresh worker thread). Here Shutdown() is one-way: pausing and
// resuming is Stop()/Start(), and a genuine restart-after-shutdown would need a new AllocationSampler.
// The asymmetry is intentional -- an EventPipe session cannot be reopened as cheaply or as safely as a
// worker thread can be respawned -- but Task 4 should wire the managed enable/disable path to Stop()/
// Start(), NOT to Shutdown()/Start().
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class AllocationSampler
    {
    public:
        // GCAllocationTick's event id in the Microsoft-Windows-DotNETRuntime provider (the GC keyword's
        // 10th event). Exposed so the CorProfilerCallbackImpl dispatch can filter without duplicating
        // this knowledge.
        static constexpr DWORD AllocationTickEventId = 10;

        // The ONLY payload version ParseAllocationTickPayload can read. The v4 layout is parsed partly
        // from the front and partly from the BACK of the buffer (AllocatedSize is the last field), so a
        // different version must be rejected rather than misread: an older v2/v3 payload would yield a
        // slice of the Address field as the allocation size, and a hypothetical future v5 that appends a
        // field would do the same. v4 has been the newest version since .NET Core 3.0; if a future
        // runtime ships v5, allocation sampling goes quiet (no samples) until this is updated -- which is
        // the correct failure mode for telemetry.
        static constexpr DWORD AllocationTickSupportedVersion = 4;

        // Upper bound on an accepted sample budget, 300x the shipped default of 200/minute. Not a tuned
        // performance limit -- it is the point past which the number stops meaning anything: each sample
        // costs a stack walk plus frame-name resolution ON THE ALLOCATING APPLICATION THREAD, and the
        // two-slot SampleBufferQueue plus the managed drain interval, not this cap, become the binding
        // constraint well below 1000/second. Anything larger is indistinguishable from "unbounded".
        static constexpr int32_t MaxSupportedSamplesPerMinute = 60000;

        // Validates a sample budget as it arrives from managed code -- SIGNED, because that is how it
        // crosses the P/Invoke boundary -- and converts it to the unsigned value Start() takes. Returns
        // false when sampling must not be started at all.
        //
        // This exists because a blind static_cast<uint32_t> of a non-positive value is NOT a harmless
        // no-op: -1 becomes 4294967295, AllocationSubSampler does not clamp its target, and its odds
        // computation then saturates to >= 1 -- so every single AllocationTick (~10^5/second in an
        // allocation-heavy app) would take the tick mutex, walk the stack, resolve frame names and encode,
        // on customer threads. A configuration mistake would become a performance incident. Zero is
        // rejected rather than treated as "sample nothing", because a session opened for a zero budget
        // still makes the CLR generate every AllocationTick for no benefit.
        //
        // Lives here, next to the sub-sampler it protects, so the rule is unit-testable and cannot drift
        // from the state it guards; the export layer only logs and forwards.
        static bool TryNormalizeMaxSamplesPerMinute(int32_t requested, uint32_t& normalized) noexcept
        {
            if (requested <= 0)
            {
                normalized = 0;
                return false;
            }

            // Local copy, same reason as StopSessionWithBoundedWait's: a conditional expression whose
            // operands are both lvalues yields an lvalue, which risks odr-using this static constexpr
            // member -- and C++11/14 (the Linux build) has no way to define one in a header-only class.
            const int32_t ceiling = MaxSupportedSamplesPerMinute;
            normalized = static_cast<uint32_t>(requested < ceiling ? requested : ceiling);
            return true;
        }

        // Dispatch predicate for CorProfilerCallbackImpl::EventPipeEventDelivered. Provider identity is
        // the caller's business (it holds the EVENTPIPE_PROVIDER handle for the session it created); this
        // only answers "is this the event we can parse?".
        static bool IsAllocationTickEvent(DWORD eventId, DWORD eventVersion) noexcept
        {
            return eventId == AllocationTickEventId && eventVersion == AllocationTickSupportedVersion;
        }

        // Called during Profiler Initialize. Like ContinuousProfiler::Init this does no heavy lifting: it
        // stores the ICorProfilerInfo4, probes for the ICorProfilerInfo12 that EventPipe sessions require
        // (.NET 5+ / CoreCLR only -- absent on .NET Framework), and creates the frame-name resolver. It
        // starts no threads and opens no session.
        //
        // `sharedTraceContexts` is the ContinuousProfiler's OWN map (not owned here, must outlive this
        // object): the managed side pushes trace context through a single export that writes that map, so
        // allocation samples can only be correlated by reading the same instance.
        void Init(ICorProfilerInfo4* corProfilerInfo, TraceContextMap* sharedTraceContexts) noexcept
        {
            LogDebug(L"Initializing AllocationSampler");

            _corProfilerInfo = corProfilerInfo;
            _traceContexts = sharedTraceContexts;

            if (corProfilerInfo == nullptr)
            {
                return;
            }

            HRESULT hr = corProfilerInfo->QueryInterface(__uuidof(ICorProfilerInfo12), (void**)&_corProfilerInfo12);
            // Debug, not Info: every .NET Framework process would otherwise log two lines at Info about a
            // feature it can never use. The lines still exist for diagnosing "why no allocation samples?",
            // which is a debug-level question by then.
            if (SUCCEEDED(hr))
            {
                LogDebug(L"AllocationSampler: ICorProfilerInfo12 available");
            }
            else
            {
                LogDebug(L"AllocationSampler: ICorProfilerInfo12 unavailable (.NET Framework or old CoreCLR) -- allocation sampling disabled");
            }

            try
            {
                // Its OWN resolver over its OWN name cache -- deliberately NOT shared with
                // ContinuousProfiler's. Sharing would put two threads (an app thread here, the sampling
                // thread there) inside the same non-thread-safe LRU cache and scratch buffer, which is
                // heap corruption rather than a stale name; and a mutex around it would let an app thread
                // block behind the sampler resolving a hundred threads' stacks. See FrameNameResolver.h.
                _frameNames.reset(new FrameNameResolver(_nameCache, corProfilerInfo));
            }
            catch (const std::exception&)
            {
                LogError(L"AllocationSampler: failed to create the frame name resolver; frames will be empty");
            }
        }

        // Whether allocation sampling can work in this process at all. Two independent preconditions:
        //   1. the runtime exposes the EventPipe profiling API (ICorProfilerInfo12) -- absent on .NET
        //      Framework and pre-.NET 5 CoreCLR; and
        //   2. the profiler actually holds COR_PRF_HIGH_MONITOR_EVENT_PIPE, without which the runtime
        //      never delivers EventPipeEventDelivered (see MarkUnavailable).
        // The owner checks this before setting the mask bit and before calling Start().
        bool IsAvailable() const noexcept
        {
            return _corProfilerInfo12 != nullptr && !_eventDeliveryUnavailable.load();
        }

        // Called by the owner when it could NOT enable COR_PRF_HIGH_MONITOR_EVENT_PIPE (SetEventMask2
        // rejected it, so the mask was re-applied without it). Without this, the sampler would have no way
        // to know, and Start() would happily open an EventPipe session that makes the CLR generate
        // AllocationTick events at full cost while EventPipeEventDelivered is never invoked for them --
        // paying the entire overhead of the feature for exactly zero samples, which is strictly worse than
        // not having it. Disarms permanently: it describes a fact about this process, fixed at startup.
        //
        // Deliberately does NOT clear _corProfilerInfo12: Stop()/Shutdown() still need that interface if a
        // session somehow exists, and conflating "the interface is missing" with "the mask bit is missing"
        // would make the two failure modes indistinguishable in the logs.
        void MarkUnavailable() noexcept
        {
            _eventDeliveryUnavailable.store(true);
            LogWarn(L"AllocationSampler: EventPipe event delivery is unavailable in this process; "
                L"allocation sampling is disabled and Start() will be ignored");
        }

        // Open the AllocationTick EventPipe session and arm the handler. No-op (logged) when
        // ICorProfilerInfo12 is unavailable. Idempotent: a second Start() after a Stop() re-arms the
        // handler and resets the sub-sampler WITHOUT opening a second session -- overwriting _sessionId
        // would orphan the first session (nothing could ever stop it) and double the runtime's event cost.
        //
        // Serialized against Shutdown() (and against another Start()) by _lifecycleMutex, so the
        // check-then-open of the session below cannot interleave with a claim-then-stop. Lifecycle callers
        // may block on that mutex; the tick path never touches it.
        //
        // REFUSES to arm once Shutdown() has completed. Mutual exclusion alone is not enough: a Start()
        // that runs strictly AFTER Shutdown() returned would open a brand-new session and re-arm an object
        // the owner considers dead and is about to destroy -- and no further Shutdown() is coming to close
        // that session. The _shutdownComplete latch makes Shutdown() terminal, which is what lets the owner
        // shut down without first proving that no Start() can still arrive. Use Stop()/Start() for pausing
        // and resuming; Shutdown() is one-way.
        void Start(uint32_t maxSamplesPerMinute) noexcept
        {
            // Both preconditions, not just the interface: opening a session the runtime will never deliver
            // events for costs the full AllocationTick generation overhead and yields nothing (see
            // MarkUnavailable).
            if (!IsAvailable())
            {
                LogDebug(L"AllocationSampler: Start ignored; allocation sampling is unavailable in this process ",
                    L"(either the runtime has no EventPipe profiling API, or the profiler could not subscribe "
                    L"to EventPipe events -- see the startup logs for which)");
                return;
            }

            try
            {
                std::lock_guard<std::mutex> lifecycleLock(_lifecycleMutex);

                if (_shutdownComplete.load())
                {
                    LogDebug(L"AllocationSampler: Start ignored; Shutdown() has already run and is terminal "
                        L"(use Stop()/Start() to pause and resume instead)");
                    return;
                }

                {
                    // Held so a tick in flight can never observe a half-replaced sub-sampler. Start() runs
                    // on the agent's own thread, where a brief wait is fine (unlike the tick path, which
                    // only ever try_locks).
                    std::lock_guard<std::mutex> tickLock(_tickMutex);
                    _subSampler.reset(new AllocationSubSampler(maxSamplesPerMinute, SubSampleCycleSeconds));
                }

                if (_sessionId.load() != 0)
                {
                    _sessionActive.store(true);
                    LogTrace(L"AllocationSampler: re-armed the existing EventPipe session");
                    return;
                }

                COR_PRF_EVENTPIPE_PROVIDER_CONFIG providerConfig{};
                providerConfig.providerName = _X("Microsoft-Windows-DotNETRuntime");
                // GCKeyword (0x1). AllocationTick is a GC-keyword event and, despite being documented at
                // Informational level, is only emitted at Verbose.
                providerConfig.keywords = 0x1;
                providerConfig.loggingLevel = COR_PRF_EVENTPIPE_VERBOSE;
                providerConfig.filterData = nullptr;

                EVENTPIPE_SESSION sessionId = 0;
                HRESULT hr = _corProfilerInfo12->EventPipeStartSession(1, &providerConfig, FALSE, &sessionId);
                if (FAILED(hr))
                {
                    LogError(L"AllocationSampler: EventPipeStartSession failed: ", std::hex, std::showbase, hr,
                        std::resetiosflags(std::ios_base::basefield | std::ios_base::showbase));
                    _sessionActive.store(false);
                    return;
                }

                // Publish the id BEFORE arming the handler so Shutdown() can always find a session that
                // ticks may already be arriving on.
                _sessionId.store(sessionId);
                _sessionActive.store(true);
                LogInfo(L"AllocationSampler: EventPipe session started");
            }
            catch (const std::exception&)
            {
                _sessionActive.store(false);
                LogError(L"AllocationSampler: exception starting the EventPipe session");
            }
        }

        // Stop producing samples but leave the EventPipe session open. Mirrors ContinuousProfiler::Stop's
        // semantics (which parks its worker thread rather than destroying it): the expensive/risky
        // teardown lives in Shutdown(). The runtime keeps raising AllocationTick, but the handler returns
        // on its first branch.
        //
        // SEALS AND PUBLISHES a pending (still-accumulating) batch on the way out, so the samples a tick
        // added since the last managed read are handed over instead of sitting in a buffer nothing will look
        // at again until some later Start(). Doing that needs _tickMutex, taken here with a BLOCKING lock
        // exactly as Start() and Shutdown() already take it -- the real constraint is not "lifecycle calls
        // never take _tickMutex" (they do) but the LOCK ORDER: _lifecycleMutex then _tickMutex, never the
        // reverse, and never a blocking _tickMutex acquisition from the tick or read paths (they try_lock,
        // which is what keeps Shutdown()'s drain deterministic -- nothing can be QUEUED ahead of it there).
        // Since every lifecycle call holds _lifecycleMutex first, they serialize with each other before ever
        // reaching _tickMutex, so this cannot queue behind another lifecycle call's hold of it either.
        //
        // COST, WHICH IS NEW AND NO LONGER SHUTDOWN-ONLY: acquiring _tickMutex can wait out one in-flight
        // tick's full walk + name resolution + encode (the same bounded wait Shutdown()'s drain has always
        // paid, described there). Stop() is not only called at teardown -- the managed side calls it on a
        // config-driven disable, a heap stop command, and on a send-failure backoff trip, and that last one
        // runs on the DRAIN thread while holding the managed service's own lifecycle lock. So a backoff trip
        // can now block the drain thread for about one sample's work. That is a millisecond-scale wait on a
        // path that is already doing I/O, and the alternative is stranding the accumulated batch, but it is
        // worth knowing before adding anything heavier to the tick path.
        //
        // Takes _lifecycleMutex for the same reason Shutdown() does: a bare store racing an in-progress
        // Start() can be overwritten by that Start()'s own arm, so Stop() would return having silently not
        // stopped. No use-after-free risk in that case (just a stale-enabled sampler), but "Stop() stops"
        // is worth making true rather than documenting an exception to. Blocking here is fine -- this is a
        // lifecycle call off any hot path -- though it does mean Stop() waits out a concurrent Shutdown().
        void Stop() noexcept
        {
            try
            {
                std::lock_guard<std::mutex> lifecycleLock(_lifecycleMutex);
                _sessionActive.store(false);

                // Disarmed first, so this runs with no further ticks able to append: take _tickMutex (which
                // also waits out the one tick that may be in flight) and hand over whatever accumulated.
                // Publishes only if the queue has room; if it does not, the batch simply stays pending, which
                // is no worse than before.
                std::lock_guard<std::mutex> tickLock(_tickMutex);
                try
                {
                    _pendingBatch.FlushIfPending();
                }
                catch (...)
                {
                    // Sealing writes to the buffer, so a throw can leave a partial tail that must never be
                    // published (see EncodeAndPublish's phase-2 catch).
                    _pendingBatch.AbandonBatch();
                    LogTrace(L"AllocationSampler: exception flushing the pending allocation batch on stop; discarded it");
                }
            }
            catch (const std::exception&)
            {
                // A lock_guard on a valid mutex does not throw in practice; if it somehow did, fall back to
                // the unsynchronized store so Stop() still disarms in the common (uncontended) case.
                _sessionActive.store(false);
            }
        }

        // Close the EventPipe session and DRAIN any tick handler still in flight. Must be called
        // explicitly (the destructor cannot do this job -- see ~AllocationSampler) and only from a thread
        // that is allowed to block for a bounded time, i.e. the agent's own shutdown path -- never from a
        // tick handler.
        //
        // Two distinct hazards are handled here:
        //
        // 1. HANG. EventPipeStopSession has to rendezvous with the runtime's own EventPipe machinery, so it
        //    runs on a DETACHED thread under a bounded wait. On timeout the call is abandoned (the detached
        //    thread holds its own references, so nothing dangles) instead of hanging agent shutdown behind
        //    the runtime. std::async is deliberately NOT used: the std::future it returns has a BLOCKING
        //    destructor, which would silently undo the bounded wait -- the exact hang this avoids.
        //
        // 2. USE-AFTER-FREE. Clearing _sessionActive does not evict a handler that already passed that
        //    gate: it may be mid stack-walk, holding _tickMutex, using _frameNames / _nameCache /
        //    _pendingBatch / _sampleBuffers. If the owner destroys this object right after Shutdown()
        //    returns, those members would vanish underneath that thread. So Shutdown() ends by taking
        //    _tickMutex with a BLOCKING lock: since the handler only ever try_locks, nothing can be queued
        //    behind it, and acquiring it means the last in-flight handler has finished. This matters most
        //    in the timeout case above, where the session is still OPEN and ticks keep arriving -- they
        //    then bounce off the _sessionActive gate (and off the re-check inside the lock) without
        //    touching any member.
        //
        // The disarm below MUST happen under _lifecycleMutex, not before taking it. Clearing
        // _sessionActive first and then blocking on the mutex lets a concurrent Start() win the lock,
        // re-arm the handler, and return -- so this method would drain once and then hand back an object
        // that is still armed, with ticks free to run a full walk/encode against members the owner is now
        // entitled to destroy. That is hazard 2 all over again, entered through the lifecycle door.
        void Shutdown() noexcept
        {
            try
            {
                std::lock_guard<std::mutex> lifecycleLock(_lifecycleMutex);

                // Authoritative disarm + terminal latch, both set before anything else in the critical
                // section. Any Start() racing this is either queued on this same mutex (and will then
                // observe _shutdownComplete and refuse to arm) or already finished before we got here, so
                // _sessionActive cannot be resurrected between this store and the drain below -- and no
                // LATER Start() can resurrect it either. Setting the latch first also means the degraded
                // paths below (a stop that times out, or an exception) still leave the object terminal.
                _sessionActive.store(false);
                _shutdownComplete.store(true);

                // Claim the session so a concurrent/second Shutdown() cannot stop the same session twice.
                const EVENTPIPE_SESSION sessionId = _sessionId.exchange(0);
                if (_corProfilerInfo12 && sessionId != 0)
                {
                    StopSessionWithBoundedWait(sessionId);
                }

                // Drain: see hazard 2 above. This is NOT instantaneous and is not hard-bounded by a
                // timeout: it can wait for one in-flight tick's full stack walk + name resolution +
                // encode, and if the periodic sampler suspends the runtime while that handler is in its
                // (unserialized) resolve/encode phase, for that suspend window too. It is bounded in
                // practice -- one sample's work -- and is the price of handing back an object the owner can
                // safely destroy.
                std::lock_guard<std::mutex> drainLock(_tickMutex);
            }
            catch (const std::exception&)
            {
                // Reachable only if locking itself fails (a pathological std::system_error), i.e. before
                // the stores above ran. Disarm and latch anyway: this is the path where the owner is about
                // to destroy the object, so returning with the sampler still armed -- and still re-armable
                // by a later Start() -- is the worst possible outcome. Mirrors Stop()'s fallback. NOTE: no
                // drain happens on this path (the mutex that failed is the one guarding it), so a handler
                // in flight at that instant is not waited for; nothing better is available if the runtime
                // can no longer lock a mutex.
                _sessionActive.store(false);
                _shutdownComplete.store(true);
                LogError(L"AllocationSampler: exception stopping the EventPipe session; the sampler is disarmed "
                    L"but its EventPipe session (if any) could not be closed");
            }
        }

        // Drain the oldest filled sample buffer into the caller's array; the allocation-sample counterpart
        // of ContinuousProfiler::ReadThreadSamples, decoded by the same managed BufferParser. Returns the
        // number of bytes written (0 when nothing is ready or the args are invalid). Never throws.
        //
        // The managed drain calls this REPEATEDLY until it returns 0, so one drain collects every batch
        // the sampler is holding -- which is what makes multi-batch accumulation useful rather than just
        // deferred.
        int32_t ReadAllocationSamples(int32_t len, unsigned char* buf) noexcept
        {
            if (buf == nullptr || len <= 0)
            {
                return 0;
            }

            try
            {
                // Hand over a batch still accumulating under back-pressure before reading, so a drain
                // picks it up in THIS sweep. Without this it would sit unsealed until the next allocation
                // tick noticed a free slot -- i.e. indefinitely once the workload stops allocating, losing
                // the tail of every burst.
                //
                // try_lock, never lock: _tickMutex is held by tick handlers on customer threads, and
                // Shutdown()'s drain depends on nothing ever QUEUEING on it. A lost race just defers the
                // flush to the next read. The latch re-check under the lock keeps this off an object
                // Shutdown() has already declared finished (and whose owner may be about to destroy it).
                {
                    std::unique_lock<std::mutex> flushLock(_tickMutex, std::try_to_lock);
                    if (flushLock.owns_lock() && !_shutdownComplete.load())
                    {
                        // Caught HERE, not by the outer handler, for two reasons: a failed flush must not
                        // cost the caller the batch that is already sitting in the queue (the read below
                        // still has to happen), and sealing a batch is a write -- a throw mid-seal leaves a
                        // partial tail that must never be published, so the batch is abandoned.
                        try
                        {
                            _pendingBatch.FlushIfPending();
                        }
                        catch (...)
                        {
                            _pendingBatch.AbandonBatch();
                            LogTrace(L"AllocationSampler: exception flushing the pending allocation batch; discarded it");
                        }
                    }
                }

                return _sampleBuffers.Read(len, buf);
            }
            catch (...)
            {
                LogTrace(L"AllocationSampler: exception draining sample buffer");
            }

            return 0;
        }

        // Handle one AllocationTick. Runs ON THE ALLOCATING APP THREAD inside the runtime's EventPipe
        // dispatch (see the class comment). Never throws, never blocks.
        //
        // Takes eventId/eventVersion and re-checks them itself rather than trusting the caller's filter.
        // The check is two integer comparisons, and this is the ONE place in the feature where getting it
        // wrong produces silently WRONG telemetry instead of none: ParseAllocationTickPayload reads
        // AllocatedSize from the END of the buffer, so a v2/v3 payload parses "successfully" into a slice
        // of the Address field. Defense in depth against a future caller or refactor that forgets to
        // pre-filter.
        void OnAllocationTick(DWORD eventId, DWORD eventVersion, ULONG dataLen, LPCBYTE data) noexcept
        {
            if (!IsAllocationTickEvent(eventId, eventVersion))
            {
                return;
            }

            if (!_sessionActive.load())
            {
                return;
            }

            try
            {
                // Serialize tick handling across app threads (see the class comment). try_lock, never
                // lock: a concurrent tick is dropped, not queued behind another thread's stack walk. It is
                // also what lets Shutdown() drain safely -- nothing is ever queued on this mutex.
                std::unique_lock<std::mutex> tickLock(_tickMutex, std::try_to_lock);
                if (!tickLock.owns_lock())
                {
                    return;
                }

                // Re-check under the lock: Stop()/Shutdown() may have fired between the gate above and
                // acquiring the mutex, and past this point the handler touches shared state that Shutdown()
                // is entitled to consider quiesced.
                if (!_sessionActive.load())
                {
                    return;
                }

                // Rate limit BEFORE any stack walk (AllocationSubSampler's documented contract). Ticks
                // dropped by the try_lock above are not counted here, so the sub-sampler's estimate of
                // per-cycle tick volume is a slight undercount on heavily-parallel workloads -- it paces
                // conservatively as a result, which is the harmless direction.
                if (!_subSampler || !_subSampler->ShouldSample())
                {
                    return;
                }

                uint64_t allocatedSize = 0;
                xstring_t typeName;
                if (!ParseAllocationTickPayload(dataLen, data, allocatedSize, typeName))
                {
                    return; // malformed / unexpected-version payload -- drop it, never guess
                }

                // Back-pressure before the expensive part -- but note what this gate does NOT test any
                // more. "Both queue slots are full" is no longer a reason to drop a tick: the sample joins
                // the PENDING batch instead, and that batch is published as soon as the reader frees a
                // slot (see AllocationBatchAccumulator, which exists because dropping here capped delivery
                // at one sample per drain interval regardless of the configured budget). Only the
                // genuinely saturated state -- no free slot AND a pending batch with no room left -- is
                // still dropped before the walk, because there is then nowhere for the result to go.
                // Safe as a gate because _tickMutex makes this the single producer.
                if (!_pendingBatch.CanAcceptSample())
                {
                    _pendingBatch.RecordDroppedSample();
                    LogTrace(L"AllocationSampler: sample buffers and pending batch both full; skipping tick (reader has not drained)");
                    return;
                }

                // Own-thread trace-context read: unlike ContinuousProfiler's cross-thread read, there is no
                // concurrent writer to race -- the only writer of this slot is this very thread. (Even in
                // the impossible case of a tick landing inside that thread's own seqlock write, TryGet is
                // wait-free and simply reports "no context".)
                ThreadID currentThreadId = 0;
                _corProfilerInfo->GetCurrentThreadID(&currentThreadId);
                TraceContext context{};
                if (_traceContexts != nullptr)
                {
                    _traceContexts->TryGet(currentThreadId, context);
                }

                std::vector<FunctionID> functionIds;
                functionIds.reserve(MaxStackFramesSupported);
                StackWalkContext walkContext{ &functionIds, false };

                {
                    // Serialize with ContinuousProfiler/ThreadProfiler: DoStackSnapshot must not run against
                    // a thread that another walk is already targeting, and the periodic sampler walks EVERY
                    // managed thread -- including this one. try_lock, so a tick that collides with a
                    // periodic sample is dropped rather than parking an app thread for a whole suspend
                    // window. Released before the (much longer) resolve/encode phase, which needs no
                    // serialization of its own.
                    std::unique_lock<NewRelic::Profiler::SuspendMutex> suspendLock(
                        NewRelic::Profiler::SuspendMutex::Shared(), std::try_to_lock);
                    if (!suspendLock.owns_lock())
                    {
                        LogTrace(L"AllocationSampler: skipping tick, the periodic sampler holds the suspend mutex");
                        return;
                    }

                    // NULL target thread == walk the CURRENT thread synchronously; no thread is suspended
                    // by this call. That is also why the callback below may allocate, unlike
                    // ContinuousProfiler's strictly zero-alloc one.
                    const HRESULT walkResult = _corProfilerInfo->DoStackSnapshot(static_cast<ThreadID>(0),
                        &StaticAllocationStackFrameCallback, COR_PRF_SNAPSHOT_INFO::COR_PRF_SNAPSHOT_DEFAULT,
                        &walkContext, nullptr, 0);

                    // A stack deeper than the frame cap reports failure (CORPROF_E_STACKSNAPSHOT_ABORTED)
                    // because the callback deliberately aborted the walk; those leaf-most frames are good,
                    // so keep the sample -- exactly ContinuousProfiler's truncation handling.
                    if (FAILED(walkResult) && !walkContext.Truncated)
                    {
                        LogTrace(L"AllocationSampler: DoStackSnapshot failed for allocation sample");
                        return;
                    }
                }

                EncodeAndPublish(currentThreadId, context, allocatedSize, typeName, functionIds);
            }
            catch (...)
            {
                // A COM callback boundary: swallow everything. Logging is safe here (no runtime suspend is
                // in progress on this path) but is kept to Finest -- this can fire per allocation tick.
                LogTrace(L"AllocationSampler: exception handling allocation tick");
            }
        }

        // Parse the AllocationTick **v4** payload. Ported field-for-field from OpenTelemetry's
        // opentelemetry-dotnet-instrumentation continuous_profiler.cpp (ContinuousProfiler::AllocationTick),
        // which is the authoritative description of this ETW-shaped blob:
        //
        //   AllocationAmount   int32
        //   AllocationKind     int32
        //   InstanceId         int16
        //   AllocationAmount64 int64
        //   TypeId             pointer-sized
        //   TypeName           UTF-16, NUL-terminated, VARIABLE length
        //   HeapIndex          int32
        //   Address            pointer-sized
        //   AllocatedSize      int64   <- LAST field (appended in v4 after the v3 fields)
        //
        // Only AllocatedSize and TypeName are consumed downstream, and because TypeName is variable-length
        // they are read from OPPOSITE ends: TypeName from the fixed front offset, AllocatedSize from the
        // last 8 bytes. Pointer-sized fields use sizeof(void*) -- the payload is emitted by the runtime
        // hosting us, so its pointer width is ours (correct for both x64 and x86).
        //
        // Static + stateless so it is directly unit-testable without a CLR. Never throws.
        static bool ParseAllocationTickPayload(ULONG dataLen, LPCBYTE data, uint64_t& allocatedSize, xstring_t& typeName) noexcept
        {
            allocatedSize = 0;
            typeName.clear();

            if (data == nullptr)
            {
                return false;
            }

            const size_t len = static_cast<size_t>(dataLen);

            // OTel's own guard against a buffer under-read, ported as-is: the payload must be long enough
            // for every fixed field plus at least the NUL terminator of TypeName, and whatever remains
            // beyond the fixed fields must be a whole number of UTF-16 code units. Anything else is a
            // truncated or differently-shaped payload -- reject it without reading a single byte.
            if (len < AllocationTickV4SizeWithoutTypeName + sizeof(xchar_t) ||
                (len - AllocationTickV4SizeWithoutTypeName) % sizeof(xchar_t) != 0)
            {
                return false;
            }

            // memcpy rather than a reinterpret_cast dereference: the field is not guaranteed to be aligned
            // for a uint64_t, and this is also strict-aliasing clean.
            uint64_t size = 0;
            std::memcpy(&size, data + (len - sizeof(uint64_t)), sizeof(uint64_t));

            // Character count EXCLUDING the NUL terminator. Constructed with an explicit length: the
            // terminator is not assumed to be reachable, so no NUL-scanning constructor is used.
            const size_t typeNameCharLen = (len - AllocationTickV4SizeWithoutTypeName) / sizeof(xchar_t) - 1;

            try
            {
                // UTF-16LE in the payload; xchar_t is the platform's 2-byte UTF-16 code unit (wchar_t on
                // Windows, char16_t under the PAL), so this is a straight copy on both.
                typeName.assign(reinterpret_cast<const xchar_t*>(data + AllocationTickV4TypeNameStartByteIndex), typeNameCharLen);
            }
            catch (...)
            {
                return false; // only an allocation failure can land here
            }

            allocatedSize = size;
            return true;
        }

        // NOTE: intentionally NOT `noexcept = default` -- see the identical note on ContinuousProfiler's
        // constructor (clang computes the implicit spec from members that allocate, MSVC does not).
        AllocationSampler() = default;

        // TEARDOWN-SAFE, and deliberately NOT a call to Shutdown().
        //
        // This destructor can run while the CLR/loader is tearing the process down (DLL_PROCESS_DETACH, or
        // after ExitProcess has already terminated every other thread), where Shutdown()'s machinery is not
        // merely slow but wrong:
        //   * Creating a thread under the loader lock is illegal, and a thread created during
        //     DLL_PROCESS_DETACH cannot even begin to run until DllMain returns -- so the stopper thread
        //     could never reach EventPipeStopSession and the bounded wait would be GUARANTEED to burn its
        //     full timeout. Same outcome after ExitProcess: the other threads are already gone.
        //   * Calling EventPipeStopSession inline is no better -- it rendezvouses with runtime threads that
        //     may no longer exist.
        //   * Blocking on _tickMutex is unsafe too: a thread terminated by ExitProcess while holding it
        //     would never release it, deadlocking process exit.
        // So this path is INSTANT: no thread, no timed wait, no blocking lock. The session (if any) is
        // simply abandoned -- it dies with the process, which is the only scenario this path can be in.
        //
        // Consequence: Task 4's owner MUST call Shutdown() explicitly while the process is still healthy.
        // Reaching the log line below means that did not happen.
        //
        // Residual, knowingly accepted: the LogError calls below take the global logger's mutex, which is
        // the same class of hazard this path avoids elsewhere (a thread killed mid-teardown while holding
        // that mutex would hang us here). They are kept because this is the only signal that Shutdown()
        // was skipped, and because ContinuousProfiler's own destructor already logs on this same path --
        // so it is pre-existing precedent, not a new risk. It is NOT fully solved; if teardown hangs are
        // ever observed here, dropping these two lines is the fix.
        ~AllocationSampler() noexcept
        {
            // Disarm and latch (no lock -- see above; a Start() racing destruction is already unrecoverable,
            // but the latch means one that merely queued behind us cannot arm a half-destroyed object).
            _sessionActive.store(false);
            _shutdownComplete.store(true);

            if (_sessionId.exchange(0) != 0)
            {
                LogError(L"AllocationSampler: destroyed with an EventPipe session still open -- Shutdown() was never "
                    L"called. Abandoning the session instead of stopping it, because this path may be running under "
                    L"process/DLL teardown where stopping it cannot succeed and would hang.");
            }

            // Best-effort drain only (try_lock, never block -- see above). Under normal teardown no tick
            // can be in flight; if one somehow is, there is nothing safe left to do about it here.
            std::unique_lock<std::mutex> drainLock(_tickMutex, std::try_to_lock);
            if (!drainLock.owns_lock())
            {
                LogError(L"AllocationSampler: destroyed while an allocation tick was still in flight");
            }
        }

        AllocationSampler(const AllocationSampler&) = delete;
        AllocationSampler(AllocationSampler&&) = delete;
        AllocationSampler& operator=(const AllocationSampler&) = delete;
        AllocationSampler& operator=(AllocationSampler&&) = delete;

    private:
        using NameCache = NewRelic::Profiler::ThreadProfiler::NameCache;

        // Pointer width of the process emitting the payload -- which is this process (see
        // ParseAllocationTickPayload), so our own pointer size is the right one on x64 and x86 alike.
        static constexpr size_t EtwPointerSize = sizeof(void*);

        // Byte offset of the variable-length TypeName field: AllocationAmount(4) + AllocationKind(4) +
        // InstanceId(2) + AllocationAmount64(8) + TypeId(ptr).
        static constexpr size_t AllocationTickV4TypeNameStartByteIndex = 4 + 4 + 2 + 8 + EtwPointerSize;

        // Total size of every FIXED field, i.e. the whole payload minus TypeName's bytes: the fields above
        // plus HeapIndex(4) + Address(ptr) + AllocatedSize(8).
        static constexpr size_t AllocationTickV4SizeWithoutTypeName = 4 + 4 + 2 + 8 + EtwPointerSize + 4 + EtwPointerSize + 8;

        // Per-thread frame cap, mirroring ContinuousProfiler::MaxStackFramesSupported: the CLR walks
        // leaf-first, so the frames captured before the cap are the leaf-most ones -- where the allocation
        // actually happened. Bounds the walk on pathological/runaway recursion.
        static constexpr size_t MaxStackFramesSupported = 128;

        // Hard ceiling on one encoded allocation BATCH -- which, since a batch can now accumulate many
        // samples under back-pressure, is also what bounds how many samples one drain interval can deliver
        // (the sampler holds at most three batches between drains: two SampleBufferQueue slots plus the
        // pending one). So it is a throughput knob, not just a safety cap, and the per-sample cost that
        // divides into it is the MARGINAL cost of appending sample N to an ALREADY-OPEN batch -- NOT the size
        // of a standalone one-sample batch, and not the size of the OTLP profile built from it. Two regimes,
        // because the per-batch frame-interning table makes the difference enormous:
        //
        //   * REPEATED stacks (one allocating loop -- the common shape, and what the integration test
        //     exercises): every frame after its first sight is a 2-byte back-reference, so a sample costs
        //     opcode(1) + threadName(2+) + 6 int64s(48) + typeName(~28) + 2 bytes/frame + terminator(2)
        //     ~= 95-150 bytes. Enforced, not assumed: AllocationBatchAccumulatorTest's
        //     MarginalBytesPerAppendedSample_StaysSmallForARepeatedStack asserts it. 64 KB would already
        //     hold ~500 such samples.
        //   * DISTINCT stacks (a service allocating from many call sites): each new frame costs a full inline
        //     definition, 4 + 2*chars bytes, so ~20 fresh 60-char frames put a sample at ~2.5 KB. THIS is the
        //     regime that sizes the cap: the shipped defaults (200 samples/minute at a 10 s drain) demand
        //     ~33 samples per interval, which at 2.5 KB is ~83 KB -- more than 64 KB, i.e. the old value
        //     could not deliver the DEFAULT budget for a diverse workload even with batching. 128 KB covers
        //     ~50 such samples, and ~1300 in the repeated-stack regime.
        //
        // Cost of the headroom is bounded and only paid under load: the buffers are std::vectors that grow to
        // what is actually written, so the worst case (3 x this) only materializes while genuinely
        // backpressured on a diverse workload; steady state stays at a few KB. Still far below
        // ContinuousProfiler's 4 MB per-tick all-threads batch, and well inside the managed drain buffer
        // (DrainBufferSize, 4 MB). A budget large enough to exceed this cap is now visible rather than silent
        // (Supportability/DotNET/ContinuousProfiling/AllocationSamplesDropped).
        static constexpr size_t MaxAllocationBufferBytes = 128 * 1024;

        // The sub-sampler's cycle length. maxSamplesPerMinute is a per-MINUTE budget, hence 60.
        static constexpr uint32_t SubSampleCycleSeconds = 60;

        // Bound on how long Shutdown() waits for EventPipeStopSession before abandoning it.
        static constexpr uint32_t StopSessionTimeoutSeconds = 5;

        // Rendezvous between Shutdown() and the detached thread that calls EventPipeStopSession. Held by
        // shared_ptr on BOTH sides so an abandoned (timed-out) stop can still complete and signal without
        // touching freed memory.
        struct StopSessionSignal
        {
            std::mutex Mtx;
            std::condition_variable Cv;
            bool Done{ false };
        };

        // Run EventPipeStopSession on a detached thread and wait a bounded time for it. Called only from
        // the explicit Shutdown() path (never the destructor -- see ~AllocationSampler for why). The
        // detached thread holds its own CComPtr + shared signal, so abandoning it on timeout dangles
        // nothing.
        void StopSessionWithBoundedWait(EVENTPIPE_SESSION sessionId) noexcept
        {
            try
            {
                auto signal = std::make_shared<StopSessionSignal>();
                CComPtr<ICorProfilerInfo12> info12 = _corProfilerInfo12; // keeps the interface alive for the detached thread

                std::thread stopper([info12, sessionId, signal]() {
                    info12->EventPipeStopSession(sessionId);
                    {
                        std::lock_guard<std::mutex> l(signal->Mtx);
                        signal->Done = true;
                    }
                    signal->Cv.notify_all();
                });
                stopper.detach();

                std::unique_lock<std::mutex> l(signal->Mtx);
                // Copied into a local on purpose. std::chrono::duration's converting constructor takes its
                // argument by CONST REFERENCE, which odr-uses StopSessionTimeoutSeconds -- and the Linux
                // build is C++11/14, where a static constexpr member that is odr-used needs an out-of-line
                // definition no header-only class can provide (there are no inline variables before C++17).
                // Passing a local avoids that: MSVC elides it either way, clang would otherwise leave an
                // undefined symbol in libNewRelicProfiler.so that only surfaces at load time.
                const uint32_t stopTimeoutSeconds = StopSessionTimeoutSeconds;
                const bool stopped = signal->Cv.wait_for(l, std::chrono::seconds(stopTimeoutSeconds),
                    [signal]() { return signal->Done; });

                if (stopped)
                {
                    LogTrace(L"AllocationSampler: EventPipe session stopped");
                }
                else
                {
                    LogError(L"AllocationSampler: EventPipeStopSession did not return within ",
                        static_cast<uint32_t>(StopSessionTimeoutSeconds), L"s; abandoning it rather than blocking "
                        L"shutdown. The session stays open, so ticks may keep arriving -- they are gated off by "
                        L"_sessionActive and cannot touch sampler state.");
                }
            }
            catch (const std::exception&)
            {
                LogError(L"AllocationSampler: exception stopping the EventPipe session");
            }
        }

        // Context for the stack-walk callback: where to append FunctionIDs, and whether the walk was
        // deliberately aborted at the frame cap (vs. having genuinely failed).
        struct StackWalkContext
        {
            std::vector<FunctionID>* Ids;
            bool Truncated;
        };

        // Per-frame snapshot callback for the CURRENT thread's walk. NOT running under a runtime suspend
        // (nothing suspends anything on this path), so an ordinary vector push_back is safe here -- the
        // zero-allocation discipline that is mandatory in ContinuousProfiler's callback is not load-bearing
        // here, and this path fires orders of magnitude less often (sub-sampled) than the periodic one.
        //
        // Returning non-S_OK makes the CLR abort the walk (DoStackSnapshot then reports
        // CORPROF_E_STACKSNAPSHOT_ABORTED); frames already recorded survive the abort.
        static HRESULT __stdcall StaticAllocationStackFrameCallback(uintptr_t functionId, uintptr_t /* instructionPointer */,
            uintptr_t /* frameInfo */, uint32_t /* contextSize */, uint8_t[] /* context */, void* clientData)
        {
            const HRESULT StackTooDeep = S_FALSE;

            try
            {
                auto* walkContext = static_cast<StackWalkContext*>(clientData);
                if (walkContext == nullptr || walkContext->Ids == nullptr)
                {
                    return StackTooDeep; // nothing to write into; stop walking rather than spin
                }

                if (walkContext->Ids->size() >= MaxStackFramesSupported)
                {
                    walkContext->Truncated = true;
                    return StackTooDeep;
                }

                walkContext->Ids->push_back(static_cast<FunctionID>(functionId));
            }
            catch (...)
            {
                // Never let an exception cross back into the CLR's stack walker.
            }

            return S_OK;
        }

        // Encoded size of a length-prefixed string field: the big-endian int16 char count plus the
        // UTF-16LE code units, with the encoder's own 512-char cap applied (see WriteString).
        static size_t EncodedStringBytes(const xstring_t& value) noexcept
        {
            const size_t chars = value.size() < SampleBufferWriter::MaxStringChars
                ? value.size() : SampleBufferWriter::MaxStringChars;
            return 2 + (chars * 2);
        }

        // Encoded size of one frame-list entry at its WORST case: a 2-byte code plus a full inline string
        // definition (what a frame not yet interned in this batch costs).
        static size_t EncodedFrameBytes(const xstring_t& frame) noexcept
        {
            return 2 + EncodedStringBytes(frame);
        }

        // Encoded size of everything in an allocation sample except its frame list: the 0x08 opcode, the
        // thread name, six int64 fields (os thread id, traceIdHigh, traceIdLow, spanId, timestamp,
        // allocated size), the type name, and the frame-list terminator.
        static size_t AllocationSampleFixedBytes(const xstring_t& threadName, const xstring_t& typeName) noexcept
        {
            return 1 + EncodedStringBytes(threadName) + (6 * 8) + EncodedStringBytes(typeName) + 2;
        }

        // Resolve names and encode this sample into the pending batch, publishing that batch when there is
        // a queue slot for it (see AllocationBatchAccumulator for why a batch can span several ticks).
        // Runs on the allocating thread with no lock held beyond _tickMutex: metadata calls, string
        // building and allocation are all fine here (nothing is suspended). Never throws.
        void EncodeAndPublish(ThreadID managedThreadId, const TraceContext& context, uint64_t allocatedSize,
            const xstring_t& typeName, const std::vector<FunctionID>& functionIds) noexcept
        {
            // PHASE 1 -- resolve, in its own try/catch, because NOTHING here touches the pending batch and
            // this is where nearly all of this function's allocation happens (thread-name lookup, frame-name
            // building, NameCache inserts, the scratch vector's growth). A bad_alloc here must cost ONE
            // sample, exactly as it did before batching existed. Sharing the catch below would instead
            // discard an accumulated batch of up to hundreds of already-encoded, perfectly good samples --
            // the fix would have multiplied the blast radius of an allocation failure by the batch size.
            DWORD osThreadId = 0;
            int64_t nowNanos = 0;
            xstring_t threadName;
            try
            {
                // Not error-checked, exactly as in ContinuousProfiler::EnrichCapturedThreads: on failure
                // the id stays 0 and ResolveOsThreadName treats that as "no name".
                _corProfilerInfo->GetThreadInfo(managedThreadId, &osThreadId);

                nowNanos = static_cast<int64_t>(std::chrono::duration_cast<std::chrono::nanoseconds>(
                    std::chrono::system_clock::now().time_since_epoch()).count());

                threadName = ResolveOsThreadName(osThreadId);

                // Resolve every frame name BEFORE touching the batch, into a reused scratch vector. That
                // makes this sample's exact worst-case encoded size known up front, which is what lets the
                // accumulator decide at a SAMPLE boundary whether the sample fits the open batch -- rather
                // than starting to write it and truncating its frame list at the buffer's edge.
                _frameScratch.clear();
                for (const auto functionId : functionIds)
                {
                    _frameScratch.push_back(_frameNames ? _frameNames->ResolveFrameName(functionId) : xstring_t());
                }
            }
            catch (...)
            {
                // One sample lost, the open batch untouched and still deliverable.
                _pendingBatch.RecordDroppedSample();
                LogTrace(L"AllocationSampler: exception resolving allocation sample names; dropping this sample only");
                return;
            }

            // PHASE 2 -- encode into the batch. Every statement below either touches the batch or is
            // arithmetic that cannot throw, so this catch can abandon unconditionally: there is no way to
            // reach it without having been inside the encoder. (That is precisely why the resolve work was
            // lifted out above rather than guarded by a "did we start writing?" flag -- the split makes the
            // distinction structural instead of something a later edit could get wrong.)
            try
            {
                size_t requiredBytes = AllocationSampleFixedBytes(threadName, typeName);
                for (const auto& frame : _frameScratch)
                {
                    requiredBytes += EncodedFrameBytes(frame);
                }

                SampleBufferWriter* writer = _pendingBatch.BeginSample(requiredBytes, nowNanos);
                if (writer == nullptr)
                {
                    // Saturated: no slot to publish into and no room left in the pending batch. Already
                    // counted as a drop by the accumulator.
                    LogTrace(L"AllocationSampler: sample buffers and pending batch both full; dropping allocation sample");
                    return;
                }

                writer->WriteStartAllocationSample();
                writer->WriteThreadName(threadName);
                writer->WriteInt64Field(static_cast<int64_t>(osThreadId));
                writer->WriteInt64Field(context.TraceIdHigh);
                writer->WriteInt64Field(context.TraceIdLow);
                writer->WriteInt64Field(context.SpanId);
                writer->WriteInt64Field(nowNanos / 1000000); // sample timestamp, milliseconds
                writer->WriteUInt64Field(allocatedSize);
                writer->WriteStringField(typeName);

                for (const auto& frame : _frameScratch)
                {
                    // Only reachable for a sample whose frames alone exceed the whole buffer cap -- the
                    // accumulator has already guaranteed room for every other sample. The reservation
                    // covers this frame's worst case (a fresh, uninterned definition), the list
                    // terminator, and the batch's own closing records, so the batch can always be sealed.
                    if (!writer->WillFit(EncodedFrameBytes(frame) + 2 + AllocationBatchAccumulator::BatchTailBytes))
                    {
                        LogTrace(L"AllocationSampler: sample buffer full mid-sample; truncating the frame list");
                        break;
                    }

                    writer->WriteCodedFrameString(frame);
                }

                writer->WriteFrameListTerminator();

                // Publishes now if a slot is free (the common case, byte-identical to the old
                // one-sample-per-batch behavior); otherwise the batch stays open and the next tick appends
                // to it instead of being dropped.
                _pendingBatch.EndSample();
            }
            catch (...)
            {
                // A throw can leave a HALF-WRITTEN record in the open batch, and the encoder cannot roll
                // one back -- a partial record desynchronizes the managed decoder for every sample after
                // it. Discarding the whole pending batch (counted as dropped) is the only safe response.
                _pendingBatch.AbandonBatch();
                LogTrace(L"AllocationSampler: exception encoding allocation sample; discarded the pending batch");
            }
        }

        // Interface to the CLR execution engine and metadata services, provided during profiler Initialize.
        CComPtr<ICorProfilerInfo4> _corProfilerInfo;

        // EventPipe session control. Null on .NET Framework and pre-.NET 5 CoreCLR, which is exactly the
        // "allocation sampling unavailable" signal (see IsAvailable).
        CComPtr<ICorProfilerInfo12> _corProfilerInfo12;

        // Per-thread active trace context. NOT owned -- it is ContinuousProfiler's map, shared so that the
        // single managed trace-context export feeds both samplers (see Init).
        TraceContextMap* _traceContexts{ nullptr };

        // Serializes the whole tick handler across app threads; every state member below it is
        // single-threaded-only and protected by it. Only ever try_lock'd on the tick path (which is what
        // both keeps app threads unblocked and lets Shutdown() drain deterministically).
        std::mutex _tickMutex;

        // Serializes session lifecycle (Start/Shutdown) so a check-then-open can never interleave with a
        // claim-then-stop. Taken ONLY by lifecycle callers, which are allowed to block; never by the tick
        // path, and never by the destructor (blocking there could deadlock process exit).
        std::mutex _lifecycleMutex;

        // Rate limiter. Created by Start() (so a restart re-paces from zero) under _tickMutex.
        std::unique_ptr<AllocationSubSampler> _subSampler;

        // Whether the handler should produce samples. Toggled by Start()/Stop()/Shutdown() and read on
        // every tick before any lock is taken, so it is atomic rather than _tickMutex-guarded. It is the
        // gate that makes a post-Shutdown tick harmless (it returns before touching any other member).
        std::atomic<bool> _sessionActive{ false };

        // One-way latch set by Shutdown() (and the destructor) under _lifecycleMutex. Once true, Start()
        // refuses to arm, so Shutdown() is TERMINAL rather than merely momentary: no later Start() can
        // open a fresh session on an object its owner has already torn down. Never cleared -- a sampler
        // that has been shut down stays shut down; Stop()/Start() is the pause/resume pair. Atomic because
        // it is also read on the (unlocked) destructor path.
        std::atomic<bool> _shutdownComplete{ false };

        // Set once, at startup, by MarkUnavailable() when the owner could not enable
        // COR_PRF_HIGH_MONITOR_EVENT_PIPE. Distinct from _shutdownComplete: that one means "this sampler
        // is finished", this one means "this process can never deliver events to it". Atomic only because
        // IsAvailable() is called from lifecycle threads other than the one that set it.
        std::atomic<bool> _eventDeliveryUnavailable{ false };

        // The open EventPipe session, or 0 when none is open. Atomic, and claimed via exchange(0) on the
        // teardown paths, so two lifecycle callers can never both believe they own the same session (a
        // double EventPipeStopSession) nor both open one (an orphaned session nothing can ever stop).
        std::atomic<EVENTPIPE_SESSION> _sessionId{ 0 };

        // Type/method name cache. Declared BEFORE _frameNames, which borrows it, so reverse-order
        // destruction tears the resolver down first.
        NameCache _nameCache;

        // Frame-name resolution over _nameCache. This sampler's own instance -- see the note in Init().
        std::unique_ptr<FrameNameResolver> _frameNames;

        // Resolved frame names for the sample being encoded. A member so the vector's storage is reused
        // across ticks; resolution happens up front so the sample's exact encoded size is known before the
        // batch is touched (see EncodeAndPublish).
        std::vector<xstring_t> _frameScratch;

        // Two-slot FIFO hand-off to the managed reader (its own lock -- see SampleBufferQueue.h). Separate
        // from ContinuousProfiler's queue so allocation samples and thread samples cannot starve each
        // other, and so each is drained by its own managed reader. Declared BEFORE _pendingBatch, which
        // holds a reference to it.
        SampleBufferQueue _sampleBuffers;

        // Owns the encode buffer, the batch's writer (one instance, so frame interning stays consistent
        // across a multi-tick batch) and the accumulate/flush decision. Guarded by _tickMutex like every
        // other state member here, including on the FlushIfPending call the drain path makes.
        AllocationBatchAccumulator _pendingBatch{ _sampleBuffers, MaxAllocationBufferBytes };
    };
}}}
