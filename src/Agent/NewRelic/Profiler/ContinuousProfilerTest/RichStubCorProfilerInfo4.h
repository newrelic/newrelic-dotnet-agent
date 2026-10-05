/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <cstdint>
#include <vector>
#include "StubCorProfilerInfo4.h"

// A richer ICorProfilerInfo4 stand-in that -- unlike StubCorProfilerInfo4, which E_NOTIMPL's the entire
// capture pipeline -- returns a real, scripted thread list and drives the profiler's stack-snapshot
// callback with synthetic frames. It exists so ProfileAllThreads' failure/truncation handling
// (DoStackSnapshot failing, or a deliberately-aborted too-deep walk) can be executed for real without a
// live CLR. It only overrides the handful of ICorProfilerInfo4 methods ProfileAllThreads /
// EnumerateThreadsInto actually call on a capture path; everything else keeps the base's E_NOTIMPL.
//
// Not thread-safe and not reference-counted (AddRef/Release are inherited no-ops): stack-allocated by the
// test and driven single-threaded, exactly like the base stub.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    // Per-thread script controlling what DoStackSnapshot does for one ThreadID.
    struct ThreadSnapshotScript
    {
        ThreadID Thread{ 0 };
        // Frames the CLR would deliver leaf->root during DoStackSnapshot. For a deliberately-too-deep walk,
        // populate more than MaxStackFramesSupported entries; the profiler's callback aborts partway and the
        // walk returns CORPROF_E_STACKSNAPSHOT_ABORTED (see FailImmediately == false below).
        std::vector<FunctionID> Frames;
        // When true, deliver NO frames and return HrIfNoAbort directly (models a DoStackSnapshot that fails
        // before producing any frame -- e.g. a managed thread with no managed frames, or one that died
        // between EnumThreads and the snapshot). failedSnapshotCount must count these and drop the thread.
        bool FailImmediately{ false };
        // HRESULT returned when the walk completes without the callback aborting (or, when FailImmediately,
        // returned immediately). A successful walk uses S_OK; an immediate failure uses E_FAIL.
        HRESULT HrIfNoAbort{ S_OK };
        // When true, GetThreadInfo fails for this thread (models a dead/unresolvable Thread*), exercising
        // ProfileAllThreads' synthetic-id fallback instead of the normal Thread* -> OS id resolution below.
        bool FailGetThreadInfo{ false };
    };

    // Minimal ICorProfilerThreadEnum over a fixed ThreadID list, matching EnumerateThreadsInto's use
    // (GetCount, then batched Next returning S_FALSE at the end).
    class StubThreadEnum : public ICorProfilerThreadEnum
    {
    public:
        explicit StubThreadEnum(std::vector<ThreadID> threads) : _threads(std::move(threads)) {}

        virtual HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
        {
            if (ppvObject == nullptr)
            {
                return E_POINTER;
            }
            if (riid == __uuidof(ICorProfilerThreadEnum) || riid == __uuidof(IUnknown))
            {
                *ppvObject = static_cast<ICorProfilerThreadEnum*>(this);
                return S_OK;
            }
            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }
        virtual ULONG STDMETHODCALLTYPE AddRef() override { return 1; }
        virtual ULONG STDMETHODCALLTYPE Release() override { return 1; }

        virtual HRESULT STDMETHODCALLTYPE Skip(ULONG celt) override
        {
            _cursor += celt;
            if (_cursor > _threads.size())
            {
                _cursor = _threads.size();
            }
            return S_OK;
        }
        virtual HRESULT STDMETHODCALLTYPE Reset() override { _cursor = 0; return S_OK; }
        virtual HRESULT STDMETHODCALLTYPE Clone(ICorProfilerThreadEnum** ppEnum) override
        {
            if (ppEnum == nullptr)
            {
                return E_POINTER;
            }
            *ppEnum = nullptr;
            return E_NOTIMPL;
        }
        virtual HRESULT STDMETHODCALLTYPE GetCount(ULONG* pcelt) override
        {
            if (pcelt == nullptr)
            {
                return E_POINTER;
            }
            *pcelt = static_cast<ULONG>(_threads.size());
            return S_OK;
        }
        virtual HRESULT STDMETHODCALLTYPE Next(ULONG celt, ThreadID ids[], ULONG* pceltFetched) override
        {
            ULONG fetched = 0;
            for (; fetched < celt && _cursor < _threads.size(); ++fetched, ++_cursor)
            {
                ids[fetched] = _threads[_cursor];
            }
            if (pceltFetched != nullptr)
            {
                *pceltFetched = fetched;
            }
            // S_FALSE signals "fewer than requested / end reached", which EnumerateThreadsInto uses to stop.
            return fetched < celt ? S_FALSE : S_OK;
        }

    private:
        std::vector<ThreadID> _threads;
        size_t _cursor{ 0 };
    };

    class RichStubCorProfilerInfo4 : public StubCorProfilerInfo4
    {
    public:
        // Configure the threads (and their scripts) EnumThreads/DoStackSnapshot will act on, in order.
        void SetScripts(std::vector<ThreadSnapshotScript> scripts) { _scripts = std::move(scripts); }

        virtual HRESULT STDMETHODCALLTYPE EnumThreads(ICorProfilerThreadEnum** ppEnum) override
        {
            if (ppEnum == nullptr)
            {
                return E_POINTER;
            }
            std::vector<ThreadID> threads;
            threads.reserve(_scripts.size());
            for (const auto& script : _scripts)
            {
                threads.push_back(script.Thread);
            }
            // Leaked by design: the profiler wraps this in a CComPtr and Releases it, but Release is a no-op
            // here (matching the base stub's non-owning contract), so a plain heap object is fine for a
            // short-lived single-threaded test. AddRef is likewise a no-op.
            *ppEnum = new StubThreadEnum(std::move(threads));
            return S_OK;
        }

        virtual HRESULT STDMETHODCALLTYPE DoStackSnapshot(ThreadID thread, StackSnapshotCallback* callback,
            ULONG32 /*infoFlags*/, void* clientData, BYTE /*context*/[], ULONG32 /*contextSize*/) override
        {
            const ThreadSnapshotScript* script = nullptr;
            for (const auto& candidate : _scripts)
            {
                if (candidate.Thread == thread)
                {
                    script = &candidate;
                    break;
                }
            }
            if (script == nullptr)
            {
                return E_FAIL;
            }
            if (script->FailImmediately)
            {
                return script->HrIfNoAbort;
            }

            for (const auto functionId : script->Frames)
            {
                if (callback(functionId, 0, static_cast<COR_PRF_FRAME_INFO>(0), 0, nullptr, clientData) != S_OK)
                {
                    // The callback aborted the walk (SWA_ABORT) -- the CLR surfaces this as
                    // CORPROF_E_STACKSNAPSHOT_ABORTED. Frames already delivered are retained; the profiler
                    // tells this apart from a genuine failure via ThreadProfile::_truncated.
                    return CORPROF_E_STACKSNAPSHOT_ABORTED;
                }
            }
            return script->HrIfNoAbort;
        }

        // Resolve the OS thread id as the low bits of the ThreadID, so captured threads carry a stable,
        // distinct, non-zero OS id (post-resume name/CPU lookups treat 0 as "no name, off-CPU").
        virtual HRESULT STDMETHODCALLTYPE GetThreadInfo(ThreadID threadId, DWORD* pdwWin32ThreadId) override
        {
            if (pdwWin32ThreadId == nullptr)
            {
                return E_POINTER;
            }
            for (const auto& candidate : _scripts)
            {
                if (candidate.Thread == threadId && candidate.FailGetThreadInfo)
                {
                    return E_FAIL;
                }
            }
            *pdwWin32ThreadId = static_cast<DWORD>(threadId);
            return S_OK;
        }

    private:
        std::vector<ThreadSnapshotScript> _scripts;
    };

    // A scriptable ICorProfilerInfo10 stand-in that drives ContinuousProfiler's CoreCLR stop-the-world
    // path (the _isCoreClr branch in CaptureAllThreads that calls SuspendRuntime/ResumeRuntime). The base
    // stubs deliberately REFUSE ICorProfilerInfo10 from QueryInterface so the profiler takes its "runtime
    // cannot suspend" branch; this one HANDS IT OUT, so Init() populates _corProfilerInfo10 and the
    // suspend/resume path executes for real -- letting the three otherwise-untested failure branches be
    // exercised without a live CLR:
    //   1. an exception inside the suspend window must still ResumeRuntime (never leave the runtime hung);
    //   2. a failed SuspendRuntime must NOT ResumeRuntime (we never owned the suspend);
    //   3. a failed ResumeRuntime must hit the warning path (not crash / not throw).
    //
    // SuspendRuntime/ResumeRuntime return caller-configured HRESULTs and record call counts plus a running
    // NetSuspendDepth (incremented on a SUCCESSFUL suspend, decremented on every resume attempt). A tick
    // that suspends and resumes correctly leaves NetSuspendDepth back at 0 -- the balanced-pairing
    // invariant this whole cluster is about. Inherits RichStubCorProfilerInfo4's EnumThreads/DoStackSnapshot
    // scripting (defaulting to an empty thread list, which is all branches 2 and 3 need).
    //
    // Not thread-safe and not reference-counted: stack-allocated by the test and driven single-threaded.
    class ScriptableSuspendResumeCorProfilerInfo : public RichStubCorProfilerInfo4
    {
    public:
        // Configurable results (default success). Set to a failure HRESULT to drive branches 2 / 3.
        HRESULT SuspendRuntimeResult{ S_OK };
        HRESULT ResumeRuntimeResult{ S_OK };

        // Observations, read by the test after driving a capture.
        int SuspendCallCount{ 0 };
        int ResumeCallCount{ 0 };
        // ++ on a successful SuspendRuntime, -- on every ResumeRuntime call. 0 after a balanced tick;
        // a non-zero value would mean a suspend without a matching resume (or vice versa) -- the exact
        // process-hang-class defect this cluster guards against.
        int NetSuspendDepth{ 0 };

        // When true, QueryInterface refuses ICorProfilerInfo10 (as the base stubs do), so Init() leaves
        // _corProfilerInfo10 null and CaptureAllThreads takes its "runtime does not support Info10" branch.
        // Lets a test assert that branch neither suspends nor resumes -- while still reading the counters.
        bool RefuseInfo10{ false };

        virtual HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
        {
            if (ppvObject == nullptr)
            {
                return E_POINTER;
            }
            const bool wantsInfo10Family =
                riid == __uuidof(ICorProfilerInfo10) || riid == __uuidof(ICorProfilerInfo9) ||
                riid == __uuidof(ICorProfilerInfo8) || riid == __uuidof(ICorProfilerInfo7) ||
                riid == __uuidof(ICorProfilerInfo6) || riid == __uuidof(ICorProfilerInfo5);
            const bool wantsInfo4Family =
                riid == __uuidof(ICorProfilerInfo4) || riid == __uuidof(ICorProfilerInfo3) ||
                riid == __uuidof(ICorProfilerInfo2) || riid == __uuidof(ICorProfilerInfo) ||
                riid == __uuidof(IUnknown);

            if ((wantsInfo10Family && !RefuseInfo10) || wantsInfo4Family)
            {
                *ppvObject = static_cast<ICorProfilerInfo10*>(this);
                return S_OK;
            }
            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }

        virtual HRESULT STDMETHODCALLTYPE SuspendRuntime(void) override
        {
            ++SuspendCallCount;
            if (SUCCEEDED(SuspendRuntimeResult))
            {
                ++NetSuspendDepth;
            }
            return SuspendRuntimeResult;
        }

        virtual HRESULT STDMETHODCALLTYPE ResumeRuntime(void) override
        {
            ++ResumeCallCount;
            --NetSuspendDepth;
            return ResumeRuntimeResult;
        }
    };
}}}
