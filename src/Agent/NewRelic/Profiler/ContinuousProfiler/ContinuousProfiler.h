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
#include "../SignatureParser/SignatureParser.h"
#include "../SignatureParser/SignatureFormatting.h"
#include "../Profiler/CorTokenResolver.h"
#include "SampleBufferWriter.h"
#include "SuspendMutex.h"
#include "TraceContextMap.h"

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
        void Init(ICorProfilerInfo4* corProfilerInfo) noexcept
        {
            LogInfo(L"Initializing ContinuousProfiler");

            _corProfilerInfo = corProfilerInfo;

            HRESULT corProfilerInfoInitResult = corProfilerInfo->QueryInterface(__uuidof(ICorProfilerInfo10), (void**)&_corProfilerInfo10);
            if (SUCCEEDED(corProfilerInfoInitResult)) {
                LogInfo(L"CP: ICorProfilerInfo10 available");
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
                _samplingActive.store(true);

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
            _samplingActive.store(false);
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

        // Drain one filled sample buffer into the caller's array. This IS the native side of the managed
        // ISampleSource.ReadBatch contract: claim a filled double-buffer slot, memcpy up to `len` bytes
        // into `buf`, free the slot, and return the number of bytes written (0 if no buffer is ready or
        // args are invalid). The managed BufferParser then decodes those bytes. The extern "C" export
        // that P/Invoke calls wraps this member (Task 5). Never throws.
        int32_t ReadThreadSamples(int32_t len, unsigned char* buf) noexcept
        {
            if (buf == nullptr || len <= 0)
            {
                return 0;
            }

            try
            {
                std::lock_guard<std::mutex> l(_mtx_buffers);
                for (auto& slot : _sampleBuffers)
                {
                    if (!slot.Filled)
                    {
                        continue;
                    }

                    // Copy up to `len` bytes. A batch larger than the caller's array is truncated (the
                    // managed parser tolerates a truncated tail, Global Constraint: never throw); the
                    // slot is freed regardless so the producer can reuse it.
                    const size_t available = slot.Bytes.size();
                    const size_t toCopy = available < static_cast<size_t>(len) ? available : static_cast<size_t>(len);
                    if (toCopy > 0)
                    {
                        std::memcpy(buf, slot.Bytes.data(), toCopy);
                    }

                    slot.Bytes.clear();
                    slot.Filled = false;
                    return static_cast<int32_t>(toCopy);
                }
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
        // constraints apply): NameCache, the prealloc name buffers, and the type/method name holder.
        using NameCache = NewRelic::Profiler::ThreadProfiler::NameCache;
        using TypeAndMethodNames = NewRelic::Profiler::ThreadProfiler::TypeAndMethodNames;
        using PreallocTypeName = NewRelic::Profiler::ThreadProfiler::PreallocTypeName;
        using PreallocMethodName = NewRelic::Profiler::ThreadProfiler::PreallocMethodName;

        // How many stack frames we support per thread. Walking truncates (keeping the root) beyond this.
        // Matches ThreadProfiler::MaxStackFramesSupported (ThreadProfiler.h:280).
        static constexpr size_t MaxStackFramesSupported = 1337;

        // A guess at how many threads we will see; used to reserve the per-tick capture vector.
        static constexpr size_t ThreadCountForReservation = 100;

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
            HRESULT _errorCode{};
            ThreadID _managedTID;
            ThreadProfile(ThreadID managedTID, ICorProfilerInfo4* corProfilerInfo, NameCache& nameCache, StackWalk& stackwalk) :
                _corProfilerInfo(corProfilerInfo), _nameCache(nameCache), _stackwalk(stackwalk), _frameNext(std::begin(_stackwalk)), _managedTID(managedTID)
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
            TraceContext Context{}; // stamped AFTER resume from the lock-free trace-context map.
            bool OnCpu{}; // set post-resume from CPU-time delta since last tick; false on the first tick.
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
            _shuttingDown.store(true);
            _cv_wake.notify_one();
        }

        // Worker thread entry point. Initializes the thread for calling the Execution Engine (required
        // before suspending any thread), then loops: sleep for the sampling interval (or until woken by
        // Stop()/Shutdown()), and while sampling is active capture a sample. Terminates when
        // _shuttingDown is true.
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
                        _cv_wake.wait_for(l, std::chrono::milliseconds(_intervalMs.load()),
                            [&]() noexcept { return _shuttingDown.load() || !_samplingActive.load(); });
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
            // Preallocate the large per-thread frame buffer ONCE (several MB), reused every tick. Done
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

#ifdef PAL_STDCPP_COMPAT
                // CoreCLR: explicitly suspend the runtime around the walk (mirror ThreadProfiler.h:650).
                // On .NET Framework / Windows the runtime is not explicitly suspended; DoStackSnapshot
                // suspends each target thread itself.
                if (!_corProfilerInfo10)
                {
                    // Publish nothing and leave the prior buffer state intact -- an early return here
                    // must not clobber a previously filled slot.
                    LogDebug(L"CP: CaptureAllThreads called without ICorProfilerInfo10; skipping sample.");
                    return;
                }
                _corProfilerInfo10->SuspendRuntime();
#endif

                const auto suspendStart = std::chrono::steady_clock::now();
                try
                {
                    ProfileAllThreads(failedSnapshotCount, overflowCount);
                }
                catch (...)
                {
                    // The show must go on -- a failed sample is never fatal.
                    LogTrace(L"CP: exception in CaptureAllThreads");
                }
                microsSuspended = std::chrono::duration_cast<std::chrono::microseconds>(
                    std::chrono::steady_clock::now() - suspendStart).count();

#ifdef PAL_STDCPP_COMPAT
                _corProfilerInfo10->ResumeRuntime();
#endif
            }

            // AFTER ResumeRuntime, all outside the suspend window:
            // 1. Resolve each captured FunctionID sequence into fully-qualified frame names + signatures
            //    (metadata calls, signature parsing, string building -- the bulk of the old suspend cost).
            ResolveCapturedFrames();
            // 2. Resolve each thread's OS thread name (may allocate / read /proc) and tally how many carry a
            //    trace context. The trace-context READ itself already happened under suspend in
            //    ProfileAllThreads (writers frozen -> stable seqlock read).
            const uint32_t threadsWithContext = EnrichCapturedThreads();

            // Diagnostic: how many captured threads carried a trace context this tick. Cheap Finest line.
            LogTrace(L"[ContinuousProfiling] capture: ", _capturedCount, L" thread(s), ", threadsWithContext,
                L" with trace context");

            if (overflowCount != 0)
            {
                // Honest truncation signal, mirroring the sample-buffer truncation log in EncodeAndPublish:
                // more managed threads were successfully walked this tick than the ThreadCountForReservation
                // persistent slots hold, so the extras were dropped rather than growing the capture buffer
                // under suspend.
                LogTrace(L"CP: thread capture overflow; dropped ", overflowCount, L" thread(s) beyond the ",
                    ThreadCountForReservation, L"-slot capture buffer");
            }

            EncodeAndPublish(failedSnapshotCount, batchTimestamp, microsSuspended);
        }

        // Encode this tick's captured stacks into the byte-opcode format BufferParser decodes and hand
        // the result to a free double-buffer slot. Runs AFTER ResumeRuntime, so allocation is fine here.
        // Applies back-pressure: if both buffers are still full (the managed reader has not drained),
        // the tick is DROPPED and logged rather than blocking the app or growing memory.
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

                // Hand the encoded bytes to a free slot; drop (back-pressure) if both are full.
                {
                    std::lock_guard<std::mutex> l(_mtx_buffers);
                    SampleBuffer* freeSlot = nullptr;
                    for (auto& slot : _sampleBuffers)
                    {
                        if (!slot.Filled)
                        {
                            freeSlot = &slot;
                            break;
                        }
                    }

                    if (freeSlot == nullptr)
                    {
                        LogTrace(L"CP: sample buffers full; dropping tick (reader has not drained)");
                        _encodeScratch.clear();
                        return;
                    }

                    freeSlot->Bytes.swap(_encodeScratch);
                    freeSlot->Filled = true;
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
            // 1 opcode + name len prefix(2) + 4 int64 fields(32) + onCpu byte(1) + frame terminator(2).
            size_t bytes = 1 + 2 + 32 + 1 + 2;

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
        // it carries a trace context. The trace-context read happens earlier, under suspend, in
        // ProfileAllThreads; this pass only does the name resolution, which allocates / reads /proc and is
        // therefore NOT suspend-safe -- NEVER call this inside the suspend window. Returns the number of
        // threads that carry a context (diagnostic).
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

                thread.ThreadName = ResolveThreadName(thread.OsThreadId);

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

                // comm is newline-terminated; trim the trailing '\n' and any tail.
                size_t len = read;
                while (len > 0 && (name[len - 1] == '\n' || name[len - 1] == '\r'))
                {
                    --len;
                }
                name[len] = '\0';

                return ToWideString(name);
#else
                // Windows: GetThreadDescription (Win 10+). THREAD_QUERY_LIMITED_INFORMATION is the minimal
                // right needed and succeeds for threads in our own process.
                xstring_t result;
                HANDLE hThread = ::OpenThread(THREAD_QUERY_LIMITED_INFORMATION, FALSE, osThreadId);
                if (hThread == nullptr)
                {
                    return xstring_t();
                }

                PWSTR description = nullptr;
                const HRESULT hr = ::GetThreadDescription(hThread, &description);
                if (SUCCEEDED(hr) && description != nullptr)
                {
                    result.assign(description);
                    ::LocalFree(description);
                }
                ::CloseHandle(hThread);
                return result;
#endif
            }
            catch (...)
            {
                return xstring_t();
            }
        }

        // Cumulative CPU time (user+kernel) for an OS thread, in microseconds; -1 if unavailable (thread
        // gone, or the read failed). Runs POST-resume only, same as ResolveThreadName -- both allocate /
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

                if (pSigBlob != nullptr && sigBlobLength > 0 && sigBlobLength <= MaxSigBlobBytes)
                {
                    std::memcpy(scratch.sigBlob.data(), pSigBlob, sigBlobLength);
                    scratch.sigBlobLength = sigBlobLength;
                }

                auto& typeName = scratch.typeName;
                auto& cachedTypeName = _nameCache.typename_for(scratch.typeDef);
                if (cachedTypeName == TypeAndMethodNames::GetUnknownTypeName())
                {
                    DWORD typeFlags = 0;
                    metaData->GetTypeDefProps(scratch.typeDef, &typeName.first.front(), static_cast<ULONG>(typeName.first.size()), &typeName.second, &typeFlags, nullptr);

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
                    wcscpy_s(typeName.first.data(), static_cast<ULONG>(typeName.first.size()), cachedTypeName->c_str());
                }

                AppendSignature(scratch); // fold the parameter list into the method name
                _nameCache.insert(scratch.functionId, scratch.typeDef, scratch.typeName, scratch.methodName);
            }
            catch (...)
            {
                // Leave uncached -> name-only / UnknownMethod(<id>). Never crash the sampler.
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
                // Leave the bare innermost name as-is; never crash the sampler.
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

        // POST-RESUME: resolve every captured thread's FunctionID sequence into fully-qualified frame names.
        // All metadata + signature + string work happens here, out of the suspend window. Runs on the
        // sampling thread after ResumeRuntime.
        void ResolveCapturedFrames()
        {
            for (size_t i = 0; i < _capturedCount; ++i)
            {
                auto& thread = _capture[i];
                // Frames was already cleared for this slot (under suspend, in ProfileAllThreads) and is
                // reserved to MaxStackFramesSupported, so these emplace_backs cannot reallocate even
                // though this runs post-resume.
                for (const auto functionId : thread.FunctionIds)
                {
                    ResolveIntoCache(functionId);
                    thread.Frames.emplace_back(AssembleFrameName(functionId));
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
                // Keep the name-only method name.
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
        void ProfileAllThreads(uint32_t& failedSnapshotCount, uint32_t& overflowCount)
        {
            _capturedCount = 0;
            failedSnapshotCount = 0;
            overflowCount = 0;

            // _threadList was populated by EnumerateThreadsInto() BEFORE the suspend window opened -- no
            // enumeration or allocation happens here, under suspend.
            for (const auto threadId : _threadList)
            {
                if (IsShutdownRequested())
                {
                    break;
                }

                try
                {
                    // Reset the preallocated per-thread walk state; no allocation happens here.
                    ThreadProfile threadProfile(threadId, _corProfilerInfo, _nameCache, *_stackwalk);

                    // If context is NULL, the walk begins at the last available managed frame for the
                    // target thread (mirror ThreadProfiler.h:585).
                    const auto result = _corProfilerInfo->DoStackSnapshot(threadId, StaticStackFrameCallback,
                        COR_PRF_SNAPSHOT_INFO::COR_PRF_SNAPSHOT_DEFAULT, &threadProfile, nullptr, 0);

                    // A managed thread with no managed frames (e.g. an idle thread-pool thread), or a
                    // thread that died between Enum and snapshot (CORPROF_E_STACKSNAPSHOT_INVALID_TGT_THREAD),
                    // fails here -- record it and skip, never fatal (mirror ThreadProfiler.h:590-596).
                    if (FAILED(result))
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

                    // Read the thread's active trace context HERE, INSIDE the suspend window, while every
                    // app thread -- and therefore any writer of this slot -- is frozen. That quiescence is
                    // exactly the stable-read precondition the lock-free seqlock in TraceContextMap is
                    // built on (see its header). Reading post-resume (the previous location) raced live
                    // re-pushes from the running app thread: the seqlock observed a write-in-progress /
                    // changed seq on nearly every read and bailed per its wait-free contract, so the link
                    // was dropped ~99% of the time. TryGet is wait-free, lock-free, allocation-free and
                    // makes no CLR calls, so it is safe under suspend. TryGet unconditionally overwrites
                    // captured.Context (zeroing it first), so no stale context from this slot's last
                    // occupant can survive. Name/signature resolution runs post-resume in
                    // ResolveCapturedFrames.
                    _traceContexts.TryGet(threadId, captured.Context);

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
                    // (mirror ThreadProfiler.h:611-615).
                    LogTrace(L"CP: exception profiling a thread");
                }
            }

            // Capture is returned to CaptureAllThreads via _capturedCount/_capture; encoding to the byte
            // buffer happens there AFTER ResumeRuntime so no allocation occurs inside the suspend window.
            LogTrace(L"CP: captured ", _capturedCount, L" thread(s); ", failedSnapshotCount, L" snapshot failure(s)");
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
        static HRESULT __stdcall StaticStackFrameCallback(uintptr_t functionId, uintptr_t /* instructionPointer */, uintptr_t /* frameInfo */, uint32_t /* contextSize */, uint8_t[] /* context */, void* clientData)
        {
            try
            {
                const HRESULT StackTooDeep = S_FALSE;

                ThreadProfile& threadProfile = *static_cast<ThreadProfile*>(clientData);

                // We must keep the root of the stack but can afford to lose leaves: if we overflow the
                // preallocated array, reset to the start and count again (mirror ThreadProfiler.h:678-683).
                if (threadProfile._frameNext == std::end(threadProfile._stackwalk))
                {
                    threadProfile._frameNext = std::begin(threadProfile._stackwalk);
                    threadProfile._errorCode = StackTooDeep;
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

        // One slot of the producer/consumer double-buffer. Bytes holds an encoded batch; Filled marks
        // it ready for the managed reader to drain via ReadThreadSamples.
        struct SampleBuffer
        {
            std::vector<uint8_t> Bytes;
            bool Filled{ false };
        };

        // Hard ceiling on a single encoded batch (fixed max buffer size). A batch that would exceed this
        // is truncated + stat-counted rather than growing without bound.
        static constexpr size_t MaxBufferBytes = 4 * 1024 * 1024;

        // Two-slot double-buffer (mirror OTel cpu_buffer_a/b): the producer fills a free slot after
        // resume; the managed reader drains a filled slot. When both are filled the producer applies
        // back-pressure by DROPPING the tick (never blocks the app). Guarded by _mtx_buffers.
        std::mutex _mtx_buffers;
        std::array<SampleBuffer, 2> _sampleBuffers;
    };
}}}
