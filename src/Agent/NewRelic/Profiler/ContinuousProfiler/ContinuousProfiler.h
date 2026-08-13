/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iterator>
#include <memory>
#include <mutex>
#include <thread>
#include <unordered_map>
#include <vector>

#include <cor.h>
#include <corprof.h>

#ifdef PAL_STDCPP_COMPAT
// Linux: /proc/self/task/<tid>/comm is the OS-tid-keyed source of a thread's name (pthread_getname_np
// needs a pthread_t, which we do not have for an arbitrary sampled OS thread id). Read AFTER resume.
// /proc/self/task/<tid>/stat (parsed for CPU-time classification) is read via the same <cstdio> API;
// sysconf(_SC_CLK_TCK) converts its clock-tick fields to microseconds.
#include <cstdio>
#include <unistd.h>
#endif

#include "../Logging/Logger.h"
#include "../ThreadProfiler/namecache.h"
#include "FrameNameResolver.h"
#include "OsThreadName.h"
#include "SampleBufferQueue.h"
#include "SampleBufferWriter.h"
#include "SuspendMutex.h"
#include "TraceContextMap.h"
#include "AgentWorkMap.h"

// ContinuousProfiler is the always-on counterpart to the collector-driven ThreadProfiler. Where the
// ThreadProfiler takes a single time-boxed profile on demand (RequestProfile), the ContinuousProfiler
// owns a long-lived worker thread that periodically samples every managed thread on a fixed interval.
//
// This class mirrors ThreadProfiler's lifecycle shape: Init() during profiler Initialize (no threads,
// no allocation), a lazily-created worker thread started/stopped via Start()/Stop(), and Shutdown()
// that signals + joins the worker.
//
// The capture itself mirrors ThreadProfiler exactly (see ThreadProfiler.h): on each tick the worker
// takes SuspendMutex::Shared() (serializing against the on-demand ThreadProfiler so the two never
// suspend the runtime concurrently), suspends the runtime (CoreCLR only), enumerates managed threads
// via EnumThreads, and walks each stack via DoStackSnapshot into PREALLOCATED per-thread frame
// buffers. Exactly as in ThreadProfiler, the snapshot callback does ZERO heap allocation and takes NO
// locks while the runtime is suspended (the hard rule at ThreadProfiler.h:23-36) -- FunctionIDs are
// written into a preallocated StackWalk array and names resolved into a reused NameCache, all under
// the suspend window; the marshaling/name-cache fold happens after the walk. The resolved per-thread
// frames are stashed in _lastCapture (name-only) for Task 3 to encode to the OTLP byte buffer.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class ContinuousProfiler
    {
    public:
        // Called during Profiler Initialize. Like ThreadProfiler::Init, this does no heavy lifting --
        // it only stores the ICorProfilerInfo4 interface and probes for ICorProfilerInfo10 (needed for
        // SuspendRuntime/ResumeRuntime on CoreCLR). It never starts threads or allocates resources.
        //
        // isCoreClr comes from CorProfilerCallbackImpl::SetClrType (GetRuntimeInformation), decided
        // BEFORE this call on every platform -- this is what CaptureAllThreads gates SuspendRuntime on,
        // NOT PAL_STDCPP_COMPAT/OS. ICorProfilerInfo10::SuspendRuntime is a corprof.h COM API available
        // on Windows CoreCLR exactly as on Linux CoreCLR; the previous OS-based gate left Windows
        // CoreCLR never calling it, unlike OTel's ClrRuntimeCapture (which suspends on every OS for
        // CoreCLR). .NET Framework (Windows-only) has no runtime-wide suspend and keeps relying on
        // DoStackSnapshot's own per-thread suspend, matching OTel's NetFxRuntimeCapture.
        void Init(ICorProfilerInfo4* corProfilerInfo, bool isCoreClr) noexcept
        {
            LogInfo(L"Initializing ContinuousProfiler");

            _corProfilerInfo = corProfilerInfo;
            _isCoreClr = isCoreClr;

            HRESULT corProfilerInfoInitResult = corProfilerInfo->QueryInterface(__uuidof(ICorProfilerInfo10), (void**)&_corProfilerInfo10);
            if (SUCCEEDED(corProfilerInfoInitResult)) {
                LogInfo(L"CP: ICorProfilerInfo10 available");
            }

            // The name resolver needs the (now-known) ICorProfilerInfo4, so it is created here rather than
            // at construction. It borrows _nameCache, which is declared ahead of it and therefore outlives
            // it. Created before any thread exists, so no lock is needed around this store; the sampling
            // thread only reads it, and only from Start() onwards.
            try
            {
                _frameNames.reset(new FrameNameResolver(_nameCache, corProfilerInfo));
            }
            catch (const std::exception&)
            {
                LogError(L"CP: failed to create the frame name resolver; sampling will be skipped");
            }
        }

        // Begin (or resume) periodic sampling on the given interval. Lazily creates the worker thread
        // if it is not already running (the thread lives until Shutdown()); records the sampling
        // interval; and clears any prior stop so the worker resumes sampling.
        void Start(uint32_t intervalMs) noexcept
        {
            try
            {
                _intervalMs.store(intervalMs);

                // Publish under _mtx_wake and notify: an idle worker waits on _cv_wake with no timeout,
                // so it only resumes if the flag change is visible to its predicate and it is signalled.
                {
                    std::lock_guard<std::mutex> l(_mtx_wake);
                    _samplingActive.store(true);
                }
                _cv_wake.notify_one();

                if (!_workerThread.joinable())
                {
                    LogTrace(L"CP: starting sampling thread");
                    _workerThread = std::thread(&ContinuousProfiler::SamplingThreadStart, this);
                    std::this_thread::yield();
                }
            }
            catch (const std::exception&)
            {
            }
        }

        // Stop sampling but keep the worker thread alive (idle, waiting for the next Start()). The
        // thread is only torn down by Shutdown().
        void Stop() noexcept
        {
            {
                std::lock_guard<std::mutex> l(_mtx_wake);
                _samplingActive.store(false);
            }
            _cv_wake.notify_one();
        }

        // Terminate the worker thread and free resources. Mirrors ThreadProfiler::Shutdown -- signals
        // shutdown, joins the worker, and resets flags so a subsequent Start() can create a fresh thread.
        void Shutdown() noexcept
        {
            try
            {
                SignalShutdown();

                if (_workerThread.joinable())
                {
                    _workerThread.join();  // joinable is false upon return
                    LogTrace(L"CP: sampling thread shut down");
                }
                else
                {
                    LogTrace(L"CP: ", __func__, L" called while thread is not running");
                }

                _samplingActive.store(false);
                _shuttingDown.store(false);
            }
            catch (const std::exception&)
            {
            }
        }

        // Drain the oldest filled sample buffer into the caller's array. This IS the native side of the
        // managed ISampleSource.ReadBatch contract: claim the oldest filled double-buffer slot, memcpy up
        // to `len` bytes into `buf`, free the slot, and return the number of bytes written (0 if no buffer
        // is ready or args are invalid). The managed BufferParser then decodes those bytes. The extern "C"
        // export that P/Invoke calls wraps this member (Task 5). Never throws.
        int32_t ReadThreadSamples(int32_t len, unsigned char* buf) noexcept
        {
            if (buf == nullptr || len <= 0)
            {
                return 0;
            }

            try
            {
                return _sampleBuffers.Read(len, buf);
            }
            catch (...)
            {
                LogTrace(L"CP: exception draining sample buffer");
            }

            return 0;
        }

        // Record the calling MANAGED thread's active distributed-tracing context so the next sample of
        // that thread can be correlated to its trace/span. Called from arbitrary app threads (Task 8
        // wires the extern "C" export). Keyed by the CLR ThreadID (ICorProfilerInfo::GetCurrentThreadID)
        // -- the SAME id space the sampler already holds for each thread from EnumThreads -- and stored
        // in a lock-free, suspend-safe map (see TraceContextMap.h) so it can be read while the runtime is
        // suspended without deadlock.
        void SetTraceContext(int64_t traceIdHigh, int64_t traceIdLow, int64_t spanId) noexcept
        {
            const ThreadID tid = CurrentManagedThreadId();
            _traceContexts.Set(tid, traceIdHigh, traceIdLow, spanId);

            // Diagnostic (throttled): prove the setter fires with non-zero ids. Only a real
            // (non-zero) trace context is worth logging; the noisy zero-clears are ignored here.
            if ((traceIdHigh != 0 || traceIdLow != 0 || spanId != 0) && ShouldLogPushDiagnostic())
            {
                LogTrace(L"[ContinuousProfiling] pushed trace context tid=", tid, L" traceHi=", traceIdHigh,
                    L" traceLo=", traceIdLow, L" span=", spanId);
            }
        }

        // Clear the calling managed thread's active trace context (its transaction/segment ended).
        // Subsequent samples of the thread carry zeros -> no link -- until the next SetTraceContext.
        void ResetTraceContext() noexcept
        {
            _traceContexts.Reset(CurrentManagedThreadId());
        }

        // Mark the calling managed thread as one level deeper into agent-owned background dispatch
        // (Scheduler wraps its own timer-callback invocation with this -- see AgentWorkMap.h for why
        // thread IDENTITY, not frame text, is needed to catch parked agent threads). Nesting-safe.
        void SetAgentWork() noexcept
        {
            _agentWork.Increment(CurrentManagedThreadId());
        }

        // Mark the calling managed thread one level shallower. Must be paired 1:1 with SetAgentWork.
        void ResetAgentWork() noexcept
        {
            _agentWork.Decrement(CurrentManagedThreadId());
        }

        // The per-thread trace-context map this sampler owns, for OTHER samplers to read from.
        // AllocationSampler MUST use this instance rather than one of its own: there is a single managed
        // trace-context export, and it writes here (via SetTraceContext above), so an allocation sample can
        // only be correlated by reading the same map. Safe to share -- unlike the name cache, TraceContextMap
        // is lock-free and wait-free by design (see its header), so concurrent readers on app threads and
        // the sampling thread are exactly what it is built for. Never null; owned by this object, so it
        // outlives any sampler the profiler callback holds alongside it.
        TraceContextMap* SharedTraceContexts() noexcept
        {
            return &_traceContexts;
        }

        // NOTE: intentionally NOT declared `noexcept = default`. clang/libstdc++ computes the implicit
        // default ctor's exception spec from the members (some -- e.g. the reused NameCache / vector
        // buffers -- allocate and are therefore not noexcept), so `noexcept = default` is a hard compile
        // error there ("exception specification ... does not match the calculated one") even though MSVC
        // accepts it. Leaving the spec implicit keeps the profiler buildable on both toolchains; a
        // construction-time allocation failure is fatal regardless. The destructor stays noexcept.
        ContinuousProfiler() = default;

        // Defensive safety net: if managed code never calls Shutdown() explicitly (or the profiler
        // object is torn down some other way) while the worker thread is still running, a bare
        // std::thread destructor would call std::terminate() and crash the host process. Shutdown()
        // is idempotent (guards on joinable(), resets its flags), so it is always safe to call here
        // even if it already ran explicitly.
        ~ContinuousProfiler() noexcept
        {
            Shutdown();
        }

        ContinuousProfiler(const ContinuousProfiler&) = delete;
        ContinuousProfiler(ContinuousProfiler&&) = delete;
        ContinuousProfiler& operator=(const ContinuousProfiler&) = delete;
        ContinuousProfiler& operator=(ContinuousProfiler&&) = delete;

    private:
        // Reuse the ThreadProfiler's preallocated name-cache machinery verbatim (same suspend-safe
        // constraints apply). The prealloc name buffers, the type/method name holder and the frame struct
        // that holds them now live with the name resolution they exist for -- see FrameNameResolver.h.
        using NameCache = NewRelic::Profiler::ThreadProfiler::NameCache;

        // The preallocated per-frame walk slot. Owned by FrameNameResolver (it is that class's scratch
        // type); the walk buffer below is an array of them.
        using StackFrame = FrameNameResolver::StackFrame;

        // How many stack frames we support per thread. Walking stops (keeping the leaf-most frames) beyond
        // this; see StaticStackFrameCallback. Deliberately NOT ThreadProfiler's 1337 -- that value was
        // inherited by an early copy of this file and never re-derived. This cap is what sizes the two
        // permanently-resident scratch allocations (_stackwalk = cap StackFrames, ~4.4 KB each, and each of
        // _capture's ThreadCountForReservation slots reserving cap FunctionIds + cap xstring_t), so 1337 cost
        // ~12 MB resident for the life of the process, versus roughly 1 MB at 128. Observed managed stacks
        // average under 5 frames deep, and the deepest depth budgeted for this feature is 60 frames.
        static constexpr size_t MaxStackFramesSupported = 128;

        // A guess at how many threads we will see; used to reserve the per-tick capture vector.
        static constexpr size_t ThreadCountForReservation = 100;

        // Preallocated array of frames -- avoids dynamic allocation during the walk
        // (mirror ThreadProfiler.h:305).
        using StackWalk = std::array<StackFrame, MaxStackFramesSupported>;

        // Unmarshaled per-thread walk state; also the context passed to the snapshot callback. Mirrors
        // ThreadProfiler::ThreadProfile (ThreadProfiler.h:308-325). Holds references to the shared,
        // reused stackwalk buffer and name cache -- constructing one does NOT allocate.
        struct ThreadProfile
        {
            ICorProfilerInfo4* _corProfilerInfo;
            NameCache& _nameCache;
            StackWalk& _stackwalk;
            StackWalk::iterator _frameNext{};
            // Set by StaticStackFrameCallback when the stack is deeper than MaxStackFramesSupported and the
            // walk was aborted on purpose. Lets ProfileAllThreads tell a deliberate abort (frames captured,
            // keep them) apart from a genuine DoStackSnapshot failure (no frames, drop the thread), without
            // depending on which HRESULT the CLR maps the abort to.
            bool _truncated{};
            ThreadID _managedTID;
            // Trace-context read-back, plumbed through so StaticStackFrameCallback can stamp it on the
            // FIRST callback invocation -- i.e. while the CLR is still inside DoStackSnapshot for this
            // thread, which is the only suspension guarantee that holds on EVERY platform (global
            // SuspendRuntime is CoreCLR/Linux-only; see CaptureAllThreads). _contextCaptured ensures
            // the read happens exactly once per walk regardless of frame count.
            TraceContextMap& _traceContexts;
            TraceContext& _contextOut;
            bool _contextCaptured{};
            ThreadProfile(ThreadID managedTID, ICorProfilerInfo4* corProfilerInfo, NameCache& nameCache, StackWalk& stackwalk,
                TraceContextMap& traceContexts, TraceContext& contextOut) :
                _corProfilerInfo(corProfilerInfo), _nameCache(nameCache), _stackwalk(stackwalk), _frameNext(std::begin(_stackwalk)),
                _managedTID(managedTID), _traceContexts(traceContexts), _contextOut(contextOut)
            {}
            ~ThreadProfile() = default;
            ThreadProfile(ThreadProfile&&) = default;

            ThreadProfile(const ThreadProfile&) = delete;
            ThreadProfile& operator=(const ThreadProfile&) = delete;
            ThreadProfile& operator=(ThreadProfile&&) = delete;
        };

        // The captured, name-resolved stack for one managed thread from a single tick. This is the
        // in-memory hand-off that Task 3 encodes to the OTLP extended-pprof byte buffer (Task 4 adds
        // trace context). Frames are leaf->root, "Namespace.Type.Method", name-only.
        struct CapturedThread
        {
            ThreadID ManagedThreadId{};
            DWORD OsThreadId{};
            xstring_t ThreadName;   // resolved AFTER resume (may allocate); "" when the OS has no name.
            TraceContext Context{}; // stamped INSIDE StaticStackFrameCallback, while DoStackSnapshot has this thread suspended.
            bool OnCpu{}; // set post-resume from CPU-time delta since last tick; false on the first tick.
            bool IsAgentWork{}; // stamped INSIDE the suspend window from AgentWorkMap -- see its read site.
            // Function IDs captured leaf->root UNDER SUSPEND (cheap copy from the walk buffer, no metadata).
            std::vector<FunctionID> FunctionIds;
            // Fully-qualified frame names, resolved leaf->root AFTER resume from FunctionIds (metadata +
            // signature formatting run post-resume so the suspend window holds only the stack walk).
            std::vector<xstring_t> Frames;
        };

        // Resolve the CLR ThreadID of the CALLING (managed app) thread via the stored ICorProfilerInfo.
        // This is the id the TraceContextMap is keyed by; it lives in the SAME id space as the ThreadIDs
        // the sampler enumerates via EnumThreads, so a set here is looked up by the sampler exactly.
        // Returns 0 (the map's empty-slot sentinel -> a silent no-op) if the CLR call fails.
        ThreadID CurrentManagedThreadId() const noexcept
        {
            ThreadID tid = 0;
            if (_corProfilerInfo == nullptr || FAILED(_corProfilerInfo->GetCurrentThreadID(&tid)))
            {
                return 0;
            }
            return tid;
        }

        // Throttle for the push diagnostic: log only the first N non-zero pushes so a busy app does not
        // spam the log. Cheap relaxed atomic; the exact cutoff under races does not matter.
        static constexpr uint32_t MaxPushDiagnostics = 20;
        bool ShouldLogPushDiagnostic() noexcept
        {
            return _pushDiagnosticCount.fetch_add(1, std::memory_order_relaxed) < MaxPushDiagnostics;
        }

        // Test if shutdown has been requested (and log if it has), returning the state of the flag.
        bool IsShutdownRequested() const noexcept
        {
            const auto shutdownRequested = _shuttingDown.load();
            if (shutdownRequested) {
                LogInfo(L"CP: Shutting down continuous profiler");
            }
            return shutdownRequested;
        }

        // Set _shuttingDown and wake the worker so it can observe the shutdown request.
        void SignalShutdown() noexcept
        {
            // Store under _mtx_wake so the notification cannot be lost against a worker that is between
            // evaluating its wait predicate and blocking -- an idle worker waits with no timeout, so a
            // lost shutdown signal would hang Shutdown()'s join() forever rather than delay it one tick.
            {
                std::lock_guard<std::mutex> l(_mtx_wake);
                _shuttingDown.store(true);
            }
            _cv_wake.notify_one();
        }

        // Worker thread entry point. Initializes the thread for calling the Execution Engine (required
        // before suspending any thread), then loops: while sampling is active, wait up to the sampling
        // interval (or until woken by Stop()/Shutdown()) and capture a sample; while paused, wait
        // indefinitely for Start()/Shutdown() rather than polling. Terminates when _shuttingDown is true.
        void SamplingThreadStart()
        {
            LogTrace(L"CP: sampling thread started");

            // Must be called on any thread before making ICorProfilerInfo* calls and before any thread
            // is suspended by this profiler, to avoid loader/heap-lock deadlocks with a suspended thread.
            HRESULT hr = _corProfilerInfo->InitializeCurrentThread();
            if (FAILED(hr))
            {
                LogError(L"CP: InitializeCurrentThread failed: ", std::hex, std::showbase, hr,
                    std::resetiosflags(std::ios_base::basefield | std::ios_base::showbase));
            }

            for (;;)
            {
                try
                {
                    {
                        std::unique_lock<std::mutex> l(_mtx_wake);
                        if (_samplingActive.load())
                        {
                            // Active: wake on the interval OR an explicit signal (Stop/Shutdown).
                            _cv_wake.wait_for(l, std::chrono::milliseconds(_intervalMs.load()),
                                [&]() noexcept { return _shuttingDown.load() || !_samplingActive.load(); });
                        }
                        else
                        {
                            // Paused: nothing to do until Start() or Shutdown() wakes us. Waiting with
                            // no timeout (instead of re-polling wait_for every interval) is what makes
                            // Stop() cheap -- an always-true predicate re-evaluated every _intervalMs
                            // returns instantly each time, pegging a core for the whole paused duration.
                            _cv_wake.wait(l, [&]() noexcept { return _shuttingDown.load() || _samplingActive.load(); });
                        }
                    }

                    if (IsShutdownRequested())
                    {
                        break;
                    }

                    if (!_samplingActive.load())
                    {
                        continue;
                    }

                    CaptureAllThreads();
                }
                catch (...)
                {
                    LogError(L"CP: Exception thrown while sampling.");
                    // an exception here is recoverable, "The thread must go on!"
                }
            }

            LogTrace(L"CP: sampling thread terminating");
        }

        // Take one all-thread sample. Mirrors ThreadProfiler::ProfilerThreadStart's suspend/walk/resume
        // block (ThreadProfiler.h:649-657): hold the shared suspend mutex for the whole cycle so the CP
        // and the on-demand ThreadProfiler never suspend the runtime at the same time, suspend the
        // runtime on CoreCLR, walk every managed thread, then resume. Never throws (SamplingThreadStart
        // also guards) -- a failure here must never crash or hang the host.
        void CaptureAllThreads()
        {
            // BACK-PRESSURE FIRST: if the managed reader has not drained, this tick's batch has nowhere
            // to go, so skip the whole cycle instead of suspending the runtime, walking every stack and
            // encoding a batch we would only discard at publish time. Suspending the app to produce
            // output we know we must drop is the most expensive possible no-op. Safe as a gate because
            // this thread is the only producer -- see SampleBufferQueue::HasFreeSlot.
            if (!_sampleBuffers.HasFreeSlot())
            {
                LogTrace(L"CP: sample buffers full; skipping tick without suspending (reader has not drained)");
                return;
            }

            // Preallocate the per-thread frame buffer ONCE (MaxStackFramesSupported frames), reused every tick. Done
            // here -- outside the suspend window and before taking the shared mutex -- so no allocation
            // ever happens while the runtime is suspended.
            if (!_stackwalk)
            {
                _stackwalk = std::make_unique<StackWalk>();
            }

            // Preallocate this tick's capture storage ONCE (first call only), also outside the suspend
            // window: ThreadCountForReservation persistent slots, each with FunctionIds/Frames reserved
            // to MaxStackFramesSupported -- the same bound the walk buffer (and therefore
            // StaticStackFrameCallback on overflow) is capped at -- so a per-thread push_back inside the
            // suspend window can never reallocate. Reused every tick from here on; ProfileAllThreads only
            // clears/overwrites slots in place, never grows this vector.
            if (_capture.size() != ThreadCountForReservation)
            {
                _capture.resize(ThreadCountForReservation);
                for (auto& slot : _capture)
                {
                    slot.FunctionIds.reserve(MaxStackFramesSupported);
                    slot.Frames.reserve(MaxStackFramesSupported);
                }
                // Reserve the pre-suspend thread-ID list once, matching the capture slots.
                _threadList.reserve(ThreadCountForReservation);
            }

            uint32_t failedSnapshotCount = 0;
            uint32_t overflowCount = 0;
            uint32_t exceptionCount = 0;
            uint32_t truncatedStackCount = 0;
            bool captureThrew = false;

            // Wall-clock stamp for the batch, and the suspend-window duration reported in BatchStats.
            const auto batchTimestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
                std::chrono::system_clock::now().time_since_epoch()).count();
            int64_t microsSuspended = 0;

            // Enumerate managed threads BEFORE suspending the runtime (and before taking the suspend mutex).
            // EnumThreads + building the ID list allocate/iterate, which must never run inside the suspend
            // window (heap-lock deadlock hazard). A thread that dies between here and its DoStackSnapshot
            // simply fails the snapshot and is counted -- never fatal. Mirrors OTel's pre-suspend enumerate.
            EnumerateThreadsInto(_threadList);

            {
                // Serialize with the ThreadProfiler: only one runtime suspend/stack-walk cycle in flight
                // process-wide. Held across the entire suspend->walk->resume sequence.
                std::lock_guard<NewRelic::Profiler::SuspendMutex> suspendLock(NewRelic::Profiler::SuspendMutex::Shared());

                // Stop-the-world on CoreCLR, on EVERY OS -- mirrors OTel's ClrRuntimeCapture (calls
                // ICorProfilerInfo::SuspendRuntime uniformly on Windows and Linux CoreCLR; only
                // .NET Framework, which has no runtime-wide suspend API, gets per-thread-only handling).
                // Gated on _isCoreClr (set in Init from CorProfilerCallbackImpl::SetClrType's
                // GetRuntimeInformation result), NOT on PAL_STDCPP_COMPAT/OS -- ICorProfilerInfo10 is a
                // corprof.h COM interface available on Windows CoreCLR exactly as on Linux CoreCLR.
                // The previous OS-based gate left Windows CoreCLR never stopping the world at all, out of
                // step with OTel and, per product direction, not "operating correctly" -- DoStackSnapshot's
                // own per-thread suspend is not an accepted substitute for CoreCLR.
                if (_isCoreClr)
                {
                    if (!_corProfilerInfo10)
                    {
                        // Publish nothing and leave the prior buffer state intact -- an early return here
                        // must not clobber a previously filled slot. This runtime will never support
                        // Continuous Profiling, so warn loudly ONCE (every subsequent tick would otherwise
                        // hit this same branch forever and flood the log at the sampling interval).
                        if (!_loggedUnsupportedRuntimeWarning)
                        {
                            LogWarn(L"Continuous Profiling: this runtime does not support ICorProfilerInfo10 "
                                L"(required for SuspendRuntime/ResumeRuntime); Continuous Profiling cannot run "
                                L"and will not collect samples for the lifetime of this process.");
                            _loggedUnsupportedRuntimeWarning = true;
                        }
                        else
                        {
                            LogDebug(L"Continuous Profiling: CaptureAllThreads called without ICorProfilerInfo10; skipping sample.");
                        }
                        return;
                    }
                    const HRESULT suspendHr = _corProfilerInfo10->SuspendRuntime();
                    if (FAILED(suspendHr))
                    {
                        // A busy suspend (e.g. CORPROF_E_SUSPENSION_IN_PROGRESS -- the CLR's own GC is
                        // already suspending) means the runtime never actually stopped. Walking it now
                        // would read a moving target, and ResumeRuntime would resume a suspend we never
                        // own. Bail out exactly like the missing-ICorProfilerInfo10 case above: publish
                        // nothing and leave the prior buffer state intact.
                        LogDebug(L"CP: SuspendRuntime failed: ", std::hex, std::showbase, suspendHr,
                            std::resetiosflags(std::ios_base::basefield | std::ios_base::showbase),
                            L"; skipping this tick's sample.");
                        return;
                    }
                }
                // else: .NET Framework -- no runtime-wide suspend API exists; DoStackSnapshot's own
                // per-target-thread suspend is the only mechanism, on Windows, same as OTel's
                // NetFxRuntimeCapture. Trace-context correlation (StaticStackFrameCallback) does not
                // depend on which branch ran here -- it reads during DoStackSnapshot's own per-thread
                // suspend either way.

                const auto suspendStart = std::chrono::steady_clock::now();
                try
                {
                    ProfileAllThreads(failedSnapshotCount, overflowCount, exceptionCount, truncatedStackCount);
                }
                catch (...)
                {
                    // The show must go on -- a failed sample is never fatal. Flagged rather than logged:
                    // this catch runs inside the suspend window, where taking StdLog's mutex could
                    // deadlock against a frozen app thread. Reported after ResumeRuntime below.
                    captureThrew = true;
                }
                microsSuspended = std::chrono::duration_cast<std::chrono::microseconds>(
                    std::chrono::steady_clock::now() - suspendStart).count();

                if (_isCoreClr)
                {
                    _corProfilerInfo10->ResumeRuntime();
                }
            }

            // AFTER ResumeRuntime, all outside the suspend window:
            // 1. Resolve each captured FunctionID sequence into fully-qualified frame names + signatures
            //    (metadata calls, signature parsing, string building -- the bulk of the old suspend cost).
            ResolveCapturedFrames();
            // 2. Resolve each thread's OS thread name (may allocate / read /proc) and tally how many carry a
            //    trace context. The trace-context READ itself already happened under suspend in
            //    ProfileAllThreads (writers frozen -> stable seqlock read).
            const uint32_t threadsWithContext = EnrichCapturedThreads();

            // Diagnostic: how many captured threads carried a trace context this tick, and how many
            // snapshots failed. Cheap Finest line -- and it is HERE, post-resume, rather than inside
            // ProfileAllThreads, because logging inside the suspend window risks a deadlock.
            LogTrace(L"[ContinuousProfiling] capture: ", _capturedCount, L" thread(s), ", threadsWithContext,
                L" with trace context, ", failedSnapshotCount, L" snapshot failure(s)");

            if (captureThrew)
            {
                LogTrace(L"CP: exception in CaptureAllThreads");
            }

            if (exceptionCount != 0)
            {
                LogTrace(L"CP: ", exceptionCount, L" exception(s) profiling individual threads this tick");
            }

            if (overflowCount != 0)
            {
                // Honest truncation signal, mirroring the sample-buffer truncation log in EncodeAndPublish:
                // more managed threads were successfully walked this tick than the ThreadCountForReservation
                // persistent slots hold, so the extras were dropped rather than growing the capture buffer
                // under suspend.
                LogTrace(L"CP: thread capture overflow; dropped ", overflowCount, L" thread(s) beyond the ",
                    static_cast<size_t>(ThreadCountForReservation), L"-slot capture buffer");
            }

            if (truncatedStackCount != 0)
            {
                // Honest truncation signal for the OTHER truncation axis: these threads' stacks were deeper
                // than the per-thread frame cap, so the walk was aborted and only the leaf-most
                // MaxStackFramesSupported frames were kept. The samples are still published.
                LogTrace(L"CP: stack depth truncated for ", truncatedStackCount, L" thread(s); kept the leaf-most ",
                    static_cast<size_t>(MaxStackFramesSupported), L" frame(s)");
            }

            EncodeAndPublish(failedSnapshotCount, batchTimestamp, microsSuspended);
        }

        // Encode this tick's captured stacks into the byte-opcode format BufferParser decodes and hand
        // the result to a free double-buffer slot. Runs AFTER ResumeRuntime, so allocation is fine here.
        // Applies back-pressure: if both buffers are still full (the managed reader has not drained),
        // the batch is DROPPED and logged rather than blocking the app or growing memory. CaptureAllThreads
        // gates on the same condition before suspending, so this is the residual-race path only.
        void EncodeAndPublish(uint32_t failedSnapshotCount, int64_t batchTimestamp, int64_t microsSuspended)
        {
            try
            {
                SampleBufferWriter writer(_encodeScratch, MaxBufferBytes);
                writer.BeginBatch();
                writer.WriteStartBatch(batchTimestamp);

                int32_t totalFrames = 0;
                for (size_t i = 0; i < _capturedCount; ++i)
                {
                    const auto& thread = _capture[i];

                    // Estimate this sample's size and skip it if it would overflow the fixed buffer,
                    // rather than growing without bound. A truncated batch is still valid to the parser.
                    if (!writer.WillFit(EstimateSampleBytes(thread)))
                    {
                        LogTrace(L"CP: sample buffer full mid-batch; truncating remaining threads");
                        break;
                    }

                    writer.WriteStartSample();
                    writer.WriteThreadName(thread.ThreadName);
                    writer.WriteInt64Field(static_cast<int64_t>(thread.OsThreadId));
                    writer.WriteInt64Field(thread.Context.TraceIdHigh);
                    writer.WriteInt64Field(thread.Context.TraceIdLow);
                    writer.WriteInt64Field(thread.Context.SpanId);
                    writer.WriteBoolField(thread.OnCpu); // v2 per-sample on-CPU flag
                    writer.WriteBoolField(thread.IsAgentWork); // v3 per-sample agent-work flag
                    for (const auto& frame : thread.Frames)
                    {
                        writer.WriteCodedFrameString(frame);
                        ++totalFrames;
                    }
                    writer.WriteFrameListTerminator();
                }

                writer.WriteBatchStats(microsSuspended, static_cast<int32_t>(_capturedCount), totalFrames,
                    static_cast<int32_t>(failedSnapshotCount));
                writer.WriteEndBatch();

                // Hand the encoded bytes to a free slot; drop (back-pressure) if both are full. The
                // CaptureAllThreads gate normally catches saturation before we pay the suspend cost, so
                // reaching this drop means the queue filled during this tick -- still never blocks.
                if (!_sampleBuffers.TryPublish(_encodeScratch))
                {
                    LogTrace(L"CP: sample buffers full; dropping tick (reader has not drained)");
                }
                _encodeScratch.clear();
            }
            catch (...)
            {
                LogTrace(L"CP: exception encoding sample buffer");
            }
        }

        // Upper-bound byte estimate for one sample, used by the overflow guard. Assumes every frame is a
        // freshly-interned string capped at MaxStringChars (the worst case), plus fixed per-sample bytes
        // and the (now populated) thread-name string.
        static size_t EstimateSampleBytes(const CapturedThread& thread) noexcept
        {
            // 1 opcode + name len prefix(2) + 4 int64 fields(32) + onCpu byte(1) + isAgentWork byte(1) +
            // frame terminator(2).
            size_t bytes = 1 + 2 + 32 + 1 + 1 + 2;

            // Thread name: capped at MaxStringChars, 2 bytes per UTF-16 code unit. Now that names are
            // populated this must be counted so WillFit cannot admit a sample that overflows the buffer.
            const size_t nameChars = thread.ThreadName.size() < SampleBufferWriter::MaxStringChars
                ? thread.ThreadName.size() : SampleBufferWriter::MaxStringChars;
            bytes += nameChars * 2;

            for (const auto& frame : thread.Frames)
            {
                const size_t chars = frame.size() < SampleBufferWriter::MaxStringChars ? frame.size() : SampleBufferWriter::MaxStringChars;
                bytes += 2 + 2 + (chars * 2); // code short + len short + UTF-16LE bytes
            }
            return bytes;
        }

        // Post-resume enrichment: for each captured thread, resolve its OS thread name and tally whether
        // it carries a trace context. The trace-context read happens earlier, under suspend, inside
        // StaticStackFrameCallback (see ProfileAllThreads/ThreadProfile); this pass only does the name
        // resolution, which allocates / reads /proc and is therefore NOT suspend-safe -- NEVER call this
        // inside the suspend window. Returns the number of threads that carry a context (diagnostic).
        uint32_t EnrichCapturedThreads()
        {
            uint32_t withContext = 0;

            // This tick's per-thread cumulative CPU micros, used to replace _prevCpuMicros below so dead
            // threads (not seen this tick) are pruned rather than accumulating forever.
            std::unordered_map<DWORD, int64_t> seenCpu;
            seenCpu.reserve(_capturedCount);

            for (size_t i = 0; i < _capturedCount; ++i)
            {
                auto& thread = _capture[i];
                // Context was already stamped under suspend in ProfileAllThreads (writers frozen ->
                // stable read). Here we only tally how many threads carry a link (diagnostic) and resolve
                // OS thread names (post-resume: may allocate / do syscalls).
                if (thread.Context.TraceIdHigh != 0 || thread.Context.TraceIdLow != 0 || thread.Context.SpanId != 0)
                {
                    ++withContext;
                }

                // Resolve the OS thread id from the managed id HERE (post-resume), not under suspend --
                // nothing in the suspend window needs it. GetThreadInfo's HRESULT is intentionally not
                // checked: on failure (e.g. the thread exited between resume and now) OsThreadId stays 0,
                // and the name/CPU lookups below already treat 0 / an unreadable id as "no name, off-CPU".
                DWORD osThreadId = 0;
                _corProfilerInfo->GetThreadInfo(thread.ManagedThreadId, &osThreadId);
                thread.OsThreadId = osThreadId;

                thread.ThreadName = ResolveOsThreadName(thread.OsThreadId);

                // On-CPU classification: a thread is on-CPU this tick if its cumulative CPU time grew
                // since the last tick's baseline. No baseline yet (first tick this thread was seen, or
                // the read failed) -> false rather than a guess.
                const int64_t cur = ReadThreadCpuMicros(thread.OsThreadId);
                const auto prev = _prevCpuMicros.find(thread.OsThreadId);
                thread.OnCpu = (cur >= 0 && prev != _prevCpuMicros.end() && cur > prev->second);
                if (cur >= 0)
                {
                    seenCpu[thread.OsThreadId] = cur;
                }
            }

            _prevCpuMicros.swap(seenCpu); // keep only threads seen (and readable) this tick
            return withContext;
        }

        // Cumulative CPU time (user+kernel) for an OS thread, in microseconds; -1 if unavailable (thread
        // gone, or the read failed). Runs POST-resume only, same as ResolveOsThreadName -- both allocate /
        // do syscalls and are therefore not suspend-safe. Never throws.
        static int64_t ReadThreadCpuMicros(DWORD osThreadId) noexcept
        {
            try
            {
#ifdef PAL_STDCPP_COMPAT
                // Linux: /proc/self/task/<tid>/stat field 14 (utime) and 15 (stime), in clock ticks.
                // The 2nd field (comm) is parenthesized and may itself contain spaces/parens, so find the
                // LAST ')' on the line and count fields from there rather than splitting on whitespace
                // from the start.
                char path[64] = { 0 };
                std::snprintf(path, sizeof(path), "/proc/self/task/%u/stat", static_cast<unsigned>(osThreadId));

                std::FILE* f = std::fopen(path, "r");
                if (f == nullptr)
                {
                    return -1; // thread gone or stat unreadable.
                }

                char line[512] = { 0 };
                const size_t read = std::fread(line, 1, sizeof(line) - 1, f);
                std::fclose(f);
                line[read] = '\0';

                char* lastParen = std::strrchr(line, ')');
                if (lastParen == nullptr)
                {
                    return -1;
                }

                // The first whitespace-delimited token after the last ')' is field 3 (state); utime is
                // field 14, so state -> utime is an 11-field gap. Skip 11 tokens (fields 3..13) to land on
                // field 14 (utime), then read 2 more tokens (utime, then stime, field 15).
                char* cursor = lastParen + 1;
                for (int skip = 0; skip < 11; ++skip)
                {
                    while (*cursor == ' ') ++cursor;
                    if (*cursor == '\0') return -1;
                    while (*cursor != ' ' && *cursor != '\0') ++cursor;
                }

                while (*cursor == ' ') ++cursor;
                if (*cursor == '\0') return -1;
                const uint64_t utime = std::strtoull(cursor, &cursor, 10);

                while (*cursor == ' ') ++cursor;
                if (*cursor == '\0') return -1;
                const uint64_t stime = std::strtoull(cursor, &cursor, 10);

                const long clockTicksPerSec = ::sysconf(_SC_CLK_TCK);
                if (clockTicksPerSec <= 0)
                {
                    return -1;
                }

                return static_cast<int64_t>((utime + stime) * 1000000ULL / static_cast<uint64_t>(clockTicksPerSec));
#else
                // Windows: GetThreadTimes on a query-limited handle; sum kernel+user, 100ns -> microseconds.
                HANDLE hThread = ::OpenThread(THREAD_QUERY_LIMITED_INFORMATION, FALSE, osThreadId);
                if (hThread == nullptr)
                {
                    return -1;
                }

                FILETIME creation{}, exitTime{}, kernel{}, user{};
                int64_t micros = -1;
                if (::GetThreadTimes(hThread, &creation, &exitTime, &kernel, &user))
                {
                    auto toMicros = [](const FILETIME& ft) -> uint64_t
                    {
                        const uint64_t hundredNs = (static_cast<uint64_t>(ft.dwHighDateTime) << 32) | ft.dwLowDateTime;
                        return hundredNs / 10ULL; // 100ns units -> microseconds
                    };
                    micros = static_cast<int64_t>(toMicros(kernel) + toMicros(user));
                }
                ::CloseHandle(hThread);
                return micros;
#endif
            }
            catch (...)
            {
                return -1;
            }
        }

        // POST-RESUME: resolve every captured thread's FunctionID sequence into fully-qualified frame names.
        // All metadata + signature + string work happens here, out of the suspend window (it is done by
        // FrameNameResolver -- see that header for why none of it is suspend-safe). Runs on the sampling
        // thread after ResumeRuntime.
        void ResolveCapturedFrames()
        {
            if (!_frameNames)
            {
                return; // Init() failed to create the resolver; leave the frames empty rather than crash.
            }

            for (size_t i = 0; i < _capturedCount; ++i)
            {
                auto& thread = _capture[i];
                // Frames was already cleared for this slot (under suspend, in ProfileAllThreads) and is
                // reserved to MaxStackFramesSupported, so these emplace_backs cannot reallocate even
                // though this runs post-resume.
                for (const auto functionId : thread.FunctionIds)
                {
                    thread.Frames.emplace_back(_frameNames->ResolveFrameName(functionId));
                }
            }
        }

        // Enumerate all managed threads and DoStackSnapshot each one into a preallocated frame buffer,
        // resolving names into the reused NameCache. Mirrors ThreadProfiler::ProfileAllThreads
        // (ThreadProfiler.h:559-617). ZERO heap allocation and NO locks occur inside the DoStackSnapshot
        // callback (the hard rule at ThreadProfiler.h:23-36) -- all per-thread structures below are
        // preallocated once, reused every tick; the name-cache fold happens only after each walk.
        //
        // Writes into the persistent _capture buffer (preallocated to ThreadCountForReservation slots by
        // the caller, before suspend) rather than a per-tick vector: _capturedCount successfully-walked
        // threads land in _capture[0.._capturedCount), each slot updated in place (no emplace_back, no
        // reserve/resize) so nothing here can allocate. A tick with more successfully-walked threads than
        // slots drops the extras (overflowCount) instead of growing the buffer under suspend.
        void ProfileAllThreads(uint32_t& failedSnapshotCount, uint32_t& overflowCount, uint32_t& exceptionCount,
            uint32_t& truncatedStackCount)
        {
            _capturedCount = 0;
            failedSnapshotCount = 0;
            overflowCount = 0;
            exceptionCount = 0;
            truncatedStackCount = 0;

            // _threadList was populated by EnumerateThreadsInto() BEFORE the suspend window opened -- no
            // enumeration or allocation happens here, under suspend.
            for (const auto threadId : _threadList)
            {
                // Read the flag directly rather than via IsShutdownRequested(): that helper logs, and
                // LogStuff takes StdLog's shared mutex and allocates. A frozen app thread holding that
                // mutex (or the CRT heap lock) would block the sampler here forever, ResumeRuntime would
                // never be reached, and the whole process would hang. Shutdown is already logged by the
                // IsShutdownRequested() call in SamplingThreadStart, outside the suspend window.
                if (_shuttingDown.load())
                {
                    break;
                }

                try
                {
                    // Scratch slot for this thread's trace context, stamped by StaticStackFrameCallback on
                    // its first invocation -- i.e. while DoStackSnapshot itself has this thread suspended.
                    // That is the only suspension guarantee available on EVERY platform: global
                    // SuspendRuntime only compiles under PAL_STDCPP_COMPAT (CoreCLR on Linux -- see
                    // CaptureAllThreads); on Windows DoStackSnapshot's own per-target-thread suspend is the
                    // sole freeze, and it ends the instant DoStackSnapshot returns below. Declared local
                    // (not written to _capture directly) so an overflow/failure drop below simply discards
                    // it along with everything else this walk produced.
                    TraceContext threadContext{};

                    // Reset the preallocated per-thread walk state; no allocation happens here.
                    ThreadProfile threadProfile(threadId, _corProfilerInfo, _nameCache, *_stackwalk, _traceContexts, threadContext);

                    // If context is NULL, the walk begins at the last available managed frame for the
                    // target thread (mirror ThreadProfiler.h:585).
                    const auto result = _corProfilerInfo->DoStackSnapshot(threadId, StaticStackFrameCallback,
                        COR_PRF_SNAPSHOT_INFO::COR_PRF_SNAPSHOT_DEFAULT, &threadProfile, nullptr, 0);

                    // A managed thread with no managed frames (e.g. an idle thread-pool thread), or a
                    // thread that died between Enum and snapshot (CORPROF_E_STACKSNAPSHOT_INVALID_TGT_THREAD),
                    // fails here -- record it and skip, never fatal (mirror ThreadProfiler.h:590-596).
                    //
                    // A stack deeper than MaxStackFramesSupported also reports failure
                    // (CORPROF_E_STACKSNAPSHOT_ABORTED) because our callback deliberately aborted the walk.
                    // Those frames are good -- keep the sample and count the truncation.
                    if (threadProfile._truncated)
                    {
                        ++truncatedStackCount;
                    }
                    else if (FAILED(result))
                    {
                        ++failedSnapshotCount;
                        continue;
                    }

                    // The persistent capture buffer holds exactly ThreadCountForReservation slots,
                    // preallocated once outside the suspend window. A tick that successfully walks more
                    // threads than that must drop the extras here -- growing _capture under suspend would
                    // reallocate, which is exactly what this buffer exists to avoid.
                    if (_capturedCount >= _capture.size())
                    {
                        ++overflowCount;
                        continue;
                    }

                    // Reuse the next free slot IN PLACE -- no emplace_back, no reallocation. Stale data
                    // left in this slot from an earlier use (a prior tick, possibly a different thread) is
                    // fully overwritten/cleared below; nothing from a previous occupant leaks through.
                    auto& captured = _capture[_capturedCount];
                    captured.ManagedThreadId = threadId;
                    // OS thread id is resolved POST-RESUME in EnrichCapturedThreads (via GetThreadInfo on
                    // ManagedThreadId): nothing under suspend consumes it, and OTel likewise keeps this CLR
                    // call out of the suspend window. Zeroed here so a since-freed slot can't leak a prior
                    // occupant's id if the post-resume resolve fails.
                    captured.OsThreadId = 0;

                    // threadContext was stamped by StaticStackFrameCallback DURING DoStackSnapshot, while
                    // this specific thread was suspended -- true on every platform, unlike a read gated on
                    // global SuspendRuntime (CoreCLR/Linux only). If the walk failed before the
                    // callback ever ran (rare: e.g. zero managed frames), threadContext stays the zero
                    // value it was initialized to, same as a genuine TryGet miss. Plain copy here, not a
                    // fresh TryGet -- re-reading now would be the exact post-resume race this replaces.
                    captured.Context = threadContext;

                    // Same suspend-window-safety argument as the trace-context read above: IsAgentWork is a
                    // single wait-free atomic load, allocation-free and no CLR calls, so it is safe here.
                    // This is the thread-IDENTITY signal follow-up #16 needs -- it is true for the whole
                    // duration of a Scheduler-dispatched action regardless of what frames are on the stack,
                    // so it catches agent threads parked in System.Threading.Monitor.Wait that no frame-text
                    // predicate can see.
                    captured.IsAgentWork = _agentWork.IsAgentWork(threadId);

                    // Copy the FunctionID sequence (leaf->root) out of the reused walk buffer -- the ONLY
                    // per-frame work left under suspend. Metadata + signature resolution happens post-resume;
                    // the ids must be copied out now because _stackwalk is overwritten by the next thread.
                    // FunctionIds.clear() retains its capacity (reserved to MaxStackFramesSupported, the
                    // walk buffer's own size, in CaptureAllThreads) -- it drops stale entries from this
                    // slot's last use without freeing/reallocating, and the walk can never produce more
                    // than MaxStackFramesSupported frames, so the push_backs below cannot reallocate either.
                    captured.FunctionIds.clear();
                    for (auto it = std::begin(threadProfile._stackwalk); it != threadProfile._frameNext; ++it)
                    {
                        captured.FunctionIds.push_back(it->functionId);
                    }

                    // Drop stale frame names from this slot's last use too (same no-free clear()); the
                    // post-resume ResolveCapturedFrames pass repopulates them from the FunctionIds above.
                    captured.Frames.clear();

                    ++_capturedCount;
                }
                catch (...)
                {
                    // The show must go on -- a failure on one thread never stops the others
                    // (mirror ThreadProfiler.h:611-615). Counted rather than logged: the caller reports
                    // the tally post-resume, because logging here would take StdLog's mutex and allocate
                    // inside the suspend window.
                    ++exceptionCount;
                }
            }

            // Capture is returned to CaptureAllThreads via _capturedCount/_capture; encoding to the byte
            // buffer happens there AFTER ResumeRuntime so no allocation occurs inside the suspend window.
            // The per-tick diagnostics (captured/failed/exception/overflow counts) are likewise logged by
            // the caller after ResumeRuntime -- nothing in this function may log.
        }

        // Enumerate all active managed threads via ICorProfilerInfo::EnumThreads in batches
        // (mirror ThreadProfiler::GetThreads, ThreadProfiler.h:450-488).
        // Fill `out` with the current managed thread IDs. MUST be called OUTSIDE the suspend window (see the
        // _threadList member comment): EnumThreads and this vector's own storage would risk a heap-lock
        // deadlock if run while an app thread is suspended holding the CRT heap lock. `out` is cleared first
        // but keeps its capacity, so steady-state ticks reuse the buffer without allocating.
        void EnumerateThreadsInto(std::vector<ThreadID>& out) const
        {
            out.clear();
            CComPtr<ICorProfilerThreadEnum> threadEnum;
            if (SUCCEEDED(_corProfilerInfo->EnumThreads(&threadEnum)))
            {
                const int ThreadEnumBatchSize = 40;
                std::array<ThreadID, ThreadEnumBatchSize> threadIDBatch;
                const auto batchBegin = threadIDBatch.data();
                ULONG celtFetched{};
                HRESULT hr{};
                if (SUCCEEDED(threadEnum->GetCount(&celtFetched)))
                {
                    out.reserve(celtFetched);
                }
                celtFetched = 0;
                while (SUCCEEDED(hr = threadEnum->Next(ThreadEnumBatchSize, batchBegin, &celtFetched)))
                {
                    for (ULONG idx = 0; idx != celtFetched; ++idx)
                    {
                        out.push_back(batchBegin[idx]);
                    }

                    if (S_FALSE == hr)
                    {
                        break;
                    }
                }
                if (FAILED(hr))
                {
                    LogError(L"CP: ", __func__, L": thread enum Next() failed");
                }
            }
            else
            {
                LogError(L"CP: ", __func__, L": Could not get thread enumerator");
            }
        }

        // Per-frame snapshot callback. Records ONLY the FunctionID into the preallocated StackWalk array;
        // name/type/signature resolution is deferred to the post-resume ResolveCapturedFrames pass. ZERO
        // heap allocation, NO metadata calls, NO locks here -- the runtime is suspended (CoreCLR) / the
        // target thread is suspended by DoStackSnapshot. Do not log here (logging can allocate/lock -> deadlock).
        //
        // Returning anything other than S_OK makes the CLR abort the walk (ProfilerStackWalkCallback maps a
        // non-S_OK return to SWA_ABORT, and DoStackSnapshot then returns CORPROF_E_STACKSNAPSHOT_ABORTED),
        // which is how the overflow path below stops a too-deep walk. Frames already written to the buffer
        // survive the abort.
        static HRESULT __stdcall StaticStackFrameCallback(uintptr_t functionId, uintptr_t /* instructionPointer */, uintptr_t /* frameInfo */, uint32_t /* contextSize */, uint8_t[] /* context */, void* clientData)
        {
            try
            {
                const HRESULT StackTooDeep = S_FALSE;

                ThreadProfile& threadProfile = *static_cast<ThreadProfile*>(clientData);

                // Stamp the trace context on the FIRST callback invocation for this thread, before any
                // frame/overflow handling below (including the early truncation return) -- this is the
                // one point in the whole capture that is guaranteed suspended on every platform: the CLR
                // is calling back synchronously from inside DoStackSnapshot, which suspends exactly this
                // target thread for the duration of the call, independent of whether global SuspendRuntime
                // ran (CoreCLR/PAL_STDCPP_COMPAT only -- see CaptureAllThreads). TryGet is wait-free,
                // lock-free, allocation-free and makes no CLR calls, so it is safe here. Guarded by
                // _contextCaptured so a deep, multi-frame walk only pays for one read.
                if (!threadProfile._contextCaptured)
                {
                    threadProfile._traceContexts.TryGet(threadProfile._managedTID, threadProfile._contextOut);
                    threadProfile._contextCaptured = true;
                }

                // The CLR walks leaf (last-pushed) frame first, root/Main last, so the frames already in the
                // buffer when we hit the cap are the leaf-most MaxStackFramesSupported of the stack -- exactly
                // the end worth keeping, since that is where the CPU time being sampled actually is. Abort the
                // walk rather than paying the rest of the suspend window for frames we would discard.
                //
                // The previous behavior (inherited from ThreadProfiler.h) wrapped _frameNext back to begin() to
                // keep the root instead. That silently lost frames: ProfileAllThreads reads
                // [begin, _frameNext), so after a wrap only the ((depth - 1) mod cap) + 1 most recently
                // written frames were read back -- as few as ONE frame out of an arbitrarily deep stack --
                // and the sample was still reported as a normal, successful capture with no counter or log.
                if (threadProfile._frameNext == std::end(threadProfile._stackwalk))
                {
                    threadProfile._truncated = true;
                    return StackTooDeep;
                }

                // Record ONLY the FunctionID here. All name/type/signature resolution is deferred to the
                // post-resume ResolveCapturedFrames pass -- keeping metadata calls and allocation out of the
                // callback shrinks the suspend window and avoids taking metadata locks while an app thread is
                // suspended (a deadlock-risk class). The callback is now genuinely zero-alloc, no-metadata.
                threadProfile._frameNext->functionId = functionId;
                ++threadProfile._frameNext;
            }
            catch (...)
            {
                // Do not log here because of deadlock (the suspended-thread issue).
            }
            return S_OK;
        }

        //
        // Shutdown -- set during shutdown; the worker checks this to terminate.
        //
        std::atomic<bool> _shuttingDown{ false };

        // Whether the worker should actively sample (toggled by Start()/Stop()); the worker thread
        // itself lives until Shutdown().
        std::atomic<bool> _samplingActive{ false };

        // Sampling interval in milliseconds, set by Start().
        std::atomic<uint32_t> _intervalMs{ 0 };

        //
        // Wake -- lets Stop()/Shutdown() interrupt the worker's interval sleep promptly.
        //
        mutable std::mutex _mtx_wake;
        std::condition_variable _cv_wake;

        // Worker thread that periodically samples all managed threads.
        std::thread _workerThread;

        // Interface to the CLR execution engine and metadata services. Provided during profiler Initialize.
        CComPtr<ICorProfilerInfo4> _corProfilerInfo;
        CComPtr<ICorProfilerInfo10> _corProfilerInfo10;

        // Set in Init from CorProfilerCallbackImpl::SetClrType (GetRuntimeInformation) -- decides whether
        // CaptureAllThreads calls the runtime-wide SuspendRuntime/ResumeRuntime (CoreCLR, every OS) or
        // relies solely on DoStackSnapshot's own per-thread suspend (.NET Framework, Windows-only).
        bool _isCoreClr = false;

        // Set once the first missing-ICorProfilerInfo10 tick has logged a Warn (see CaptureAllThreads).
        // The condition is permanent for the process (this runtime will never support Continuous
        // Profiling), so every subsequent tick logs at Debug instead of re-warning every sample interval.
        bool _loggedUnsupportedRuntimeWarning = false;

        // Preallocated stack-frame buffer, reused across ticks. Allocated lazily on the first capture
        // (outside the suspend window). NEVER allocated while the runtime is suspended.
        std::unique_ptr<StackWalk> _stackwalk;

        // Persistent per-tick capture buffer. Resized ONCE, to ThreadCountForReservation slots, on the
        // first CaptureAllThreads call -- outside the suspend window -- with each slot's FunctionIds and
        // Frames vectors reserved to MaxStackFramesSupported. Every tick thereafter, ProfileAllThreads
        // reuses slots [0, _capturedCount) in place (clear() + push_back, never resize/emplace_back), so
        // no allocation is possible while the runtime is suspended. Slots at or beyond _capturedCount
        // hold stale data from an earlier tick and must not be read until claimed and overwritten again.
        std::vector<CapturedThread> _capture;

        // Number of valid, freshly-written entries in _capture for the current/most recent tick. Set by
        // ProfileAllThreads (under suspend); read by ResolveCapturedFrames, EnrichCapturedThreads, and
        // EncodeAndPublish (all post-resume) to bound their iteration over _capture.
        size_t _capturedCount{ 0 };

        // Managed-thread ID list for the current tick. Filled by EnumerateThreadsInto() BEFORE the runtime is
        // suspended -- EnumThreads plus building this ID vector must NOT happen inside the suspend window: an
        // app thread suspended while holding the CRT heap lock would deadlock any allocation here (the same
        // hazard the _capture/_stackwalk preallocation avoids). OTel enumerates pre-suspend for this reason.
        // Reserved ONCE to ThreadCountForReservation (outside the window) and reused every tick; clear()
        // retains capacity so steady-state ticks do not allocate. A tick that sees more managed threads than
        // the reserve grows it -- but only ever while the runtime is running, never suspended.
        std::vector<ThreadID> _threadList;

        // Type/method name cache, reused across ticks. Populated post-resume in ResolveCapturedFrames
        // (never touched inside the snapshot callback, which now only records FunctionIDs). Declared
        // BEFORE _frameNames, which borrows it: reverse-order destruction then guarantees the resolver
        // dies first.
        NameCache _nameCache;

        // Post-resume frame-name resolution (metadata + signature formatting into _nameCache). Created in
        // Init() once ICorProfilerInfo4 is known. Touched only by the sampling thread, only after resume --
        // it is not thread safe, which is also why AllocationSampler owns its own instance rather than
        // sharing this one (see FrameNameResolver.h).
        std::unique_ptr<FrameNameResolver> _frameNames;

        // Per-thread active trace context, written by app threads via Set/ResetTraceContext and read by
        // the sampler (by CLR ManagedThreadId) while the runtime is suspended. Lock-free + wait-free
        // reads so it is safe to touch inside the suspend window without deadlock (see TraceContextMap.h).
        TraceContextMap _traceContexts;

        // Per-thread agent-owned-dispatch depth counter, written by Scheduler's timer callbacks via
        // Set/ResetAgentWork and read by the sampler (by CLR ThreadID) while the runtime is suspended.
        // Lock-free + wait-free reads, same suspend-window-safety requirement as _traceContexts (see
        // AgentWorkMap.h).
        AgentWorkMap _agentWork;

        // Throttle counter for the SetTraceContext push diagnostic (see ShouldLogPushDiagnostic).
        std::atomic<uint32_t> _pushDiagnosticCount{ 0 };

        // Previous-tick per-OS-thread cumulative CPU micros, keyed by OS thread id. Read/written only on
        // the sampler thread, only post-resume (allocation is fine here). Rebuilt each tick in
        // EnrichCapturedThreads to prune dead threads.
        std::unordered_map<DWORD, int64_t> _prevCpuMicros;

        // Scratch buffer the encoder writes into each tick before the bytes are swapped into a filled
        // double-buffer slot. Reused across ticks; only touched by the sampling thread (after resume),
        // so it needs no lock of its own.
        std::vector<uint8_t> _encodeScratch;

        // Hard ceiling on a single encoded batch (fixed max buffer size). A batch that would exceed this
        // is truncated + stat-counted rather than growing without bound.
        static constexpr size_t MaxBufferBytes = 4 * 1024 * 1024;

        // Two-slot FIFO double-buffer (mirror OTel cpu_buffer_a/b): after resume the producer publishes
        // this tick's batch into a free slot; the managed reader drains the OLDEST filled slot. When both
        // slots are filled the producer applies back-pressure by SKIPPING the tick before it suspends
        // anything (never blocks the app). Owns its own lock -- see SampleBufferQueue.h.
        SampleBufferQueue _sampleBuffers;
    };
}}}
