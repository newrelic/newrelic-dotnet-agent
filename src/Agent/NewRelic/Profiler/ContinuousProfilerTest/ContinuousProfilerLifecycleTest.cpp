/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <atomic>
#include <chrono>
#include <cwchar>
#include <new>
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

        // Cluster C (check-then-act TOCTOU): a capture that started -- passed SamplingThreadStart's
        // _samplingActive check -- must NOT re-allocate the session buffers or publish a batch once Stop()
        // has already flipped the flag false and freed those buffers under SuspendMutex. The worker only
        // reaches SuspendMutex after Stop() releases it, so without the re-check inside CaptureAllThreads
        // the worker would re-allocate the just-freed _stackwalk/_capture and publish one stale post-stop
        // batch into the queue Stop() just reset.
        //
        // Deterministic single-threaded model of that race: arm+capture once (Stop() has something to
        // free), Stop() (sets inactive AND frees), then drive the worker's post-check, post-Stop entry via
        // CaptureAfterStopRaceForTesting -- which does NOT re-arm sampling. With the fix that pass observes
        // _samplingActive==false under SuspendMutex and bails; without it, it re-allocates and publishes.
        TEST_METHOD(capture_racing_a_completed_stop_does_not_reallocate_or_publish)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            // Prime the session: one active capture allocates the buffers and publishes a batch, so Stop()
            // has real state to reclaim and the queue is non-empty going in.
            profiler.CaptureOnceForTesting();
            Assert::IsTrue(profiler.HasSamplingResourcesForTesting(), L"the priming capture must allocate the session buffers");

            // Stop() wins the race: _samplingActive is now false and the buffers are freed / queue reset.
            profiler.Stop();
            Assert::IsFalse(profiler.HasSamplingResourcesForTesting(), L"Stop must free the session buffers");

            // The worker's already-in-flight capture (past its _samplingActive check) now reaches
            // CaptureAllThreads. With the TOCTOU re-check it must bail: no re-allocation of the freed
            // buffers ...
            profiler.CaptureAfterStopRaceForTesting();
            Assert::IsFalse(profiler.HasSamplingResourcesForTesting(),
                L"a capture that lost the race to a completed Stop must not re-allocate the freed buffers");

            // ... and no stale post-stop batch handed to the managed reader. A drain of 0 bytes proves the
            // queue Stop() reset was left empty. (Without the fix the in-flight capture would publish an
            // empty-but-real batch here, and this drain would return > 0.)
            std::vector<unsigned char> drain(4096, 0);
            const int32_t drained = profiler.ReadThreadSamples(static_cast<int32_t>(drain.size()), drain.data());
            Assert::AreEqual(0, drained, L"no post-stop batch may be published into the reset queue");

            profiler.Shutdown();
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

        // Cluster D: the CP name cache is keyed by raw MethodDesc*/Module* addresses (FunctionID and
        // ModuleID). A CLR unload -- a collectible AssemblyLoadContext unloading (Razor runtime
        // compilation, plugin hosts, hot reload), or module/class unload -- frees those allocations, and
        // the allocator can reissue the same addresses to newly loaded code. A cached name that outlived
        // the unload would then be reported for a completely unrelated frame forever, because a cache hit
        // short-circuits re-resolution. InvalidateNameCache() (wired from the profiler's
        // Assembly/Module/Class UnloadStarted callbacks) must therefore cause the stale cache to be
        // cleared. The clear is deferred to the sampler worker's next capture tick -- the cache is not safe
        // to touch from the arbitrary CLR thread the unload arrives on -- so this drives one tick
        // synchronously via CaptureOnceForTesting. Without the fix the cache is never invalidated and the
        // final assertion fails.
        TEST_METHOD(unload_invalidation_clears_the_name_cache_on_the_next_capture)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            // Seed a resolved name, as a real capture tick's resolution pass would. .second is the length
            // INCLUDING the null terminator (see namecache.h).
            auto& cache = profiler.NameCacheForTesting();
            PreallocTypeName typeName{};
            wcscpy_s(typeName.first.data(), typeName.first.size(), L"StaleType");
            typeName.second = static_cast<ULONG>(wcslen(L"StaleType") + 1);
            PreallocMethodName methodName{};
            wcscpy_s(methodName.first.data(), methodName.first.size(), L"StaleMethod");
            methodName.second = static_cast<ULONG>(wcslen(L"StaleMethod") + 1);
            cache.insert(1, 100, 5, typeName, methodName);
            Assert::IsTrue(cache.has_fid(100), L"precondition: the seeded name must be cached");

            // A capture tick with NO pending invalidation must not drop the cache -- CP keeps its bounded
            // cache across ticks on purpose (which is exactly why a stale entry could otherwise live
            // forever without an unload hook).
            profiler.CaptureOnceForTesting();
            Assert::IsTrue(cache.has_fid(100), L"a normal tick must not clear the cache");

            // Signal an unload, then run one tick: the worker must observe the pending flag and clear.
            profiler.InvalidateNameCache();
            profiler.CaptureOnceForTesting();
            Assert::IsFalse(cache.has_fid(100),
                L"a capture tick after an unload signal must clear the stale name cache");

            profiler.Shutdown();
        }

        // Cluster B (lifecycle lock-scoping defect): Start()'s failure cleanup -- the _samplingActive reset
        // in its catch handler -- MUST run while _mtx_lifecycle is held, so it is serialized against a
        // concurrent Start() exactly like the happy path is.
        //
        // The regression this guards: the lifecycle lock_guard was originally declared INSIDE Start()'s try
        // block. When std::thread construction throws, C++ stack unwinding destroys that guard (releasing
        // _mtx_lifecycle) BEFORE control reaches the catch -- so the catch's _samplingActive.store(false)
        // ran with NO lifecycle lock held. A concurrent Start() on another thread could acquire the now-free
        // mutex in that window, spawn a worker and set _samplingActive=true, only for the failing thread's
        // late catch to clobber it back to false: worker alive, flag says inactive -- the exact
        // "armed-but-dead" outage the serialization exists to prevent, reached from the failure path.
        //
        // Deterministic model of the race (single foreground thread + a one-shot probe): force the next
        // Start() to throw during worker creation, and at the top of its catch handler -- on the failing
        // thread, before the flag reset -- run a PROBE on a different thread that tries to acquire
        // _mtx_lifecycle without blocking. If Start() held the lock across the whole try/catch (the fix) the
        // probe's try_lock fails; if the lock was released on unwind (the defect) it succeeds. The probe
        // must run on its own thread because std::mutex::try_lock on a mutex the caller already owns is UB,
        // and with the fix the foreground thread owns it here.
        TEST_METHOD(start_failure_cleanup_runs_while_the_lifecycle_lock_is_held)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            std::atomic<bool> hookRan{ false };
            std::atomic<bool> lifecycleWasAcquirableDuringCatch{ false };

            profiler.SetOnStartCatchForTesting([&]()
            {
                hookRan.store(true);
                // Probe from a DIFFERENT thread: a concurrent Start() would take _mtx_lifecycle exactly
                // this way. If Start()'s failure cleanup still holds the lock, this try_lock fails.
                std::thread probe([&]()
                {
                    lifecycleWasAcquirableDuringCatch.store(profiler.TryLockLifecycleForTesting());
                });
                probe.join();
            });

            // Drive Start() down its std::thread-creation failure path.
            profiler.SetFailWorkerCreationForTesting();
            const HRESULT failedStart = profiler.Start(ParkedIntervalMs);

            // Cluster B: the caught worker-thread creation failure must be SIGNALLED to the caller (was a
            // silent void return before), so managed StartLocked can unwind instead of arming a dead session.
            Assert::AreEqual(E_FAIL, failedStart, L"a caught worker-thread creation failure must return E_FAIL");

            Assert::IsTrue(hookRan.load(), L"the forced failure must actually drive Start()'s catch handler");
            Assert::IsFalse(lifecycleWasAcquirableDuringCatch.load(),
                L"Start()'s failure cleanup must run while _mtx_lifecycle is held -- a concurrent Start() "
                L"must not be able to acquire it mid-cleanup (Cluster B lock-scoping defect)");

            // The failed Start() must leave a clean, consistent state: no worker, sampling not armed.
            Assert::IsFalse(profiler.IsWorkerThreadRunning(), L"a failed Start must not leave a worker running");
            Assert::IsFalse(profiler.IsSamplingActiveForTesting(), L"a failed Start must reset the sampling flag");

            // And a subsequent Start() must still succeed -- the failure path left the object usable.
            const HRESULT recoveredStart = profiler.Start(ParkedIntervalMs);
            Assert::AreEqual(S_OK, recoveredStart, L"Start after a failed Start must succeed and report S_OK");
            Assert::IsTrue(profiler.IsWorkerThreadRunning(), L"Start after a failed Start must respawn the worker");
            Assert::IsTrue(profiler.IsSamplingActiveForTesting(), L"a successful Start must arm sampling");

            profiler.Shutdown();
            Assert::IsFalse(profiler.IsWorkerThreadRunning());
        }

        // Cluster E (per-session reservation must be all-or-nothing): the block in CaptureAllThreads that
        // sizes _capture/_resolved to ThreadCountForReservation and reserves every per-slot
        // FunctionIds/Frames buffer is guarded only by "_capture.size() != ThreadCountForReservation". If a
        // bad_alloc lands partway through -- realistic on a memory-capped container, and swallowed by the
        // sampling thread's outer catch(...) -- an in-place mutation could leave _capture already sized to
        // ThreadCountForReservation while _resolved (or a per-slot reserve) never finished, and the guard
        // would then treat that torn state as "done" on EVERY subsequent tick, forever: OOB reads/writes in
        // ResolveCapturedFrames, or a push_back reallocating on the CRT heap INSIDE the suspend window (a
        // heap-lock deadlock -> hung process).
        //
        // The fix builds the vectors in local temporaries and only swaps them into the members once every
        // allocation has succeeded, so a mid-block throw leaves the members untouched and the guard retries
        // cleanly. This test models that throw via the reservation failpoint (fired after the _capture temp
        // is built, before _resolved's -- exactly where an in-place version would tear), asserts BOTH member
        // vectors are left empty, then clears the failpoint and proves a later tick completes the full,
        // consistent reservation. Without the fix the failing tick leaves _capture sized to
        // ThreadCountForReservation, the guard never re-enters, and the first assertion fails.
        TEST_METHOD(a_partial_reservation_failure_leaves_buffers_untouched_and_a_later_tick_completes)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            Assert::IsFalse(profiler.HasSamplingResourcesForTesting(), L"precondition: no buffers before the first capture");

            // Force a bad_alloc partway through the per-session reservation block.
            profiler.SetReservationFailpointForTesting([]() { throw std::bad_alloc(); });

            // In production the sampling thread's outer catch(...) swallows this; there is no such catch on
            // the CaptureOnceForTesting path, so model it here.
            bool threw = false;
            try
            {
                profiler.CaptureOnceForTesting();
            }
            catch (const std::bad_alloc&)
            {
                threw = true;
            }
            Assert::IsTrue(threw, L"the failpoint must actually drive the reservation block down its throw path");

            // ALL-OR-NOTHING: a mid-block failure leaves BOTH capture vectors untouched (still empty), never
            // a torn _capture==ThreadCountForReservation / _resolved==0 state that the size()-guard would
            // freeze as "done".
            Assert::AreEqual(size_t{ 0 }, profiler.CaptureSlotCountForTesting(),
                L"_capture must be left untouched (empty) after a mid-block reservation failure");
            Assert::AreEqual(size_t{ 0 }, profiler.ResolvedSlotCountForTesting(),
                L"_resolved must be left untouched (empty) after a mid-block reservation failure");
            Assert::IsFalse(profiler.ReservationIsFullyCompleteForTesting(),
                L"a failed reservation must not report itself complete");

            // Clear the failpoint: because the members were left untouched, the size()-guard still fires on
            // the next tick and now completes the FULL reservation -- both vectors sized and every per-slot
            // buffer reserved, so no push_back can reallocate inside the suspend window.
            profiler.SetReservationFailpointForTesting(nullptr);
            profiler.CaptureOnceForTesting();

            Assert::IsTrue(profiler.ReservationIsFullyCompleteForTesting(),
                L"a subsequent tick must retry and complete the full, consistent reservation");

            profiler.Shutdown();
        }

        // Compaction must NOT be gated on sampling being active. Stop() parks the worker but does not
        // disarm the managed context, and the SEND-BACKOFF pause
        // (ContinuousProfilingService::TripBackoffAndScheduleProbeLocked) parks it without even calling
        // Disable() -- so retirements keep arriving during a pause. Before the fix this test guards, the
        // paused worker sat in an UNTIMED _cv_wake.wait() and could not reach the compaction deadline at
        // all, and the !_samplingActive guard `continue`d above the tick-path compaction call. The registry
        // therefore grew for the whole pause with nothing reclaiming it, which at backoff's 300s ceiling can
        // fill the table and fire the "INCORRECT TRACE CORRELATION" warning during a window in which no
        // sample is even being taken.
        //
        // WHAT THIS DRIVES, and why nothing cheaper does. _samplingActive is false from the Stop() below
        // until Shutdown(), so the tick-path compaction call is structurally unreachable for the whole test
        // -- the ONLY call site that can run here is the one at the !_samplingActive guard, reached only
        // because the paused wait now has a timeout. Calling CompactRetiredSpansForTesting() instead would
        // exercise CompactAgainst (already covered by RetiredSpanRegistryTest) while bypassing every line
        // the fix actually added. Verified by mutation: restoring the untimed wait() makes this fail.
        //
        // DELIBERATELY A REAL ~10-SECOND WALL-CLOCK TEST, in the same spirit as
        // a_segment_held_open_for_over_ten_seconds_stays_linked_the_whole_time. Reclamation needs TWO
        // consecutive clean passes (RetiredSpanRegistry::CompactAgainst) and the paused worker wakes once
        // per CompactionIntervalMs, so the earliest possible reclamation is 2 x 5000ms after the worker
        // enters its loop. Do NOT make this faster by shortening CompactionIntervalMs: that constant being
        // fixed and config-independent is a locked requirement of this design (coupling reclamation to a
        // tuning knob is what got the TTL approach rejected), and a test that mutates it is testing
        // something this code does not do.
        //
        // The ASSERTIONS are exact (reclaimed == 1, occupied == 0); only the WAIT is time-based, and it is
        // a poll-until-deadline rather than a fixed sleep, so a pass proves the worker really compacted and
        // the only way to reach the timeout is that it genuinely never did. The deadline is generous
        // (4x the theoretical minimum) so a loaded CI box cannot turn a correct implementation red.
        TEST_METHOD(a_paused_sampling_worker_still_compacts_the_retired_span_registry)
        {
            StubCorProfilerInfo4 corProfilerInfo; // must outlive the profiler -- Release is a no-op
            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, false);

            // A span id no TraceContextMap slot references, so it is eligible on the worker's first pass.
            // Start() bumps the trace-context generation, so no slot from any earlier test can hold it.
            const int64_t retiredSpanId = 0x5EE1E55C0FFEE001LL;

            profiler.Start(ParkedIntervalMs);
            Assert::IsTrue(profiler.IsWorkerThreadRunning(), L"the worker must be running to compact anything");

            // Retire BEFORE pausing, so the entry's insertion sequence predates both passes' horizons --
            // an entry inserted at or after a pass's horizon is deliberately skipped by that pass.
            profiler.RetireSpansForTesting({ retiredSpanId });
            Assert::AreEqual(size_t{ 1 }, profiler.RetiredSpanOccupiedCountForTesting(),
                L"the retirement must actually be in the registry before we pause");
            Assert::AreEqual(uint64_t{ 0 }, profiler.RetiredSpanReclaimedCountForTesting(),
                L"nothing can have been reclaimed yet -- no compaction deadline has passed");

            // Park the worker. This is the state the send-backoff pause leaves it in.
            profiler.Stop();
            Assert::IsFalse(profiler.IsSamplingActiveForTesting(), L"sampling must be paused for this test to mean anything");
            Assert::IsTrue(profiler.IsWorkerThreadRunning(), L"Stop() parks the worker; it must still be alive to compact");

            // Two passes at CompactionIntervalMs each is the floor; wait up to 4x that.
            const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(40000);
            while (profiler.RetiredSpanReclaimedCountForTesting() == 0
                && std::chrono::steady_clock::now() < deadline)
            {
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
            }

            Assert::AreEqual(uint64_t{ 1 }, profiler.RetiredSpanReclaimedCountForTesting(),
                L"a PAUSED worker must still run compaction passes and reclaim a proven-unreferenced entry");
            Assert::AreEqual(size_t{ 0 }, profiler.RetiredSpanOccupiedCountForTesting(),
                L"the reclaimed entry must be tombstoned, not merely counted");

            // Still paused throughout -- proves the reclamation came from the guard-site call and not from
            // a capture tick that somehow re-armed.
            Assert::IsFalse(profiler.IsSamplingActiveForTesting(), L"sampling must never have re-armed during the test");

            profiler.Shutdown();
            Assert::IsFalse(profiler.IsWorkerThreadRunning());
        }
    };
}}}
