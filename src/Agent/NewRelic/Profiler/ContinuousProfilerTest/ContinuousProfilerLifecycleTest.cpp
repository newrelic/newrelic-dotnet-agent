/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <atomic>
#include <thread>
#include <vector>

// ContinuousProfiler.h logs via the shared Logger, whose StdLog/logging_available/GetLevelString
// globals must be defined in exactly ONE translation unit per test binary (ODR). This is the only
// TU in ContinuousProfilerTest that pulls in Logger.h, so it owns the definition -- matching the
// pattern in the other profiler test projects (e.g. MethodRewriterTest.cpp, SignatureParserTest.cpp).
#define LOGGER_DEFINE_STDLOG

#include "../ContinuousProfiler/ContinuousProfiler.h"
#include "StubCorProfilerInfo4.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    // Lifecycle serialization tests for ContinuousProfiler::Start/Stop/Shutdown.
    //
    // Start() refuses to arm sampling without an ICorProfilerInfo4, so these Init() the profiler with a
    // stub interface (see StubCorProfilerInfo4.h) rather than a live CLR. The stub refuses
    // ICorProfilerInfo10, so even a tick that did fire would bail before suspending anything -- and a
    // large sampling interval keeps the worker parked in its wait for the duration of each test anyway.
    // What is exercised here is purely the worker create/join/flag-reset lifecycle.
    TEST_CLASS(ContinuousProfilerLifecycleTest)
    {
    private:
        // Large enough that the worker always parks in its interval wait rather than sampling.
        static constexpr uint32_t ParkedIntervalMs = 60000;
        static constexpr int ThreadCount = 8;
        static constexpr int IterationsPerThread = 200;

    public:
        // Failure mode (b): a Start() after a Shutdown() must be able to respawn the worker. This only
        // holds if Shutdown() resets _shuttingDown/_samplingActive AND leaves _workerThread non-joinable
        // (join done) under the same lock the next Start() takes -- otherwise the worker would either
        // never spawn again (joinable stayed true) or spawn and immediately exit on a stale _shuttingDown.
        TEST_METHOD(start_after_shutdown_respawns_the_worker)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            profiler.Start(ParkedIntervalMs);
            Assert::IsTrue(profiler.IsWorkerThreadRunning(), L"worker should exist after the first Start");

            profiler.Shutdown();
            Assert::IsFalse(profiler.IsWorkerThreadRunning(), L"worker should be gone after Shutdown");

            // The regression this guards: before the lifecycle mutex, a Start racing Shutdown's flag
            // reset could leave CP permanently dead. A clean Shutdown-then-Start must respawn.
            profiler.Start(ParkedIntervalMs);
            Assert::IsTrue(profiler.IsWorkerThreadRunning(), L"worker should respawn on Start after Shutdown");

            profiler.Shutdown();
            Assert::IsFalse(profiler.IsWorkerThreadRunning());
        }

        // Start() before Init() cannot sample -- there is no CLR interface to walk with -- so it must
        // refuse rather than spawn a worker that wakes on every interval and silently collects nothing.
        // The window is real: the exported entry point only checks the profiler singleton, which exists
        // from the ctor, well before Initialize() reaches Init().
        TEST_METHOD(start_without_initialization_does_not_spawn_a_worker)
        {
            ContinuousProfiler profiler;

            profiler.Start(ParkedIntervalMs);
            Assert::IsFalse(profiler.IsWorkerThreadRunning(), L"Start must not spawn a worker before Init");
        }

        // Init(nullptr, ...) must not store the null pointer or crash -- it must leave the profiler in
        // the same "not initialized" state as never calling Init at all, so a subsequent Start() still
        // refuses to spawn a worker (see start_without_initialization_does_not_spawn_a_worker).
        TEST_METHOD(init_with_null_cor_profiler_info_does_not_crash_or_arm_sampling)
        {
            ContinuousProfiler profiler;
            profiler.Init(nullptr, false);

            profiler.Start(ParkedIntervalMs);
            Assert::IsFalse(profiler.IsWorkerThreadRunning(), L"Start must not spawn a worker after Init(nullptr)");
        }

        // Start() is idempotent while already running: a second Start must NOT assign a new std::thread
        // over the already-joinable worker (that assignment is what would call std::terminate).
        TEST_METHOD(repeated_start_keeps_a_single_worker)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            profiler.Start(ParkedIntervalMs);
            profiler.Start(ParkedIntervalMs);
            profiler.Start(ParkedIntervalMs);
            Assert::IsTrue(profiler.IsWorkerThreadRunning());

            profiler.Shutdown();
            Assert::IsFalse(profiler.IsWorkerThreadRunning());
        }

        // Stop() pauses sampling but keeps the worker alive; Shutdown() is what tears it down.
        TEST_METHOD(stop_keeps_worker_alive_shutdown_tears_it_down)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            profiler.Start(ParkedIntervalMs);
            profiler.Stop();
            Assert::IsTrue(profiler.IsWorkerThreadRunning(), L"Stop must not join the worker");

            profiler.Shutdown();
            Assert::IsFalse(profiler.IsWorkerThreadRunning());
        }

        // Stop() must reclaim the per-session sampling buffers, not leave them resident until Shutdown,
        // so a stop/start retune shrinks the process back to baseline between sessions. Deterministic
        // and single-threaded: CaptureOnceForTesting drives one synchronous capture (allocating the
        // buffers) with no worker thread; Stop() then frees them synchronously (it takes SuspendMutex
        // and, finding no capture in flight, releases immediately); a second capture proves clean
        // re-allocation. Exercises the real Stop() release path, not a test-only shortcut.
        TEST_METHOD(stop_releases_sampling_buffers_and_next_capture_reallocates)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            Assert::IsFalse(profiler.HasSamplingResourcesForTesting(), L"no buffers should be allocated before the first capture");

            profiler.CaptureOnceForTesting();
            Assert::IsTrue(profiler.HasSamplingResourcesForTesting(), L"a capture must allocate the session buffers");

            profiler.Stop();
            Assert::IsFalse(profiler.HasSamplingResourcesForTesting(), L"Stop must free the session buffers");

            profiler.CaptureOnceForTesting();
            Assert::IsTrue(profiler.HasSamplingResourcesForTesting(), L"a capture after Stop must re-allocate cleanly");

            profiler.Shutdown();
        }

        // Shutdown() must also reclaim the buffers -- the Start()->Shutdown() path with no explicit
        // Stop() -- so nothing is left resident after teardown. Same deterministic single-threaded shape.
        TEST_METHOD(shutdown_releases_sampling_buffers)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            profiler.CaptureOnceForTesting();
            Assert::IsTrue(profiler.HasSamplingResourcesForTesting(), L"a capture must allocate the session buffers");

            profiler.Shutdown();
            Assert::IsFalse(profiler.HasSamplingResourcesForTesting(), L"Shutdown must free the session buffers");
        }

        // Failure mode (a): concurrent Start/Stop/Shutdown from many threads. Without the lifecycle
        // mutex, two Start()s both observing joinable()==false would each assign a std::thread over the
        // other's (now joinable) thread -> std::terminate() kills the test host. With it, the sequence
        // is serialized and simply completes. The assertion is reaching a clean final state at all:
        // a lost-serialization regression would crash before we get here.
        TEST_METHOD(concurrent_lifecycle_calls_do_not_terminate)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            std::atomic<bool> go{ false };
            std::vector<std::thread> threads;
            threads.reserve(ThreadCount);

            for (int t = 0; t < ThreadCount; ++t)
            {
                threads.emplace_back([&profiler, &go, t]()
                {
                    // Spin until all threads are ready, so the hammering overlaps as much as possible.
                    while (!go.load(std::memory_order_acquire))
                    {
                        std::this_thread::yield();
                    }

                    for (int i = 0; i < IterationsPerThread; ++i)
                    {
                        // Interleave the three lifecycle entry points across threads. The exact mix is
                        // unimportant; the point is that create/join/flag-reset from different callers
                        // overlap on the lifecycle mutex.
                        switch ((t + i) % 3)
                        {
                        case 0: profiler.Start(ParkedIntervalMs); break;
                        case 1: profiler.Stop(); break;
                        default: profiler.Shutdown(); break;
                        }
                    }
                });
            }

            go.store(true, std::memory_order_release);
            for (auto& th : threads)
            {
                th.join();
            }

            // Quiesce to a known state: after a final Shutdown the worker must be gone and no thread
            // may remain joinable (which would std::terminate at profiler destruction otherwise).
            profiler.Shutdown();
            Assert::IsFalse(profiler.IsWorkerThreadRunning(), L"worker must be torn down after final Shutdown");
        }
    };
}}}
