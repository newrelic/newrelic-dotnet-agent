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
//   * It NEVER blocks the app thread. Every lock it takes is a try_lock; a lost race costs one allocation
//     sample, which is statistically irrelevant to a subsampled profile and is never a correctness issue.
//   * It NEVER throws. This is a COM callback boundary; an escaping exception would propagate into the
//     runtime's EventPipe dispatch.
//   * Session teardown must NEVER block agent shutdown -- see the Shutdown() comment.
//
// Concurrency model: allocation ticks arrive on MANY app threads at once, but almost all of this class's
// state (the sub-sampler's RNG, the name cache, the frame resolver's scratch frame, the encode buffer)
// is single-threaded-only. Rather than sprinkling locks over each, one try_lock'd _tickMutex makes the
// whole handler mutually exclusive: exactly one thread handles a tick at a time and the rest return
// immediately. That also makes this class the single producer SampleBufferQueue::HasFreeSlot assumes.
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
            LogInfo(L"Initializing AllocationSampler");

            _corProfilerInfo = corProfilerInfo;
            _traceContexts = sharedTraceContexts;

            if (corProfilerInfo == nullptr)
            {
                return;
            }

            HRESULT hr = corProfilerInfo->QueryInterface(__uuidof(ICorProfilerInfo12), (void**)&_corProfilerInfo12);
            if (SUCCEEDED(hr))
            {
                LogInfo(L"AllocationSampler: ICorProfilerInfo12 available");
            }
            else
            {
                LogInfo(L"AllocationSampler: ICorProfilerInfo12 unavailable (.NET Framework or old CoreCLR) -- allocation sampling disabled");
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

        // Whether allocation sampling can work in this process at all (i.e. the runtime is new enough to
        // expose the EventPipe profiling API). Lets the caller skip setting COR_PRF_HIGH_MONITOR_EVENT_PIPE
        // and skip Start() entirely on .NET Framework.
        bool IsAvailable() const noexcept
        {
            return _corProfilerInfo12 != nullptr;
        }

        // Open the AllocationTick EventPipe session and arm the handler. No-op (logged) when
        // ICorProfilerInfo12 is unavailable. Idempotent: a second Start() after a Stop() re-arms the
        // handler and resets the sub-sampler WITHOUT opening a second session -- overwriting _sessionId
        // would orphan the first session (nothing could ever stop it) and double the runtime's event cost.
        void Start(uint32_t maxSamplesPerMinute) noexcept
        {
            if (!_corProfilerInfo12)
            {
                LogDebug(L"AllocationSampler: Start ignored; the EventPipe profiling API is unavailable in this runtime");
                return;
            }

            try
            {
                {
                    // Held so a tick in flight can never observe a half-replaced sub-sampler. Start() runs
                    // on the agent's own thread, where a brief wait is fine (unlike the tick path, which
                    // only ever try_locks).
                    std::lock_guard<std::mutex> tickLock(_tickMutex);
                    _subSampler.reset(new AllocationSubSampler(maxSamplesPerMinute, SubSampleCycleSeconds));
                }

                if (_sessionId != 0)
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
                _sessionId = sessionId;
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
        void Stop() noexcept
        {
            _sessionActive.store(false);
        }

        // Close the EventPipe session. NEVER blocks agent shutdown indefinitely: EventPipeStopSession has
        // to rendezvous with the runtime's own EventPipe machinery, so it is run on a DETACHED thread with
        // a bounded wait. On timeout the call is abandoned (the detached thread keeps its own references
        // alive, so nothing dangles) instead of hanging the agent's shutdown path behind the runtime.
        //
        // std::async is deliberately NOT used here: the std::future it returns has a BLOCKING destructor,
        // so the "bounded wait" would be silently undone by the future going out of scope -- the exact
        // hang this is written to avoid.
        void Shutdown() noexcept
        {
            _sessionActive.store(false);

            if (!_corProfilerInfo12 || _sessionId == 0)
            {
                return;
            }

            // Claim the session so a second Shutdown() (the destructor always calls it as a safety net)
            // cannot stop the same session twice.
            const EVENTPIPE_SESSION sessionId = _sessionId;
            _sessionId = 0;

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
                const bool stopped = signal->Cv.wait_for(l, std::chrono::seconds(StopSessionTimeoutSeconds),
                    [signal]() { return signal->Done; });

                if (stopped)
                {
                    LogTrace(L"AllocationSampler: EventPipe session stopped");
                }
                else
                {
                    LogError(L"AllocationSampler: EventPipeStopSession did not return within ",
                        static_cast<uint32_t>(StopSessionTimeoutSeconds), L"s; abandoning it rather than blocking shutdown");
                }
            }
            catch (const std::exception&)
            {
                LogError(L"AllocationSampler: exception stopping the EventPipe session");
            }
        }

        // Drain the oldest filled sample buffer into the caller's array; the allocation-sample counterpart
        // of ContinuousProfiler::ReadThreadSamples, decoded by the same managed BufferParser. Returns the
        // number of bytes written (0 when nothing is ready or the args are invalid). Never throws.
        int32_t ReadAllocationSamples(int32_t len, unsigned char* buf) noexcept
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
                LogTrace(L"AllocationSampler: exception draining sample buffer");
            }

            return 0;
        }

        // Handle one AllocationTick. Runs ON THE ALLOCATING APP THREAD inside the runtime's EventPipe
        // dispatch (see the class comment). Callers must have already filtered the event with
        // IsAllocationTickEvent. Never throws, never blocks.
        void OnAllocationTick(ULONG dataLen, LPCBYTE data) noexcept
        {
            if (!_sessionActive.load())
            {
                return;
            }

            try
            {
                // Serialize tick handling across app threads (see the class comment). try_lock, never
                // lock: a concurrent tick is dropped, not queued behind another thread's stack walk.
                std::unique_lock<std::mutex> tickLock(_tickMutex, std::try_to_lock);
                if (!tickLock.owns_lock())
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

                // Back-pressure before the expensive part: if the managed reader has not drained, this
                // sample has nowhere to go, so skip the walk/resolve/encode instead of throwing the result
                // away at publish time. Safe as a gate because _tickMutex makes this the single producer.
                if (!_sampleBuffers.HasFreeSlot())
                {
                    LogTrace(L"AllocationSampler: sample buffers full; skipping tick (reader has not drained)");
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

        // Safety net for the case where managed code never calls Shutdown() explicitly: leaving an
        // EventPipe session open costs the process the runtime's verbose GC event traffic forever.
        // Shutdown() is idempotent (it zeroes _sessionId), so this is safe after an explicit call.
        ~AllocationSampler() noexcept
        {
            Shutdown();
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

        // Hard ceiling on one encoded allocation sample. Far smaller than ContinuousProfiler's per-tick
        // all-threads batch because this is a single sample.
        static constexpr size_t MaxAllocationBufferBytes = 64 * 1024;

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

        // Resolve names and encode this ONE sample into its own batch, then publish it. Runs on the
        // allocating thread with no lock held beyond _tickMutex: metadata calls, string building and
        // allocation are all fine here (nothing is suspended). Never throws.
        void EncodeAndPublish(ThreadID managedThreadId, const TraceContext& context, uint64_t allocatedSize,
            const xstring_t& typeName, const std::vector<FunctionID>& functionIds) noexcept
        {
            try
            {
                // Not error-checked, exactly as in ContinuousProfiler::EnrichCapturedThreads: on failure
                // the id stays 0 and ResolveOsThreadName treats that as "no name".
                DWORD osThreadId = 0;
                _corProfilerInfo->GetThreadInfo(managedThreadId, &osThreadId);

                const auto nowNanos = std::chrono::duration_cast<std::chrono::nanoseconds>(
                    std::chrono::system_clock::now().time_since_epoch()).count();

                SampleBufferWriter writer(_encodeScratch, MaxAllocationBufferBytes);
                writer.BeginBatch();
                writer.WriteStartBatch(nowNanos);
                writer.WriteStartAllocationSample();
                writer.WriteThreadName(ResolveOsThreadName(osThreadId));
                writer.WriteInt64Field(static_cast<int64_t>(osThreadId));
                writer.WriteInt64Field(context.TraceIdHigh);
                writer.WriteInt64Field(context.TraceIdLow);
                writer.WriteInt64Field(context.SpanId);
                writer.WriteInt64Field(nowNanos / 1000000); // sample timestamp, milliseconds
                writer.WriteUInt64Field(allocatedSize);
                writer.WriteStringField(typeName);

                for (const auto functionId : functionIds)
                {
                    const xstring_t frame = _frameNames ? _frameNames->ResolveFrameName(functionId) : xstring_t();

                    // Keep the encoded sample inside the fixed buffer instead of growing it without bound.
                    // The reservation covers this frame's worst case (a fresh, uninterned definition) plus
                    // the list terminator, so there is always room to close the frame list.
                    const size_t chars = frame.size() < SampleBufferWriter::MaxStringChars
                        ? frame.size() : SampleBufferWriter::MaxStringChars;
                    if (!writer.WillFit(2 + 2 + (chars * 2) + 2))
                    {
                        LogTrace(L"AllocationSampler: sample buffer full mid-sample; truncating the frame list");
                        break;
                    }

                    writer.WriteCodedFrameString(frame);
                }

                writer.WriteFrameListTerminator();
                writer.WriteEndBatch();

                // The OnAllocationTick gate normally catches saturation before any of the above is paid
                // for, so reaching this drop means the queue filled while this sample was being built.
                if (!_sampleBuffers.TryPublish(_encodeScratch))
                {
                    LogTrace(L"AllocationSampler: sample buffers full; dropping allocation sample");
                }
                _encodeScratch.clear();
            }
            catch (...)
            {
                LogTrace(L"AllocationSampler: exception encoding allocation sample");
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
        // single-threaded-only and protected by it. Only ever try_lock'd on the tick path.
        std::mutex _tickMutex;

        // Rate limiter. Created by Start() (so a restart re-paces from zero) under _tickMutex.
        std::unique_ptr<AllocationSubSampler> _subSampler;

        // Whether the handler should produce samples. Toggled by Start()/Stop()/Shutdown() and read on
        // every tick, so it is the one member that is deliberately atomic rather than _tickMutex-guarded.
        std::atomic<bool> _sessionActive{ false };

        // The open EventPipe session, or 0 when none is open. Written only by Start()/Shutdown().
        EVENTPIPE_SESSION _sessionId{ 0 };

        // Type/method name cache. Declared BEFORE _frameNames, which borrows it, so reverse-order
        // destruction tears the resolver down first.
        NameCache _nameCache;

        // Frame-name resolution over _nameCache. This sampler's own instance -- see the note in Init().
        std::unique_ptr<FrameNameResolver> _frameNames;

        // Scratch buffer the encoder writes into before the bytes are swapped into a filled queue slot.
        std::vector<uint8_t> _encodeScratch;

        // Two-slot FIFO hand-off to the managed reader (its own lock -- see SampleBufferQueue.h). Separate
        // from ContinuousProfiler's queue so allocation samples and thread samples cannot starve each
        // other, and so each is drained by its own managed reader.
        SampleBufferQueue _sampleBuffers;
    };
}}}
