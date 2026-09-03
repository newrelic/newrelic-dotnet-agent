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
#include "namecache.h"
#include "ThreadDescriptionResolver.h"
#include "../SignatureParser/SignatureParser.h"
#include "../SignatureParser/SignatureFormatting.h"
#include "../Profiler/CorTokenResolver.h"
#include "SampleBufferQueue.h"
#include "SampleBufferWriter.h"
#include "SuspendMutex.h"
#include "TraceContextMap.h"
#include "AgentWorkMap.h"

// Always-on counterpart to the collector-driven ThreadProfiler: a long-lived worker thread samples
// every managed thread on a fixed interval, instead of a single on-demand profile. Lifecycle
// (Init/Start/Stop/Shutdown) and capture shape mirror ThreadProfiler (see ThreadProfiler.h).
//
// Suspend rule (referenced throughout this file): while the runtime is suspended, code may take ZERO
// heap allocations, ZERO locks, and must not log -- any of those can deadlock against a frozen app
// thread holding the CRT heap lock, a metadata lock, or StdLog's mutex. Only wait-free/lock-free reads
// and writes into preallocated buffers are safe there; everything else (metadata resolution, name
// formatting, logging, /proc reads) is deferred to run POST-RESUME.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class ContinuousProfiler
    {
    public:
        // Called during Profiler Initialize. Like ThreadProfiler::Init, this does no heavy lifting --
        // it only stores the ICorProfilerInfo4 interface and probes for ICorProfilerInfo10 (needed for
        // SuspendRuntime/ResumeRuntime on CoreCLR). It never starts threads or allocates resources.
        //
        // isCoreClr (from CorProfilerCallbackImpl::SetClrType) is what CaptureAllThreads gates
        // SuspendRuntime on -- NOT PAL_STDCPP_COMPAT/OS. ICorProfilerInfo10::SuspendRuntime is a
        // corprof.h COM API available on Windows CoreCLR exactly as on Linux CoreCLR; .NET Framework
        // has no runtime-wide suspend and relies solely on DoStackSnapshot's own per-thread suspend.
        void Init(ICorProfilerInfo4* corProfilerInfo, bool isCoreClr) noexcept
        {
            LogInfo(L"Initializing ContinuousProfiler");

            if (corProfilerInfo == nullptr)
            {
                LogError(L"CP: Init called with a null ICorProfilerInfo4; Continuous Profiling cannot run.");
                return;
            }

            _corProfilerInfo = corProfilerInfo;
            _isCoreClr = isCoreClr;

            HRESULT corProfilerInfoInitResult = corProfilerInfo->QueryInterface(__uuidof(ICorProfilerInfo10), (void**)&_corProfilerInfo10);
            if (SUCCEEDED(corProfilerInfoInitResult)) {
                LogInfo(L"CP: ICorProfilerInfo10 available");
            }
            else {
                LogInfo(L"CP: ICorProfilerInfo10 not available: ", std::hex, std::showbase, corProfilerInfoInitResult,
                    std::resetiosflags(std::ios_base::basefield | std::ios_base::showbase));
            }
        }

        // Begin (or resume) periodic sampling on the given interval. Lazily creates the worker thread
        // if it is not already running (the thread lives until Shutdown()); records the sampling
        // interval; and clears any prior stop so the worker resumes sampling.
        void Start(uint32_t intervalMs) noexcept
        {
            // Same guard as ThreadProfiler::RequestProfile. The exported entry point only checks that the
            // profiler singleton exists (it is set in the ctor), which leaves a window before Initialize()
            // reaches Init() where a managed Start would arm sampling and spawn a worker that can never
            // sample -- silently collecting nothing for the life of the session. Refuse loudly instead.
            if (_corProfilerInfo == nullptr)
            {
                LogError(L"CP: ", __func__, L" called without proper initialization. (corProfilerInfo)");
                return;
            }

            try
            {
                // Serialize the whole lifecycle with Stop()/Shutdown() (see _mtx_lifecycle). Without it,
                // two concurrent Start()s could both observe _workerThread.joinable()==false and each
                // assign a std::thread over what becomes a joinable thread -> std::terminate() (host
                // crash); and a Start() racing Shutdown()'s join()/flag-reset could spawn a worker that
                // immediately parks on a stale _shuttingDown while _workerThread stays joinable, leaving
                // CP permanently dead with no future Start() able to respawn.
                std::lock_guard<std::mutex> lifecycle(_mtx_lifecycle);

                _intervalMs.store(std::max<uint32_t>(intervalMs, MinIntervalMs));

                // A fresh start gets a fresh agent-work map. SetAgentWork/ResetAgentWork drive a per-thread
                // nesting-DEPTH counter that is never tombstoned, so an increment orphaned by a managed-side
                // lifecycle race would otherwise keep that thread tagged as agent work -- and its samples
                // filtered out of the profile -- for the rest of the process. Clearing here, BEFORE sampling
                // is armed, bounds any such leak to the session that caused it. See AgentWorkMap::Clear for
                // why this is safe against a concurrent suspended-runtime reader.
                _agentWork.Clear();

                // A fresh start also retires every trace-context link stored by the previous session. A
                // managed-side reset orphaned by a stop/start race leaves a live thread's slot holding the
                // old session's (traceId, spanId), which the sampler would otherwise ship on new profile
                // data until that thread's next SetTraceContext. Bumping the generation invalidates them
                // all at once; see TraceContextMap::NewGeneration for why no slot is touched.
                _traceContexts.NewGeneration();

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
        // thread is only torn down by Shutdown(). Also reclaims the session's sampling buffers so a
        // stopped session's peak memory shrinks back between sessions instead of staying resident
        // until Shutdown (see ReleaseSamplingResources) -- important for stop/start retunes, where a
        // server-side config change stops and restarts sampling and the process should not keep
        // holding a prior session's ~1 MB stack-walk buffer, capture slots, and multi-MB batch buffers.
        void Stop() noexcept
        {
            // Serialize with Start()/Shutdown() on the lifecycle mutex (see _mtx_lifecycle) so the
            // sampling-active toggle can't interleave with a concurrent thread create/join.
            std::lock_guard<std::mutex> lifecycle(_mtx_lifecycle);
            {
                std::lock_guard<std::mutex> l(_mtx_wake);
                _samplingActive.store(false);
            }
            _cv_wake.notify_one();

            // Free the session's sampling buffers now, not at Shutdown. Correctness against the worker
            // thread (which Stop does NOT join -- it only parks it) rests on two facts:
            //   1. _samplingActive is already false (stored above under _mtx_wake), so the worker will
            //      park at the top of its loop rather than starting a new capture; and
            //   2. acquiring SuspendMutex here waits for any capture the worker is mid-way through to
            //      finish -- CaptureAllThreads holds this same mutex across its ENTIRE body, including
            //      the post-resume EncodeAndPublish, so once we hold it the worker is guaranteed to be
            //      out of CaptureAllThreads and cannot touch any buffer ReleaseSamplingResources frees.
            // _mtx_lifecycle (held above) blocks a concurrent Start() from re-arming sampling while we
            // free. Lock order is always _mtx_lifecycle -> SuspendMutex and nothing takes them the other
            // way (CaptureAllThreads takes SuspendMutex but never _mtx_lifecycle), so this can't invert.
            try
            {
                std::lock_guard<NewRelic::Profiler::SuspendMutex> suspendLock(NewRelic::Profiler::SuspendMutex::Shared());
                ReleaseSamplingResources();
            }
            catch (const std::exception&)
            {
            }
        }

        // Terminate the worker thread and free resources. Mirrors ThreadProfiler::Shutdown -- signals
        // shutdown, joins the worker, and resets flags so a subsequent Start() can create a fresh thread.
        void Shutdown() noexcept
        {
            try
            {
                // Serialize with Start()/Stop() (see _mtx_lifecycle). The signal, join, AND flag resets
                // must all happen under this lock: a Start() slipping between join() and the flag resets
                // is exactly failure mode (b) the lifecycle mutex exists to prevent.
                std::lock_guard<std::mutex> lifecycle(_mtx_lifecycle);

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

                // The worker is joined (or was never running), so no capture can be in flight -- free
                // the session buffers here too, matching Stop(), so a Start()->Shutdown() sequence with
                // no explicit Stop() doesn't leave them resident. No SuspendMutex needed here (unlike
                // Stop): the worker thread is gone, so nothing else can touch these buffers.
                ReleaseSamplingResources();
            }
            catch (const std::exception&)
            {
            }
        }

        // Drain the oldest filled sample buffer into the caller's array. This IS the native side of the
        // managed ISampleSource.ReadBatch contract: claim the oldest filled double-buffer slot, memcpy up
        // to `len` bytes into `buf`, free the slot, and return the number of bytes written (0 if no buffer
        // is ready or args are invalid). The managed BufferParser then decodes those bytes. Never throws.
        int32_t ReadThreadSamples(int32_t len, unsigned char* buf) noexcept
        {
            if (buf == nullptr || len <= 0)
            {
                return 0;
            }

            try
            {
                const int32_t bytesRead = _sampleBuffers.Read(len, buf);

                // A batch bigger than the caller's buffer is truncated by Read, which loses whole samples
                // off the tail. Latent today (the managed DrainBufferSize equals MaxBufferBytes), so a
                // report here is the only thing that would surface a divergence between the two sizes
                // instead of silently shipping partial batches. Logged once per truncating drain, not per
                // drain, so a matched pair of sizes costs nothing.
                const uint64_t truncatedBytes = _sampleBuffers.TruncatedByteCount();
                if (truncatedBytes != _reportedTruncatedBytes)
                {
                    LogWarn(L"CP: sample batch truncated to fit the reader's buffer: kept ", bytesRead,
                        L" byte(s), dropped ", truncatedBytes - _reportedTruncatedBytes, L"; ",
                        _sampleBuffers.TruncatedBatchCount(), L" batch(es) truncated so far");
                    _reportedTruncatedBytes = truncatedBytes;
                }

                return bytesRead;
            }
            catch (...)
            {
                LogTrace(L"CP: exception draining sample buffer");
            }

            return 0;
        }

        // Record the calling MANAGED thread's active distributed-tracing context so the next sample of
        // that thread can be correlated to its trace/span. Called from arbitrary app threads. Keyed by
        // the CLR ThreadID (ICorProfilerInfo::GetCurrentThreadID)
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
        // (Scheduler wraps its own timer-callback invocation with this -- see AgentWorkMap.h for why a
        // thread-IDENTITY signal, not frame text, is needed to catch parked agent threads). Nesting-safe.
        void SetAgentWork() noexcept
        {
            _agentWork.Increment(CurrentManagedThreadId());
        }

        // Mark the calling managed thread one level shallower. Must be paired 1:1 with SetAgentWork.
        void ResetAgentWork() noexcept
        {
            _agentWork.Decrement(CurrentManagedThreadId());
        }

        // CLR ThreadIDs are recycled (a ThreadID is really a Thread* value), so a thread that dies
        // without ever calling ResetTraceContext/ResetAgentWork itself (killed mid-transaction, or
        // mid- Scheduler callback) would otherwise leave its slot in _traceContexts / _agentWork live
        // forever. A later, unrelated thread allocated at the same address would then inherit the
        // dead thread's trace-context link (H4a) or its agent-work depth (H4b) -- and with
        // _agentWork never freeing slots at all, unbounded thread churn over a long-running process
        // exhausts its fixed table. Called from CorProfilerCallbackImpl::ThreadDestroyed, on
        // whichever thread the CLR invokes that callback on -- never the dying thread itself, so
        // there is no race with the dying thread's own last Set/Increment.
        void ThreadDestroyed(ThreadID threadId) noexcept
        {
            _traceContexts.Reset(threadId);
            _agentWork.Forget(threadId);
        }

        // Test seam: report whether the sampler worker thread currently exists (is joinable), read
        // under the lifecycle mutex so it observes a consistent state relative to Start()/Shutdown().
        // Exists to let the native lifecycle unit test assert that a post-Shutdown Start() can respawn
        // the worker (the flag-reset-under-lock property) without piercing encapsulation elsewhere.
        bool IsWorkerThreadRunning() noexcept
        {
            std::lock_guard<std::mutex> lifecycle(_mtx_lifecycle);
            return _workerThread.joinable();
        }

        // Test seam: run exactly one capture pass synchronously on the CALLING thread, without a worker
        // thread. Under the lifecycle stub (no ICorProfilerInfo10, isCoreClr == false) this allocates
        // the per-session buffers (_stackwalk, _capture, _resolved, _threadList) and publishes one
        // (empty) batch into the sample-buffer queue, all without ever suspending a runtime -- letting a
        // lifecycle test deterministically drive the allocate path so it can then assert Stop() frees it.
        void CaptureOnceForTesting()
        {
            CaptureAllThreads();
        }

        // Test seam: whether any of the per-session sampling buffers ReleaseSamplingResources frees are
        // currently allocated. Lets a lifecycle test assert Stop()/Shutdown() reclaim them and that a
        // subsequent capture re-allocates cleanly. Read on the test thread only, with no worker running,
        // so the plain (unsynchronized) reads of the worker-owned buffers below are race-free there.
        bool HasSamplingResourcesForTesting() const noexcept
        {
            return _stackwalk != nullptr
                || !_capture.empty()
                || !_resolved.empty()
                || _threadList.capacity() != 0
                || _encodeScratch.capacity() != 0
                || !_prevCpuSamples.empty()
                || _sampleBuffers.TotalCapacityForTesting() != 0;
        }

        // Round-robin capture window for a single tick -- see PlanCaptureWindow.
        struct CaptureWindow
        {
            size_t start;       // first _threadList index to visit this tick
            size_t visitCount;  // number of threads to visit this tick == min(threadCount, capacity)
            size_t nextOffset;  // rotation offset to carry into the next tick
        };

        // Plan which threads a single capture tick visits, given the live thread count, the fixed
        // capture-slot capacity, and the rotation offset carried across ticks. Pure + allocation-free
        // (scalar math only) so it is safe to call inside the suspend window; public so the native unit
        // test can exercise the rotation arithmetic directly without a live CLR (it drives no state).
        //
        // When threadCount > capacity the naive "walk _threadList from index 0, drop the rest" approach
        // drops the SAME tail of threads on every tick, because EnumThreads returns CLR ThreadStore order,
        // which is stable across ticks -- so a fixed subset (typically the newest threads) becomes a
        // permanent sampling blind spot. Advancing the start offset by visitCount each tick turns that
        // permanent drop into fair round-robin coverage: over ceil(threadCount / capacity) ticks every
        // thread is visited. When threadCount <= capacity the whole set is visited and the offset advances
        // by a full cycle (a no-op mod threadCount next tick), so an under-capacity process sees no churn.
        // Visiting exactly visitCount (<= capacity) threads also means DoStackSnapshot is never paid for a
        // thread that would only be dropped -- the old code walked every enumerated thread and discarded
        // the overflow after paying the walk cost under suspend.
        static CaptureWindow PlanCaptureWindow(size_t threadCount, size_t capacity, size_t rotationOffset) noexcept
        {
            if (threadCount == 0)
            {
                return CaptureWindow{ 0, 0, rotationOffset };
            }
            const size_t start = rotationOffset % threadCount;
            const size_t visitCount = capacity < threadCount ? capacity : threadCount;
            return CaptureWindow{ start, visitCount, rotationOffset + visitCount };
        }

        // One tick's CPU reading for an OS thread, plus the stamp identifying WHICH thread that reading
        // belongs to. OS thread ids are recycled, so the CPU total alone is not enough to compare two
        // ticks -- see IsOnCpu.
        struct ThreadCpuSample
        {
            int64_t CpuMicros{ -1 }; // cumulative user+kernel CPU; -1 when unavailable
            uint64_t StartStamp{ 0 }; // thread creation stamp (Windows creation FILETIME / Linux starttime); 0 when unavailable
        };

        // Decide whether a thread was on-CPU during the tick that produced `current`, given the previous
        // tick's reading for the SAME OS thread id. Pure; public so the native unit test can exercise the
        // comparison (including the tid-reuse case) without a live CLR.
        //
        // The reuse hazard: `_prevCpuSamples` is keyed by OS thread id, and the OS reissues a tid soon
        // after the thread that held it exits. Without the stamp check, the dead thread's cumulative CPU
        // total becomes the new thread's baseline -- so the new thread is misclassified for its first
        // sample, either reported busy (its own total already exceeds the dead thread's) or reported idle
        // for as long as it takes to catch up. A differing stamp means "different thread, no baseline".
        static bool IsOnCpu(const ThreadCpuSample& previous, const ThreadCpuSample& current) noexcept
        {
            if (previous.CpuMicros < 0 || current.CpuMicros < 0)
            {
                return false;
            }
            if (previous.StartStamp != current.StartStamp)
            {
                return false;
            }
            return current.CpuMicros > previous.CpuMicros;
        }

        // How long the worker should wait before its NEXT capture, given the configured sampling interval
        // and the measured wall-clock cost of the capture that just finished. Pure; public so the native
        // unit test can exercise the arithmetic without a live worker thread or CLR.
        //
        // Waiting the full interval after every capture makes the real tick-to-tick period
        // interval + capture cost. At a 1s interval and a ~150ms capture that is ~13% fewer samples per
        // unit time than the managed side assumes: OtlpProfileBuilder attributes exactly `period`
        // nanoseconds of time to every sample, with period = the configured interval, so an uncompensated
        // cadence makes each profile's totals under-report the time the samples actually represent.
        // Subtracting the previous capture's cost keeps the period at the configured interval instead.
        //
        // The floor deliberately leaves half the interval idle no matter how expensive the capture was. A
        // capture that overruns its own interval cannot be compensated for at all, and driving the wait to
        // zero to chase the cadence would stop the world back-to-back and starve the application being
        // profiled -- in that regime the right thing to give up is the cadence, not the app.
        static uint32_t NextWaitMs(uint32_t intervalMs, int64_t lastCaptureMs) noexcept
        {
            const int64_t compensated = static_cast<int64_t>(intervalMs) - (lastCaptureMs > 0 ? lastCaptureMs : 0);
            const int64_t floorMs = static_cast<int64_t>(intervalMs) / 2;
            return static_cast<uint32_t>(compensated > floorMs ? compensated : floorMs);
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
        // CP's own bounded name cache -- deliberately NOT shared with
        // NewRelic::Profiler::ThreadProfiler::NameCache; see namecache.h.

        // How many stack frames we support per thread. Walking stops (keeping the leaf-most frames)
        // beyond this; see StaticStackFrameCallback. Deliberately NOT ThreadProfiler's 1337: this cap
        // sizes two permanently-resident scratch allocations (_stackwalk, and each of _capture's
        // ThreadCountForReservation slots), so 1337 costs ~12 MB resident for the process lifetime versus
        // ~1 MB at 128. Observed managed stacks average under 5 frames deep; the deepest depth budgeted
        // for this feature is 60 frames.
        static constexpr size_t MaxStackFramesSupported = 128;

        // A guess at how many threads we will see; used to reserve the per-tick capture vector.
        static constexpr size_t ThreadCountForReservation = 100;

        // Defense-in-depth floor for Start()'s interval -- managed already clamps to [1000, 60000]
        // before calling here, but Start(0) would otherwise put the worker into a tight suspend loop
        // (effectively an app-level DoS) if that clamp were ever bypassed or a caller changed.
        static constexpr uint32_t MinIntervalMs = 100;

        // Upper bound on a captured method-signature blob. Signatures larger than this fall back to a
        // name-only frame (no parameter list) rather than allocating in the snapshot callback.
        static constexpr size_t MaxSigBlobBytes = 256;

        // Defensive bound on the nested-type enclosing-chain walk in QualifyNestedTypeName. Real nesting is
        // shallow (a handful of levels at most); this only stops a pathological or corrupt-metadata loop.
        static constexpr size_t MaxTypeNestingDepth = 16;

        // One preallocated stack frame. All name storage is preallocated so the snapshot callback never
        // allocates. Mirrors ThreadProfiler::StackFrame (ThreadProfiler.h:290-302).
        struct StackFrame
        {
            FunctionID functionId{};
            // Defining module of functionId. Half of the type-name cache key: an mdTypeDef token is only
            // unique within its own module (see namecache.h).
            ModuleID moduleId{};
            mdTypeDef typeDef{};
            PreallocTypeName typeName{};
            PreallocMethodName methodName{};

            // Raw COR method-signature blob captured under suspend (zero-alloc memcpy); parsed + formatted
            // into the method name during the post-walk fold. sigBlobLength == 0 means "no signature".
            std::array<uint8_t, MaxSigBlobBytes> sigBlob{};
            uint32_t sigBlobLength{};

            StackFrame() = default;
            StackFrame(const StackFrame&) = delete;
            StackFrame(StackFrame&&) = delete;
            StackFrame& operator=(const StackFrame&) = delete;
            StackFrame& operator=(StackFrame&&) = delete;
        };

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

        // Raw per-thread capture for a single tick -- the ONLY fields ProfileAllThreads may touch while
        // the runtime is suspended. Deliberately POD/allocation-free (see CaptureAllThreads's suspend
        // rule): no strings, no CLR metadata, nothing whose destructor can call operator delete. Mirrors
        // OTel's dotnet native profiler split (their under-suspend buffer is vector<FunctionIdentifier>,
        // itself just mdToken/ModuleID/bool/UINT_PTR) -- resolved, allocation-bearing output lives only in
        // ResolvedThread below, which nothing under suspend ever reaches.
        struct CapturedThread
        {
            ThreadID ManagedThreadId{};
            // OS thread id resolved via GetThreadInfo INSIDE the suspend window (see ProfileAllThreads).
            // A ThreadID is a Thread* that is valid only until ThreadDestroyed fires, so it MUST be mapped
            // to the stable OS id while the runtime is suspended (a suspended thread can't be destroyed) --
            // resolving it post-resume risks dereferencing a freed Thread* if the thread exited in the gap.
            DWORD OsThreadId{};
            TraceContext Context{}; // stamped INSIDE StaticStackFrameCallback, while DoStackSnapshot has this thread suspended.
            // Stamped from AgentWorkMap in ProfileAllThreads. On CoreCLR that read happens while the
            // runtime is globally suspended (SuspendRuntime/ResumeRuntime); .NET Framework has no global
            // suspend, so by this point DoStackSnapshot's own per-thread suspend has already ended --
            // the read there is effectively post-resume. See ProfileAllThreads for the read site.
            bool IsAgentWork{};
            // Function IDs captured leaf->root UNDER SUSPEND (cheap copy from the walk buffer, no metadata).
            std::vector<FunctionID> FunctionIds;
        };

        // Post-resume, name-resolved output for one managed thread from a single tick -- the in-memory
        // hand-off encoded to the OTLP extended-pprof byte buffer. Frames are leaf->root,
        // "Namespace.Type.Method", name-only. Every field here is written ONLY after ResumeRuntime
        // (ResolveCapturedFrames, EnrichCapturedThreads) -- allocation, CLR metadata calls, and syscalls
        // are all fine here precisely because CapturedThread above never shares a field with this struct.
        struct ResolvedThread
        {
            DWORD OsThreadId{};
            xstring_t ThreadName;   // resolved AFTER resume (may allocate); "" when the OS has no name.
            bool OnCpu{}; // set post-resume from CPU-time delta since last tick; false on the first tick.
            // Fully-qualified frame names, resolved leaf->root AFTER resume from CapturedThread::FunctionIds
            // (metadata + signature formatting run post-resume so the suspend window holds only the stack walk).
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

        bool IsShutdownRequested() const noexcept
        {
            const auto shutdownRequested = _shuttingDown.load();
            if (shutdownRequested) {
                LogInfo(L"CP: Shutting down continuous profiler");
            }
            return shutdownRequested;
        }

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
            // Guarded on a non-null interface: Start() before Init() (misuse, or a unit test exercising
            // lifecycle without a live CLR) leaves _corProfilerInfo null, and CaptureAllThreads below
            // no-ops the same way rather than dereferencing it.
            if (_corProfilerInfo != nullptr)
            {
                HRESULT hr = _corProfilerInfo->InitializeCurrentThread();
                if (FAILED(hr))
                {
                    LogError(L"CP: InitializeCurrentThread failed: ", std::hex, std::showbase, hr,
                        std::resetiosflags(std::ios_base::basefield | std::ios_base::showbase));
                }
            }

            // Wall-clock cost of the capture that just finished, subtracted from the next wait so the
            // tick-to-tick period is the configured interval rather than interval + capture cost. Worker-
            // thread local: nothing else reads it. See NextWaitMs.
            int64_t lastCaptureMs = 0;

            for (;;)
            {
                try
                {
                    {
                        std::unique_lock<std::mutex> l(_mtx_wake);
                        if (_samplingActive.load())
                        {
                            // Active: wake on the remainder of the interval OR an explicit signal (Stop/Shutdown).
                            _cv_wake.wait_for(l, std::chrono::milliseconds(NextWaitMs(_intervalMs.load(), lastCaptureMs)),
                                [&]() noexcept { return _shuttingDown.load() || !_samplingActive.load(); });
                        }
                        else
                        {
                            // Paused: nothing to do until Start() or Shutdown() wakes us. Waiting with
                            // no timeout (instead of re-polling wait_for every interval) is what makes
                            // Stop() cheap -- an always-true predicate re-evaluated every _intervalMs
                            // returns instantly each time, pegging a core for the whole paused duration.
                            _cv_wake.wait(l, [&]() noexcept { return _shuttingDown.load() || _samplingActive.load(); });

                            // The next session's first tick has no preceding capture of its own to
                            // compensate for; a cost carried across the pause would shorten its wait.
                            lastCaptureMs = 0;
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

                    // Measure the whole capture pass -- suspend window, stack walks, and the post-resume
                    // name resolution alike -- since every part of it delays the next tick.
                    const auto captureStart = std::chrono::steady_clock::now();
                    CaptureAllThreads();
                    lastCaptureMs = std::chrono::duration_cast<std::chrono::milliseconds>(
                        std::chrono::steady_clock::now() - captureStart).count();
                }
                catch (...)
                {
                    // Cost of a capture that threw part-way through says nothing about a full pass; wait
                    // the whole interval rather than compensating for a truncated one.
                    lastCaptureMs = 0;
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
            // Nothing to sample without the CLR interface (e.g. Start() before Init(), or a lifecycle
            // unit test with no live CLR). Bail before touching anything that would dereference it.
            if (_corProfilerInfo == nullptr)
            {
                return;
            }

            // Serialize the ENTIRE capture cycle with the ThreadProfiler: only one runtime suspend/
            // stack-walk may be in flight process-wide, and -- widened here -- CP's own pre-suspend setup
            // and post-resume resolution are held under the same lock. Those phases (HasFreeSlot's mutex,
            // the StackWalk allocation, EnumerateThreadsInto's EnumThreads which takes the CLR ThreadStore
            // lock, and ResolveCapturedFrames/AppendSignature's metadata locks + allocations) are exactly
            // what the "suspend window" rule forbids overlapping with the other profiler's stop-the-world.
            //
            // Scope of the hazard this closes: Medium, Linux-only. TP's own SuspendRuntime is inside
            // #ifdef PAL_STDCPP_COMPAT (CoreCLR-on-Linux), so only there can TP freeze the world while
            // holding this mutex; on Windows CoreCLR TP never stops the world. CP's sampler is a raw
            // std::thread (not a managed thread), so a TP suspend can't freeze it -- worst case was a
            // stall for TP's window, not a permanent hang -- but it did widen the pre-existing
            // TP-allocates-under-suspend hazard, so CP's setup/post-resume now sit inside the lock too.
            std::lock_guard<NewRelic::Profiler::SuspendMutex> suspendLock(NewRelic::Profiler::SuspendMutex::Shared());

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
            // window: ThreadCountForReservation persistent slots, each with FunctionIds reserved to
            // MaxStackFramesSupported -- the same bound the walk buffer (and therefore
            // StaticStackFrameCallback on overflow) is capped at -- so a per-thread push_back inside the
            // suspend window can never reallocate. Reused every tick from here on; ProfileAllThreads only
            // clears/overwrites slots in place, never grows this vector. _resolved is sized/indexed in
            // lockstep with _capture but is only ever touched post-resume.
            if (_capture.size() != ThreadCountForReservation)
            {
                _capture.resize(ThreadCountForReservation);
                for (auto& slot : _capture)
                {
                    slot.FunctionIds.reserve(MaxStackFramesSupported);
                }
                _resolved.resize(ThreadCountForReservation);
                for (auto& slot : _resolved)
                {
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

            // Enumerate managed threads BEFORE suspending the runtime. EnumThreads + building the ID list
            // allocate/iterate, which must never run inside the actual stop-the-world window (heap-lock
            // deadlock hazard) -- though the SuspendMutex is already held at function scope above, that
            // only serializes against the OTHER profiler; it does not suspend THIS runtime. A thread that
            // dies between here and its DoStackSnapshot simply fails the snapshot and is counted -- never
            // fatal. Mirrors OTel's pre-suspend enumerate.
            EnumerateThreadsInto(_threadList);

            {
                // Inner scope = the actual stop-the-world window (SuspendRuntime -> walk -> ResumeRuntime).
                // The SuspendMutex is already held at function scope, so no lock is taken here; this scope
                // now only delimits the region where the runtime is genuinely frozen and the strict
                // "no alloc / no lock / no log" suspend rule applies.

                // Stop-the-world on CoreCLR, on every OS -- gated on _isCoreClr, not PAL_STDCPP_COMPAT/OS
                // (see Init). .NET Framework has no runtime-wide suspend and skips this branch entirely.
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
                // else: .NET Framework, no runtime-wide suspend. Trace-context correlation
                // (StaticStackFrameCallback) doesn't depend on which branch ran -- it reads during
                // DoStackSnapshot's own per-thread suspend either way.

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
                    // Safe to log here (unlike the try/catch above): the runtime is resumed the instant
                    // this call returns, so we're past the deadlock-hazard part of the suspend window even
                    // though suspendLock hasn't been released yet.
                    const HRESULT resumeHr = _corProfilerInfo10->ResumeRuntime();
                    if (FAILED(resumeHr))
                    {
                        LogWarn(L"CP: ResumeRuntime failed: ", std::hex, std::showbase, resumeHr,
                            std::resetiosflags(std::ios_base::basefield | std::ios_base::showbase));
                    }
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

            // Diagnostic: trace-context and snapshot-failure counts for this tick. Logged post-resume
            // (see the suspend rule), rather than inside ProfileAllThreads.
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
                // Honest truncation signal: more managed threads were live this tick than the
                // ThreadCountForReservation persistent slots hold, so the extras were deferred to a later
                // tick rather than growing the capture buffer under suspend. Unlike a static from-index-0
                // drop, the rotation offset advances each tick, so these threads ARE sampled on subsequent
                // ticks -- this is round-robin coverage, not a permanent blind spot.
                LogTrace(L"CP: thread capture overflow; deferred ", overflowCount, L" thread(s) beyond the ",
                    static_cast<size_t>(ThreadCountForReservation), L"-slot capture buffer to a later tick (round-robin)");
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
        // the result to a free double-buffer slot (post-resume, so allocation is fine here). Back-pressure:
        // if both buffers are still full, the batch is DROPPED and logged instead of blocking or growing
        // memory -- CaptureAllThreads gates on the same condition before suspending, so this is the
        // residual-race path only.
        void EncodeAndPublish(uint32_t failedSnapshotCount, int64_t batchTimestamp, int64_t microsSuspended)
        {
            try
            {
                SampleBufferWriter writer(_encodeScratch, MaxBufferBytes);
                writer.BeginBatch();
                writer.WriteStartBatch(batchTimestamp);

                int32_t totalFrames = 0;
                int32_t emittedCount = 0;
                for (size_t i = 0; i < _capturedCount; ++i)
                {
                    const auto& raw = _capture[i];
                    const auto& resolved = _resolved[i];

                    // Estimate this sample's size and skip it if it would overflow the fixed buffer,
                    // rather than growing without bound. A truncated batch is still valid to the parser.
                    // Reserve TrailerBytes so the WriteBatchStats/WriteEndBatch call below always has
                    // room, even when this sample is the last one that fits.
                    if (!writer.WillFit(EstimateSampleBytes(resolved) + TrailerBytes))
                    {
                        LogTrace(L"CP: sample buffer full mid-batch; truncating remaining threads");
                        break;
                    }

                    writer.WriteStartSample();
                    writer.WriteThreadName(resolved.ThreadName);
                    writer.WriteInt64Field(static_cast<int64_t>(resolved.OsThreadId));
                    writer.WriteInt64Field(raw.Context.TraceIdHigh);
                    writer.WriteInt64Field(raw.Context.TraceIdLow);
                    writer.WriteInt64Field(raw.Context.SpanId);
                    writer.WriteBoolField(resolved.OnCpu); // v2 per-sample on-CPU flag
                    writer.WriteBoolField(raw.IsAgentWork); // v3 per-sample agent-work flag
                    for (const auto& frame : resolved.Frames)
                    {
                        writer.WriteCodedFrameString(frame);
                        ++totalFrames;
                    }
                    writer.WriteFrameListTerminator();
                    ++emittedCount;
                }

                // threadCount reports samples actually emitted, not _capturedCount, so a mid-batch
                // truncation above doesn't overcount.
                writer.WriteBatchStats(microsSuspended, emittedCount, totalFrames,
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
        static size_t EstimateSampleBytes(const ResolvedThread& thread) noexcept
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

        // Post-resume: resolve each captured thread's OS name and tally trace-context carriers (the
        // context read itself already happened under suspend, in StaticStackFrameCallback). Name
        // resolution allocates / reads /proc, so this must never run inside the suspend window. Returns
        // the number of threads carrying a context (diagnostic).
        uint32_t EnrichCapturedThreads()
        {
            uint32_t withContext = 0;

            // This tick's per-thread CPU readings, used to replace _prevCpuSamples below so dead threads
            // (not seen this tick) are pruned rather than accumulating forever.
            std::unordered_map<DWORD, ThreadCpuSample> seenCpu;
            seenCpu.reserve(_capturedCount);

            for (size_t i = 0; i < _capturedCount; ++i)
            {
                const auto& raw = _capture[i];
                auto& resolved = _resolved[i];
                // Context was already stamped under suspend in ProfileAllThreads (writers frozen ->
                // stable read). Here we only tally how many threads carry a link (diagnostic) and resolve
                // OS thread names (post-resume: may allocate / do syscalls).
                if (raw.Context.TraceIdHigh != 0 || raw.Context.TraceIdLow != 0 || raw.Context.SpanId != 0)
                {
                    ++withContext;
                }

                // OS thread id was resolved UNDER SUSPEND in ProfileAllThreads (a ThreadID is a Thread*
                // that becomes invalid once ThreadDestroyed fires, so it must be mapped while the runtime
                // is suspended -- calling GetThreadInfo here, post-resume, could dereference a freed
                // Thread* for a thread that exited in the gap). On resolve failure OsThreadId stayed 0, and
                // the name/CPU lookups below already treat 0 / an unreadable id as "no name, off-CPU".
                resolved.OsThreadId = raw.OsThreadId;

                resolved.ThreadName = ResolveThreadName(resolved.OsThreadId);

                // On-CPU classification: a thread is on-CPU this tick if its cumulative CPU time grew
                // since the last tick's baseline. No baseline yet (first tick this thread was seen, the
                // read failed, or the tid has since been reused by a different thread) -> false rather
                // than a guess. See IsOnCpu.
                const ThreadCpuSample cur = ReadThreadCpuSample(resolved.OsThreadId);
                const auto prev = _prevCpuSamples.find(resolved.OsThreadId);
                resolved.OnCpu = prev != _prevCpuSamples.end() && IsOnCpu(prev->second, cur);
                if (cur.CpuMicros >= 0)
                {
                    seenCpu[resolved.OsThreadId] = cur;
                }
            }

            _prevCpuSamples.swap(seenCpu); // keep only threads seen (and readable) this tick
            return withContext;
        }

        // Resolve an OS thread's name (empty string when it has none). Windows: GetThreadDescription on a
        // handle opened for the OS thread id. Linux: read /proc/self/task/<tid>/comm (comm caps names at
        // ~15 chars). Both paths run AFTER ResumeRuntime (they allocate / do syscalls) and never throw.
        static xstring_t ResolveThreadName(DWORD osThreadId) noexcept
        {
            try
            {
#ifdef PAL_STDCPP_COMPAT
                // Linux: pthread_getname_np needs a pthread_t we do not have for an arbitrary sampled OS
                // thread id, so read the kernel-exposed comm file keyed directly by tid.
                char path[64] = { 0 };
                std::snprintf(path, sizeof(path), "/proc/self/task/%u/comm", static_cast<unsigned>(osThreadId));

                std::FILE* f = std::fopen(path, "r");
                if (f == nullptr)
                {
                    return xstring_t(); // thread gone or comm unreadable -> "".
                }

                char name[64] = { 0 };
                const size_t read = std::fread(name, 1, sizeof(name) - 1, f);
                std::fclose(f);

                size_t len = read;
                while (len > 0 && (name[len - 1] == '\n' || name[len - 1] == '\r'))
                {
                    --len;
                }
                name[len] = '\0';

                return ToWideString(name);
#else
                // Windows: GetThreadDescription (Win 10+), resolved lazily -- see ThreadDescriptionResolver.h
                // for why this must never be a static import.
                return GetThreadDescriptionResolver::ResolveThreadName(osThreadId, GetThreadDescriptionResolver::Resolve());
#endif
            }
            catch (...)
            {
                return xstring_t();
            }
        }

        // Cumulative CPU time (user+kernel) for an OS thread, in microseconds, paired with that thread's
        // creation stamp; CpuMicros is -1 if unavailable (thread gone, or the read failed). Runs
        // POST-resume only, same as ResolveThreadName -- both allocate / do syscalls and are therefore not
        // suspend-safe. Never throws.
        static ThreadCpuSample ReadThreadCpuSample(DWORD osThreadId) noexcept
        {
            ThreadCpuSample sample;
            try
            {
#ifdef PAL_STDCPP_COMPAT
                // Linux: /proc/self/task/<tid>/stat field 14 (utime), 15 (stime) -- both in clock ticks --
                // and field 22 (starttime, the thread's creation time in ticks since boot). The 2nd field
                // (comm) is parenthesized and may itself contain spaces/parens, so find the LAST ')' on the
                // line and count fields from there rather than splitting on whitespace from the start.
                char path[64] = { 0 };
                std::snprintf(path, sizeof(path), "/proc/self/task/%u/stat", static_cast<unsigned>(osThreadId));

                std::FILE* f = std::fopen(path, "r");
                if (f == nullptr)
                {
                    return sample; // thread gone or stat unreadable.
                }

                char line[512] = { 0 };
                const size_t read = std::fread(line, 1, sizeof(line) - 1, f);
                std::fclose(f);
                line[read] = '\0';

                char* lastParen = std::strrchr(line, ')');
                if (lastParen == nullptr)
                {
                    return sample;
                }

                // The first whitespace-delimited token after the last ')' is field 3 (state); utime is
                // field 14, so state -> utime is an 11-field gap. Skip 11 tokens (fields 3..13) to land on
                // field 14 (utime), then read 2 more tokens (utime, then stime, field 15).
                char* cursor = lastParen + 1;
                for (int skip = 0; skip < 11; ++skip)
                {
                    while (*cursor == ' ') ++cursor;
                    if (*cursor == '\0') return sample;
                    while (*cursor != ' ' && *cursor != '\0') ++cursor;
                }

                while (*cursor == ' ') ++cursor;
                if (*cursor == '\0') return sample;
                const uint64_t utime = std::strtoull(cursor, &cursor, 10);

                while (*cursor == ' ') ++cursor;
                if (*cursor == '\0') return sample;
                const uint64_t stime = std::strtoull(cursor, &cursor, 10);

                const long clockTicksPerSec = ::sysconf(_SC_CLK_TCK);
                if (clockTicksPerSec <= 0)
                {
                    return sample;
                }

                sample.CpuMicros = static_cast<int64_t>((utime + stime) * 1000000ULL / static_cast<uint64_t>(clockTicksPerSec));

                // starttime is field 22, so skip the six fields between stime (15) and it (16..21). A
                // failure to reach it leaves StartStamp at 0, which only costs the tid-reuse check.
                for (int skip = 0; skip < 6; ++skip)
                {
                    while (*cursor == ' ') ++cursor;
                    if (*cursor == '\0') return sample;
                    while (*cursor != ' ' && *cursor != '\0') ++cursor;
                }

                while (*cursor == ' ') ++cursor;
                if (*cursor == '\0') return sample;
                sample.StartStamp = std::strtoull(cursor, &cursor, 10);

                return sample;
#else
                // Windows: GetThreadTimes on a query-limited handle; sum kernel+user, 100ns -> microseconds.
                // Its creation FILETIME doubles as the thread's identity stamp for the tid-reuse check.
                HANDLE hThread = ::OpenThread(THREAD_QUERY_LIMITED_INFORMATION, FALSE, osThreadId);
                if (hThread == nullptr)
                {
                    return sample;
                }

                FILETIME creation{}, exitTime{}, kernel{}, user{};
                if (::GetThreadTimes(hThread, &creation, &exitTime, &kernel, &user))
                {
                    auto toTicks = [](const FILETIME& ft) -> uint64_t
                    {
                        return (static_cast<uint64_t>(ft.dwHighDateTime) << 32) | ft.dwLowDateTime;
                    };
                    sample.CpuMicros = static_cast<int64_t>((toTicks(kernel) + toTicks(user)) / 10ULL); // 100ns units -> microseconds
                    sample.StartStamp = toTicks(creation);
                }
                ::CloseHandle(hThread);
                return sample;
#endif
            }
            catch (...)
            {
                return ThreadCpuSample();
            }
        }

        // POST-RESUME: resolve one FunctionID's type + method name (+ signature) into the name cache, if not
        // already cached. Mirrors what the snapshot callback used to do under suspend, moved here so all
        // metadata calls + allocation happen after ResumeRuntime. functionId==0 and unresolvable functions
        // are left uncached (AssembleFrameName then emits "Native.Function Call" / "UnknownMethod(<id>)").
        // Never throws.
        void ResolveIntoCache(FunctionID functionId) noexcept
        {
            if (functionId == 0 || _nameCache.has_fid(functionId))
                return;

            try
            {
                CComPtr<IMetaDataImport2> metaData;
                mdToken methodToken{};
                if (FAILED(_corProfilerInfo->GetTokenAndMetaDataFromFunction(functionId, IID_IMetaDataImport2, (IUnknown**)&metaData, &methodToken)) || metaData == nullptr)
                    return;

                auto& scratch = _resolveScratch;
                scratch.functionId = functionId;
                scratch.moduleId = 0;
                scratch.typeDef = 0;
                scratch.sigBlobLength = 0;

                auto& methodName = scratch.methodName;
                PCCOR_SIGNATURE pSigBlob = nullptr;
                ULONG sigBlobLength = 0;
                if (FAILED(metaData->GetMethodProps(methodToken, &scratch.typeDef,
                    &methodName.first.front(), (ULONG)methodName.first.size(), &methodName.second,
                    nullptr, &pSigBlob, &sigBlobLength, nullptr, nullptr)))
                    return;

                if (scratch.typeDef == 0)
                    return; // no owning type -> leave uncached (AssembleFrameName emits UnknownMethod(<id>))

                // The defining module completes the type-name cache key -- an mdTypeDef token alone is only
                // unique within its own module. One extra call per cache-missing function, not per frame.
                ClassID classId = 0;
                mdToken functionToken = 0;
                if (FAILED(_corProfilerInfo->GetFunctionInfo(functionId, &classId, &scratch.moduleId, &functionToken)) || scratch.moduleId == 0)
                    return;

                if (pSigBlob != nullptr && sigBlobLength > 0 && sigBlobLength <= MaxSigBlobBytes)
                {
                    std::memcpy(scratch.sigBlob.data(), pSigBlob, sigBlobLength);
                    scratch.sigBlobLength = sigBlobLength;
                }

                auto& typeName = scratch.typeName;
                const auto cachedTypeName = _nameCache.typename_for(scratch.moduleId, scratch.typeDef);
                if (cachedTypeName == TypeAndMethodNames::GetUnknownTypeName())
                {
                    DWORD typeFlags = 0;
                    // Bail on failure rather than caching: typeName still holds the PREVIOUS resolve's name
                    // (only functionId/moduleId/typeDef/sigBlobLength are reset per call), which would
                    // otherwise be cached under this type's key. Uncached -> UnknownMethod(<id>).
                    if (FAILED(metaData->GetTypeDefProps(scratch.typeDef, &typeName.first.front(), static_cast<ULONG>(typeName.first.size()), &typeName.second, &typeFlags, nullptr)))
                        return;

                    // GetTypeDefProps returns only the innermost name for a NESTED type (e.g. the compiler
                    // closure "<>c"), dropping the declaring type -- unusable on its own since every type's
                    // closures share that name. Walk the enclosing chain and rebuild "Outer+...+Inner" so the
                    // frame is attributable. Cached per typeDef (below), so this runs once per type.
                    if (IsTdNested(typeFlags))
                    {
                        QualifyNestedTypeName(metaData, scratch.typeDef, typeFlags, typeName);
                    }
                }
                else
                {
                    // Keep .second (the length INCLUDING the null terminator -- NameCache's convention) in
                    // step with .first on this path too, and clamp to the buffer. NameCache::insert only
                    // reads .second when the type key is absent, which this cache hit rules out today, but
                    // a length left over from the previous resolve would otherwise pair this type's name
                    // with another type's length the moment that stops being true.
                    const size_t maxChars = typeName.first.size() - 1;
                    const size_t copied = std::min<size_t>(cachedTypeName->size(), maxChars);
                    std::copy_n(cachedTypeName->c_str(), copied, typeName.first.data());
                    typeName.first[copied] = 0;
                    typeName.second = static_cast<ULONG>(copied + 1);
                }

                AppendSignature(scratch); // fold the parameter list into the method name
                _nameCache.insert(scratch.moduleId, scratch.functionId, scratch.typeDef, scratch.typeName, scratch.methodName);
            }
            catch (...)
            {
            }
        }

        // POST-RESUME: rewrite a nested type's prealloc name from the bare innermost name GetTypeDefProps
        // returns (e.g. the compiler closure "<>c") to the fully-qualified "Outer+...+Inner", walking the
        // enclosing chain via GetNestedClassProps and prepending each encloser with '+' (the CLR nested-type
        // separator, matching Function.h). Uses IsTdNested -- ALL nested visibilities -- so tdNestedPrivate/
        // tdNestedAssembly compiler closures are qualified too (a tdNestedPublic|tdNestedFamily mask misses
        // them). Bounded and never throws; on any failure the bare innermost name is left as-is.
        void QualifyNestedTypeName(IMetaDataImport2* metaData, mdTypeDef typeDef, DWORD typeFlags, PreallocTypeName& out) noexcept
        {
            try
            {
                xstring_t qualified(out.first.data()); // innermost name GetTypeDefProps just wrote
                mdTypeDef current = typeDef;
                DWORD flags = typeFlags;

                for (size_t depth = 0; IsTdNested(flags) && depth < MaxTypeNestingDepth; ++depth)
                {
                    mdTypeDef enclosing = 0;
                    if (FAILED(metaData->GetNestedClassProps(current, &enclosing)) || enclosing == 0)
                        break;

                    ULONG nameLen = 0;
                    metaData->GetTypeDefProps(enclosing, nullptr, 0, &nameLen, nullptr, nullptr);
                    if (nameLen == 0)
                        break;

                    std::vector<xchar_t> buffer(nameLen);
                    DWORD enclosingFlags = 0;
                    if (FAILED(metaData->GetTypeDefProps(enclosing, buffer.data(), nameLen, &nameLen, &enclosingFlags, nullptr)))
                        break;

                    qualified = xstring_t(buffer.data()) + _X("+") + qualified;
                    current = enclosing;
                    flags = enclosingFlags;
                }

                // Copy back into the prealloc buffer, truncating to capacity. PreallocTypeName.second is the
                // length INCLUDING the null terminator (NameCache::insert stores .second - 1 chars).
                const size_t maxChars = out.first.size() - 1;
                const size_t n = qualified.size() < maxChars ? qualified.size() : maxChars;
                std::copy_n(qualified.c_str(), n, out.first.data());
                out.first[n] = 0;
                out.second = static_cast<ULONG>(n + 1);
            }
            catch (...)
            {
            }
        }

        // POST-RESUME: assemble one frame's fully-qualified name from the (now-populated) cache, mirroring
        // the thread profiler's three-case handling: functionId==0 -> "Native.Function Call"; resolved ->
        // "Type.Method(params)"; real-but-unresolvable -> "UnknownClass.UnknownMethod(<id>)".
        xstring_t AssembleFrameName(FunctionID functionId)
        {
            if (functionId == 0)
            {
                // NOTE: the managed PprofProfileBuilder.NativeFrameName constant MUST match this exact
                // string -- it keys profile.frame.type = "native" off it. Change both together.
                return _X("Native.Function Call");
            }
            if (!_nameCache.has_fid(functionId))
            {
                xstring_t frameName(_X("UnknownClass.UnknownMethod("));
                frameName.append(to_xstring((unsigned long)functionId));
                frameName.append(_X(")"));
                return frameName;
            }
            const auto& names = _nameCache[functionId];
            xstring_t frameName(names.TypeName());
            frameName.append(_X("."));
            frameName.append(names.MethodName());
            return frameName;
        }

        // POST-RESUME: resolve every captured thread's FunctionID sequence into fully-qualified frame
        // names (metadata + signature + string work, out of the suspend window).
        void ResolveCapturedFrames()
        {
            for (size_t i = 0; i < _capturedCount; ++i)
            {
                const auto& raw = _capture[i];
                auto& resolved = _resolved[i];
                // Clear here, post-resume: Frames holds last tick's resolved xstring_t names, so freeing
                // them must never happen under suspend (see ResolvedThread's header comment / review C4).
                // Reserved to MaxStackFramesSupported, so the emplace_backs below cannot reallocate.
                resolved.Frames.clear();
                for (const auto functionId : raw.FunctionIds)
                {
                    ResolveIntoCache(functionId);
                    resolved.Frames.emplace_back(AssembleFrameName(functionId));
                }
            }
        }

        // Format the frame's captured method signature and append its parameter list to the method name,
        // turning "Type.Method" into "Type.Method(System.Object, System.Int32)" -- OTel-shaped, so a
        // customer migrating OTel->NR sees identical frames and overloads are distinguishable. Runs during
        // post-resume resolution (ResolveIntoCache), once per newly-resolved functionId. Any failure (parse
        // error, unresolvable token, would-overflow the name buffer) leaves the name-only method name --
        // never throws, never crashes the sampler.
        void AppendSignature(StackFrame& frame) noexcept
        {
            if (frame.sigBlobLength == 0)
                return;

            try
            {
                // Re-fetch the defining module's metadata reader so signature type tokens resolve in the
                // correct scope. Cheap: this runs only for frames being inserted fresh into the cache.
                CComPtr<IMetaDataImport2> metaData;
                mdToken methodToken{};
                if (FAILED(_corProfilerInfo->GetTokenAndMetaDataFromFunction(frame.functionId, IID_IMetaDataImport2, (IUnknown**)&metaData, &methodToken)) || metaData == nullptr)
                    return;

                ByteVector bytes(frame.sigBlob.begin(), frame.sigBlob.begin() + frame.sigBlobLength);
                auto iterator = bytes.cbegin();
                auto methodSignature = SignatureParser::SignatureParser::ParseMethodSignature(iterator, bytes.cend());
                auto resolver = std::make_shared<CorTokenResolver>(metaData);
                const auto params = SignatureParser::FormatParameterList(methodSignature, resolver); // "(...)"

                // methodName.second is the current length INCLUDING the null terminator (NameCache convention).
                auto& buffer = frame.methodName.first;
                const size_t nameLength = frame.methodName.second == 0 ? 0 : frame.methodName.second - 1;
                if (nameLength + params.size() + 1 <= buffer.size())
                {
                    std::copy(params.begin(), params.end(), buffer.begin() + nameLength);
                    buffer[nameLength + params.size()] = _X('\0');
                    frame.methodName.second = static_cast<ULONG>(nameLength + params.size() + 1);
                }
            }
            catch (...)
            {
            }
        }

        // Contain a structured (SEH) fault raised INSIDE DoStackSnapshot while the runtime is suspended.
        //
        // DoStackSnapshot walks a target thread's native+managed stack; a corrupt frame, a torn stack, or
        // a race the CLR's own guards miss can raise an access violation (or another structured exception)
        // from inside the walk. On Windows the profiler is built with /EHsc, under which catch(...) does
        // NOT catch structured exceptions -- so such a fault would blow straight past the C++ catch in
        // ProfileAllThreads, terminate the process, AND (worse than a plain crash) leave the runtime
        // permanently suspended, because ResumeRuntime in CaptureAllThreads is never reached: every other
        // thread stays frozen while the process tears down. Wrapping just this one call in __try/__except
        // turns a fault into an ordinary failed-snapshot HRESULT, so the thread is skipped, the capture
        // loop continues, and ResumeRuntime still runs.
        //
        // Isolated in its own function with no C++ objects that require unwinding, because MSVC forbids
        // __try/__except in a frame that also needs C++ exception unwinding (C2712) -- ProfileAllThreads'
        // loop constructs ThreadProfile/TraceContext locals and so cannot host the __try itself. Logging
        // is deliberately absent (this runs inside the suspend window, where taking StdLog's mutex or
        // allocating could deadlock against a frozen app thread); a contained fault is surfaced by the
        // failedSnapshotCount tally that ProfileAllThreads reports post-resume.
        //
        // Linux/CoreCLR has no SEH -- an access violation there is a process-level signal, outside the
        // scope of this containment -- so the call is issued directly. This does not regain the Linux
        // libstdc++ concern (a C++ throw allocating via __cxa_allocate_exception under suspend): the whole
        // suspend window is allocation-free and throw-free by construction (preallocated capture buffers,
        // wait-free noexcept map reads, an HRESULT-returning DoStackSnapshot), so no C++ exception arises
        // here to allocate in the first place.
        HRESULT DoStackSnapshotContained(ThreadID threadId, ThreadProfile& threadProfile) noexcept
        {
#ifdef PAL_STDCPP_COMPAT
            return _corProfilerInfo->DoStackSnapshot(threadId, StaticStackFrameCallback,
                COR_PRF_SNAPSHOT_INFO::COR_PRF_SNAPSHOT_DEFAULT, &threadProfile, nullptr, 0);
#else
            __try
            {
                return _corProfilerInfo->DoStackSnapshot(threadId, StaticStackFrameCallback,
                    COR_PRF_SNAPSHOT_INFO::COR_PRF_SNAPSHOT_DEFAULT, &threadProfile, nullptr, 0);
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                // Treat a fault during the walk as a failed snapshot: ProfileAllThreads drops the thread
                // like any other DoStackSnapshot failure and counts it, and the runtime is still resumed.
                return E_FAIL;
            }
#endif
        }

        // Enumerate all managed threads and DoStackSnapshot each one into a preallocated frame buffer,
        // resolving names into the reused NameCache (mirrors ThreadProfiler::ProfileAllThreads). Runs
        // under the suspend rule -- see the class header.
        //
        // Writes into the persistent _capture buffer (preallocated to ThreadCountForReservation slots,
        // before suspend): _capturedCount successfully-walked threads land in _capture[0.._capturedCount),
        // each slot updated in place so nothing here can allocate. A tick with more live threads than slots
        // walks only a rotating window of them (see PlanCaptureWindow); the rest (overflowCount) are
        // deferred to later ticks rather than growing the buffer under suspend or dropping a fixed subset.
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
            //
            // Round-robin over ticks: rather than always walking _threadList from index 0 and dropping the
            // stable tail once the slots fill (a permanent blind spot -- see PlanCaptureWindow), start at a
            // rotating offset and visit exactly window.visitCount (<= slot capacity) threads. Threads not
            // visited this tick are covered by later ticks as the window advances. This also avoids paying
            // DoStackSnapshot's cost under suspend for threads that would only be dropped.
            const size_t threadCount = _threadList.size();
            const CaptureWindow window = PlanCaptureWindow(threadCount, _capture.size(), _rotationOffset);
            _rotationOffset = window.nextOffset;
            // Threads deferred to a later tick (not permanently dropped -- rotation covers them). 0 when
            // every enumerated thread fit this tick.
            overflowCount = static_cast<uint32_t>(threadCount - window.visitCount);

            for (size_t visited = 0; visited < window.visitCount; ++visited)
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

                const auto threadId = _threadList[(window.start + visited) % threadCount];

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

                    ThreadProfile threadProfile(threadId, _corProfilerInfo, _nameCache, *_stackwalk, _traceContexts, threadContext);

                    // If context is NULL, the walk begins at the last available managed frame for the
                    // target thread (mirror ThreadProfiler.h:585). Routed through DoStackSnapshotContained
                    // so a structured (SEH) fault inside the walk on Windows/EHsc is contained and the
                    // runtime is still resumed, rather than crashing with the runtime left suspended.
                    const auto result = DoStackSnapshotContained(threadId, threadProfile);

                    // A managed thread with no managed frames, or one that died between Enum and snapshot,
                    // fails here -- record it and skip, never fatal. A stack deeper than
                    // MaxStackFramesSupported also reports failure (CORPROF_E_STACKSNAPSHOT_ABORTED)
                    // because our callback deliberately aborted the walk; those frames are still good, so
                    // count the truncation instead of dropping the sample.
                    if (threadProfile._truncated)
                    {
                        ++truncatedStackCount;
                    }
                    else if (FAILED(result))
                    {
                        ++failedSnapshotCount;
                        continue;
                    }

                    // Reuse the next free slot in place; stale data from an earlier occupant is fully
                    // overwritten below. No per-thread overflow guard is needed: window.visitCount is
                    // capped at _capture.size() and failed snapshots only lower _capturedCount, so this
                    // index can never reach the slot count.
                    auto& captured = _capture[_capturedCount];
                    captured.ManagedThreadId = threadId;

                    // Resolve the OS thread id from the ThreadID HERE, under suspend, NOT post-resume. A
                    // ThreadID is a Thread* that is valid only until ThreadDestroyed fires; on CoreCLR the
                    // runtime is globally suspended for this whole loop, so the thread cannot be destroyed
                    // and the mapping is safe. (On .NET Framework there is no global suspend, but this call
                    // sits in the same tight loop as the walk instead of after resume + all name/metadata
                    // resolution, shrinking the death window to negligible.) GetThreadInfo is a cheap
                    // Thread* getter -- no allocation, no metadata -- so it is safe under suspend. HRESULT
                    // is intentionally unchecked: on failure OsThreadId stays 0 and the post-resume name/CPU
                    // lookups already treat 0 as "no name, off-CPU".
                    captured.OsThreadId = 0;
                    _corProfilerInfo->GetThreadInfo(threadId, &captured.OsThreadId);

                    // threadContext was stamped by StaticStackFrameCallback DURING DoStackSnapshot, while
                    // this specific thread was suspended -- true on every platform, unlike a read gated on
                    // global SuspendRuntime (CoreCLR/Linux only). If the walk failed before the
                    // callback ever ran (rare: e.g. zero managed frames), threadContext stays the zero
                    // value it was initialized to, same as a genuine TryGet miss. Plain copy here, not a
                    // fresh TryGet -- re-reading now would be the exact post-resume race this replaces.
                    captured.Context = threadContext;

                    // IsAgentWork is a wait-free atomic load (allocation-free, no CLR calls), so it is safe
                    // under suspend. It is the thread-IDENTITY signal that catches agent threads parked in
                    // System.Threading.Monitor.Wait, which no frame-text predicate can see.
                    captured.IsAgentWork = _agentWork.IsAgentWork(threadId);

                    // Copy the FunctionID sequence out of the reused walk buffer now, since it is
                    // overwritten by the next thread; metadata/signature resolution is deferred to
                    // post-resume. clear() retains capacity (reserved to MaxStackFramesSupported), so these
                    // push_backs cannot reallocate.
                    captured.FunctionIds.clear();
                    for (auto it = std::begin(threadProfile._stackwalk); it != threadProfile._frameNext; ++it)
                    {
                        captured.FunctionIds.push_back(it->functionId);
                    }

                    ++_capturedCount;
                }
                catch (...)
                {
                    // Counted rather than logged: logging here would take StdLog's mutex and allocate
                    // inside the suspend window.
                    ++exceptionCount;
                }
            }
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
        // name/type/signature resolution is deferred to the post-resume ResolveCapturedFrames pass. Runs
        // under the suspend rule (see the class header) -- no allocation, metadata calls, locks, or logging.
        //
        // Returning anything other than S_OK makes the CLR abort the walk (SWA_ABORT ->
        // CORPROF_E_STACKSNAPSHOT_ABORTED), which is how the overflow path below stops a too-deep walk.
        // Frames already written to the buffer survive the abort.
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
            }
            return S_OK;
        }

        // Free the per-session sampling buffers back to the allocator so a stopped session's peak memory
        // shrinks back instead of staying resident until the profiler object is destroyed. Callers MUST
        // guarantee the sampler worker thread is not inside CaptureAllThreads: Stop() enforces this by
        // holding SuspendMutex (the whole capture cycle runs under it) with _samplingActive already
        // false; Shutdown() enforces it by having joined the worker. Nothing here touches CLR state or
        // takes a lock that could be held under suspend, and every operation is on a worker-owned buffer,
        // so it is a plain, allocation-freeing reset.
        //
        // swap-with-a-fresh-empty is used instead of clear() because clear()/shrink_to_fit() does not
        // *guarantee* the capacity is returned (shrink_to_fit is a non-binding request); swapping in an
        // empty container is the guaranteed release idiom. The next Start() re-allocates lazily on the
        // worker's first tick via the size guards in CaptureAllThreads, so this only trades a small
        // restart cost for the retained memory between sessions.
        //
        // Deliberately does NOT clear _nameCache: it is a BoundedLruCache (fixed cap, cannot grow
        // without bound), and clearing it would force every frame to re-resolve its type/method metadata
        // on the next session. That is memory we keep on purpose to make restarts cheap -- a genuine
        // memory/restart-latency tradeoff, resolved in favor of keeping the bounded cache. _resolveScratch
        // (one fixed ~4 KB frame) is likewise left alone -- not worth churning.
        void ReleaseSamplingResources() noexcept
        {
            _stackwalk.reset();

            std::vector<CapturedThread>().swap(_capture);
            std::vector<ResolvedThread>().swap(_resolved);
            std::vector<ThreadID>().swap(_threadList);
            std::vector<uint8_t>().swap(_encodeScratch);
            std::unordered_map<DWORD, ThreadCpuSample>().swap(_prevCpuSamples);

            _capturedCount = 0;
            _rotationOffset = 0;

            _sampleBuffers.Reset();
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

        // Lifecycle mutex -- serializes Start()/Stop()/Shutdown() so the joinable() check, thread
        // create, join(), and _shuttingDown/_samplingActive resets never interleave across concurrent
        // callers. DISTINCT from _mtx_wake (which only guards the sampler wake condition): the worker
        // thread takes _mtx_wake but never _mtx_lifecycle, so holding _mtx_lifecycle across join()
        // cannot deadlock the worker. Lock ordering is always _mtx_lifecycle then _mtx_wake; no path
        // takes them in the opposite order.
        std::mutex _mtx_lifecycle;

        // Worker thread that periodically samples all managed threads.
        std::thread _workerThread;

        // Interface to the CLR execution engine and metadata services. Provided during profiler Initialize.
        CComPtr<ICorProfilerInfo4> _corProfilerInfo;
        CComPtr<ICorProfilerInfo10> _corProfilerInfo10;

        // Decides whether CaptureAllThreads suspends the runtime wide (CoreCLR) or relies solely on
        // DoStackSnapshot's per-thread suspend (.NET Framework) -- see Init.
        bool _isCoreClr = false;

        // Set once the first missing-ICorProfilerInfo10 tick has logged a Warn (see CaptureAllThreads).
        // The condition is permanent for the process (this runtime will never support Continuous
        // Profiling), so every subsequent tick logs at Debug instead of re-warning every sample interval.
        bool _loggedUnsupportedRuntimeWarning = false;

        // Preallocated stack-frame buffer, reused across ticks. Allocated lazily on the first capture
        // (outside the suspend window). NEVER allocated while the runtime is suspended.
        std::unique_ptr<StackWalk> _stackwalk;

        // Persistent per-tick capture buffer. Resized ONCE, to ThreadCountForReservation slots, on the
        // first CaptureAllThreads call -- outside the suspend window -- with each slot's FunctionIds
        // vector reserved to MaxStackFramesSupported. Every tick thereafter, ProfileAllThreads reuses
        // slots [0, _capturedCount) in place (clear() + push_back, never resize/emplace_back), so no
        // allocation is possible while the runtime is suspended. Slots at or beyond _capturedCount hold
        // stale data from an earlier tick and must not be read until claimed and overwritten again.
        std::vector<CapturedThread> _capture;

        // Resolved output, indexed in lockstep with _capture (same size, same [0, _capturedCount) bound).
        // ONLY ever written by ResolveCapturedFrames/EnrichCapturedThreads and read by EncodeAndPublish --
        // all post-resume. Nothing in ProfileAllThreads (the suspend-window code) may touch this.
        std::vector<ResolvedThread> _resolved;

        // Number of valid, freshly-written entries in _capture/_resolved for the current/most recent tick.
        // Set by ProfileAllThreads (under suspend); read by ResolveCapturedFrames, EnrichCapturedThreads,
        // and EncodeAndPublish (all post-resume) to bound their iteration.
        size_t _capturedCount{ 0 };

        // Round-robin start offset for thread capture, carried across ticks and advanced by each tick's
        // visitCount (see PlanCaptureWindow / ProfileAllThreads). Only ever touched by the sampler thread.
        // When live threads exceed the capture slots this rotates which threads are sampled each tick so no
        // fixed subset is permanently dropped; when they fit it is a no-op. Unbounded growth is harmless --
        // it is always read modulo the current thread count.
        size_t _rotationOffset{ 0 };

        // Managed-thread ID list for the current tick. Filled by EnumerateThreadsInto() BEFORE the runtime is
        // suspended -- EnumThreads plus building this ID vector must NOT happen inside the suspend window: an
        // app thread suspended while holding the CRT heap lock would deadlock any allocation here (the same
        // hazard the _capture/_stackwalk preallocation avoids). OTel enumerates pre-suspend for this reason.
        // Reserved ONCE to ThreadCountForReservation (outside the window) and reused every tick; clear()
        // retains capacity so steady-state ticks do not allocate. A tick that sees more managed threads than
        // the reserve grows it -- but only ever while the runtime is running, never suspended.
        std::vector<ThreadID> _threadList;

        // Type/method name cache, reused across ticks. Populated post-resume in ResolveCapturedFrames
        // (never touched inside the snapshot callback, which now only records FunctionIDs).
        NameCache _nameCache;

        // Reusable scratch frame for post-resume name/signature resolution (prealloc name + sig buffers),
        // so ResolveIntoCache does not allocate ~4 KB per resolved function. Touched only by the sampling
        // thread, after resume.
        StackFrame _resolveScratch;

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

        // Previous-tick per-OS-thread CPU reading, keyed by OS thread id. Read/written only on the sampler
        // thread, only post-resume (allocation is fine here). Rebuilt each tick in EnrichCapturedThreads to
        // prune dead threads. The stored creation stamp is what keeps a recycled tid from inheriting the
        // previous holder's baseline (see IsOnCpu).
        std::unordered_map<DWORD, ThreadCpuSample> _prevCpuSamples;

        // Scratch buffer the encoder writes into each tick before the bytes are swapped into a filled
        // double-buffer slot. Reused across ticks; only touched by the sampling thread (after resume),
        // so it needs no lock of its own.
        std::vector<uint8_t> _encodeScratch;

        // Hard ceiling on a single encoded batch (fixed max buffer size). A batch that would exceed this
        // is truncated + stat-counted rather than growing without bound.
        static constexpr size_t MaxBufferBytes = 4 * 1024 * 1024;

        // Fixed size of the trailer EncodeAndPublish always writes after the per-sample loop: one
        // BatchStats opcode (1 + int64 + int32 + int32 + int32 = 21 bytes) + one EndBatch opcode (1 byte).
        // Reserved in the per-sample WillFit check so a maxed-out batch can't overshoot MaxBufferBytes.
        static constexpr size_t TrailerBytes = 22;

        // Two-slot FIFO double-buffer (mirror OTel cpu_buffer_a/b): after resume the producer publishes
        // this tick's batch into a free slot; the managed reader drains the OLDEST filled slot. When both
        // slots are filled the producer applies back-pressure by SKIPPING the tick before it suspends
        // anything (never blocks the app). Owns its own lock -- see SampleBufferQueue.h.
        SampleBufferQueue _sampleBuffers;

        // Truncated-byte total already reported by ReadThreadSamples, so each truncating drain logs once.
        // Only ever touched on the managed reader's thread, inside ReadThreadSamples.
        uint64_t _reportedTruncatedBytes{ 0 };
    };

    // Pre-C++17, a static constexpr member that is ODR-used (bound to a reference, as it is at
    // MinIntervalMs's only use site: std::max<uint32_t>(intervalMs, MinIntervalMs) takes its arguments by
    // const&) needs a namespace-scope definition in addition to the in-class declaration, or the symbol is
    // left undefined at link time. MSVC does not enforce this and links fine without it; the Linux build
    // (Clang, -std=c++11/14) does not, and fails to link/load.
    constexpr uint32_t ContinuousProfiler::MinIntervalMs;
}}}
