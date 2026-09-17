/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <chrono>
#include <cstdint>
#include <stdexcept>
#include <thread>
#include <vector>

// Logger's StdLog/logging_available globals are defined in ContinuousProfilerLifecycleTest.cpp
// (LOGGER_DEFINE_STDLOG there); this TU only uses them, so it must NOT define the macro again (ODR).
#include "../ContinuousProfiler/ContinuousProfiler.h"
#include "StubCorProfilerInfo4.h"
#include "RichStubCorProfilerInfo4.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    // Cluster J / M1: real execution coverage for the risky capture mechanisms that the boundary stub
    // (StubCorProfilerInfo4, which E_NOTIMPL's the whole pipeline) otherwise leaves completely unexercised.
    //
    // Two seams do the reaching, both without a live CLR:
    //   - DriveStackFrameCallbackForTesting drives the real static per-frame callback directly, covering the
    //     128-frame overflow abort and the leaf-vs-root retention fix.
    //   - ProfileAllThreadsForTesting drives the real ProfileAllThreads sweep against RichStubCorProfilerInfo4
    //     (a stub that returns a live thread list and scripted DoStackSnapshot behavior), covering the
    //     failed-snapshot drop and the deliberately-truncated (too-deep) keep-and-count path.
    TEST_CLASS(ContinuousProfilerCaptureTest)
    {
    private:
        static void AssertFramesEqual(const std::vector<FunctionID>& expected, const std::vector<FunctionID>& actual, const wchar_t* what)
        {
            Assert::AreEqual(static_cast<int>(expected.size()), static_cast<int>(actual.size()), what);
            for (size_t i = 0; i < expected.size(); ++i)
            {
                Assert::IsTrue(expected[i] == actual[i], what);
            }
        }

    public:
        // The core of the leaf-vs-root retention fix the code's own comment calls out: a stack deeper than
        // MaxStackFramesSupported must abort the walk and KEEP the leaf-most (first-delivered) frames, and
        // report the deliberate truncation. The old wrap-to-begin behavior kept a rotated subset (as few as
        // one frame) and reported a normal, successful capture -- a regression reintroducing it would ship
        // undetected without this. Retaining exactly the FIRST MaxStackFramesSupported delivered frames, in
        // order, plus Truncated == true, is what pins the fix.
        TEST_METHOD(deep_stack_walk_truncates_and_keeps_the_leaf_most_frames)
        {
            ContinuousProfiler profiler;

            const size_t cap = ContinuousProfiler::StackFrameCallbackResultForTesting{}.MaxFramesSupported;

            // Deliver far more than the cap, leaf (id 1) first. The CLR would call the callback per frame
            // until it aborts.
            std::vector<FunctionID> delivered;
            for (size_t i = 1; i <= cap + 72; ++i) // + an arbitrary >0 tail so an off-by-one is visible
            {
                delivered.push_back(static_cast<FunctionID>(i));
            }

            const auto result = profiler.DriveStackFrameCallbackForTesting(/*managedTID*/ 42, delivered);

            Assert::IsTrue(result.Truncated, L"a stack deeper than the cap must be flagged truncated");
            Assert::AreEqual(static_cast<int>(cap), static_cast<int>(result.RetainedFunctionIds.size()),
                L"exactly MaxStackFramesSupported frames must be retained on overflow");

            // The retained frames must be the FIRST `cap` delivered (leaf-most) -- ids 1..cap -- NOT a
            // wrapped/rotated subset.
            std::vector<FunctionID> expected;
            for (size_t i = 1; i <= cap; ++i)
            {
                expected.push_back(static_cast<FunctionID>(i));
            }
            AssertFramesEqual(expected, result.RetainedFunctionIds, L"overflow must retain the leaf-most frames in order");
        }

        // Exactly MaxStackFramesSupported frames is the boundary: it must fill the buffer WITHOUT being
        // reported as truncated (the abort fires only when a frame arrives with the buffer already full).
        TEST_METHOD(exactly_max_frames_fills_the_buffer_without_truncating)
        {
            ContinuousProfiler profiler;
            const size_t cap = ContinuousProfiler::StackFrameCallbackResultForTesting{}.MaxFramesSupported;

            std::vector<FunctionID> delivered;
            for (size_t i = 1; i <= cap; ++i)
            {
                delivered.push_back(static_cast<FunctionID>(i));
            }

            const auto result = profiler.DriveStackFrameCallbackForTesting(7, delivered);

            Assert::IsFalse(result.Truncated, L"a stack exactly at the cap must not be flagged truncated");
            AssertFramesEqual(delivered, result.RetainedFunctionIds, L"all cap frames must be retained at the boundary");
        }

        // A normal (shallow) walk retains every delivered frame, leaf->root, and is not truncated.
        TEST_METHOD(shallow_stack_walk_retains_all_frames_in_order)
        {
            ContinuousProfiler profiler;

            const std::vector<FunctionID> delivered{ 10, 20, 30, 40, 50 };
            const auto result = profiler.DriveStackFrameCallbackForTesting(7, delivered);

            Assert::IsFalse(result.Truncated, L"a shallow walk must not truncate");
            AssertFramesEqual(delivered, result.RetainedFunctionIds, L"a shallow walk must retain every frame in order");
        }

        // StaticStackFrameCallback stamps the thread's trace context on its FIRST invocation (the one point
        // in the whole capture guaranteed suspended on every platform). A seeded context must come back
        // stamped on the result.
        TEST_METHOD(stack_walk_stamps_seeded_trace_context)
        {
            ContinuousProfiler profiler;

            const ThreadID tid = 42;
            profiler.SeedTraceContextForTesting(tid, 0x1111, 0x2222, 0x3333);

            const std::vector<FunctionID> delivered{ 1, 2, 3 };
            const auto result = profiler.DriveStackFrameCallbackForTesting(tid, delivered);

            Assert::AreEqual(static_cast<int64_t>(0x1111), result.StampedContext.TraceIdHigh, L"trace id high must be stamped");
            Assert::AreEqual(static_cast<int64_t>(0x2222), result.StampedContext.TraceIdLow, L"trace id low must be stamped");
            Assert::AreEqual(static_cast<int64_t>(0x3333), result.StampedContext.SpanId, L"span id must be stamped");
        }

        // A thread with no seeded context leaves the stamped context zeroed (a genuine "no correlation"
        // sample), never garbage.
        TEST_METHOD(stack_walk_without_seeded_context_leaves_zeroed_context)
        {
            ContinuousProfiler profiler;

            const std::vector<FunctionID> delivered{ 1, 2, 3 };
            const auto result = profiler.DriveStackFrameCallbackForTesting(/*unseeded*/ 999, delivered);

            Assert::AreEqual(static_cast<int64_t>(0), result.StampedContext.TraceIdHigh);
            Assert::AreEqual(static_cast<int64_t>(0), result.StampedContext.TraceIdLow);
            Assert::AreEqual(static_cast<int64_t>(0), result.StampedContext.SpanId);
        }

        // ProfileAllThreads must: drop a thread whose DoStackSnapshot fails (count it as a failed snapshot),
        // KEEP a thread whose too-deep walk was deliberately aborted (count it as truncated, not failed), and
        // retain a successful thread's frames leaf->root. This is the failure/truncation handling the
        // E_NOTIMPL boundary stub could never reach.
        TEST_METHOD(profile_all_threads_drops_failed_keeps_truncated_and_successful)
        {
            RichStubCorProfilerInfo4 corProfilerInfo;

            const size_t cap = ContinuousProfiler::ProfileAllThreadsResultForTesting{}.MaxFramesSupported;

            ThreadSnapshotScript ok{};
            ok.Thread = 100;
            ok.Frames = { 1, 2, 3 };
            ok.HrIfNoAbort = S_OK;

            ThreadSnapshotScript failed{};
            failed.Thread = 200;
            failed.FailImmediately = true;
            failed.HrIfNoAbort = E_FAIL;

            ThreadSnapshotScript tooDeep{};
            tooDeep.Thread = 300;
            for (size_t i = 0; i < cap + 72; ++i)
            {
                tooDeep.Frames.push_back(static_cast<FunctionID>(1001 + i)); // distinct id space so retention is checkable
            }
            tooDeep.HrIfNoAbort = S_OK; // unused: the callback aborts before completion

            corProfilerInfo.SetScripts({ ok, failed, tooDeep });

            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, /*isCoreClr*/ false);

            const auto result = profiler.ProfileAllThreadsForTesting();

            Assert::AreEqual(1, static_cast<int>(result.FailedSnapshotCount), L"the failed snapshot must be counted");
            Assert::AreEqual(1, static_cast<int>(result.TruncatedStackCount), L"the too-deep walk must be counted as truncated");
            Assert::AreEqual(0, static_cast<int>(result.OverflowCount), L"three threads fit the slots -- no round-robin overflow");
            Assert::AreEqual(0, static_cast<int>(result.ExceptionCount), L"no exception path is exercised here");
            Assert::AreEqual(2, static_cast<int>(result.CapturedCount), L"the failed thread is dropped; the other two are kept");

            // The successful thread is retained first (slot 0), the truncated thread second (the failed one
            // between them was dropped via continue).
            AssertFramesEqual(ok.Frames, result.RetainedFunctionIds.at(0), L"the successful thread retains its frames in order");

            Assert::AreEqual(static_cast<int>(cap), static_cast<int>(result.RetainedFunctionIds.at(1).size()),
                L"the truncated thread retains exactly the cap of leaf-most frames");
            std::vector<FunctionID> expectedTruncated;
            for (size_t i = 0; i < cap; ++i)
            {
                expectedTruncated.push_back(static_cast<FunctionID>(1001 + i));
            }
            AssertFramesEqual(expectedTruncated, result.RetainedFunctionIds.at(1), L"the truncated thread keeps the leaf-most frames");

            profiler.Shutdown();
        }

        // With every thread's snapshot succeeding, every thread is retained and no failure/truncation is
        // reported -- the positive control for the drop/keep test above.
        TEST_METHOD(profile_all_threads_retains_every_successful_thread)
        {
            RichStubCorProfilerInfo4 corProfilerInfo;

            ThreadSnapshotScript a{};
            a.Thread = 11;
            a.Frames = { 5, 6 };
            ThreadSnapshotScript b{};
            b.Thread = 22;
            b.Frames = { 7, 8, 9 };
            corProfilerInfo.SetScripts({ a, b });

            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, /*isCoreClr*/ false);

            const auto result = profiler.ProfileAllThreadsForTesting();

            Assert::AreEqual(2, static_cast<int>(result.CapturedCount), L"both threads must be captured");
            Assert::AreEqual(0, static_cast<int>(result.FailedSnapshotCount));
            Assert::AreEqual(0, static_cast<int>(result.TruncatedStackCount));
            AssertFramesEqual(a.Frames, result.RetainedFunctionIds.at(0), L"first thread frames");
            AssertFramesEqual(b.Frames, result.RetainedFunctionIds.at(1), L"second thread frames");

            profiler.Shutdown();
        }

        // GetThreadInfo's HRESULT must be checked: two threads whose Thread* -> OS id resolution both fail
        // must NOT collapse onto the same OsThreadId (the old unchecked code left OsThreadId at 0 for both,
        // colliding on _prevCpuSamples[0] and cross-contaminating each other's on-CPU baseline). Each failure
        // must mint its own distinct synthetic id, and neither must collide with a thread whose resolution
        // actually succeeded.
        TEST_METHOD(profile_all_threads_gives_distinct_synthetic_ids_to_threads_whose_get_thread_info_fails)
        {
            RichStubCorProfilerInfo4 corProfilerInfo;

            ThreadSnapshotScript resolvable{};
            resolvable.Thread = 100;
            resolvable.Frames = { 1 };

            ThreadSnapshotScript unresolvableA{};
            unresolvableA.Thread = 200;
            unresolvableA.Frames = { 2 };
            unresolvableA.FailGetThreadInfo = true;

            ThreadSnapshotScript unresolvableB{};
            unresolvableB.Thread = 300;
            unresolvableB.Frames = { 3 };
            unresolvableB.FailGetThreadInfo = true;

            corProfilerInfo.SetScripts({ resolvable, unresolvableA, unresolvableB });

            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, /*isCoreClr*/ false);

            const auto result = profiler.ProfileAllThreadsForTesting();

            Assert::AreEqual(3, static_cast<int>(result.CapturedCount), L"all three threads must be captured");
            const DWORD resolvedId = result.CapturedOsThreadIds.at(0);
            const DWORD failedIdA = result.CapturedOsThreadIds.at(1);
            const DWORD failedIdB = result.CapturedOsThreadIds.at(2);

            Assert::AreNotEqual(static_cast<uint32_t>(0), static_cast<uint32_t>(failedIdA),
                L"a failed GetThreadInfo must not leave OsThreadId at 0");
            Assert::AreNotEqual(static_cast<uint32_t>(0), static_cast<uint32_t>(failedIdB),
                L"a failed GetThreadInfo must not leave OsThreadId at 0");
            Assert::AreNotEqual(static_cast<uint32_t>(failedIdA), static_cast<uint32_t>(failedIdB),
                L"two different unresolved threads must not collide on the same synthetic id");
            Assert::AreNotEqual(static_cast<uint32_t>(resolvedId), static_cast<uint32_t>(failedIdA),
                L"a synthetic id must not collide with a genuinely resolved OS thread id");
            Assert::AreNotEqual(static_cast<uint32_t>(resolvedId), static_cast<uint32_t>(failedIdB),
                L"a synthetic id must not collide with a genuinely resolved OS thread id");

            profiler.Shutdown();
        }
    };

    // Cluster 1 (Round 6): the CoreCLR stop-the-world suspend/resume lifecycle. Its correctness invariant --
    // "every SuspendRuntime that actually stopped the runtime is matched by exactly one ResumeRuntime, even
    // when the capture in between fails or throws" -- had ZERO test coverage: the base stubs refuse
    // ICorProfilerInfo10, so the entire _isCoreClr branch in CaptureAllThreads (SuspendRuntime -> walk ->
    // ResumeRuntime, and its three failure branches) never executed under any C++ unit test. A refactor that
    // let an exception or a failed suspend skip or duplicate ResumeRuntime would hang the instrumented
    // process on the next tick, undetected.
    //
    // ScriptableSuspendResumeCorProfilerInfo hands out ICorProfilerInfo10 (so Init populates
    // _corProfilerInfo10 and the CoreCLR branch runs) with return-code-controllable SuspendRuntime/
    // ResumeRuntime and call counting. CaptureOnceForTesting drives exactly one synchronous capture on the
    // calling thread. NetSuspendDepth (== SuspendCallCount-on-success minus ResumeCallCount) landing back at
    // 0 is the balanced-pairing assertion each test shares.
    TEST_CLASS(ContinuousProfilerSuspendResumeTest)
    {
    public:
        // Positive control: a normal CoreCLR tick suspends once, resumes once, and ends balanced. Without
        // this, a regression that stopped calling either would be invisible to the failure tests below
        // (which only assert relative counts).
        TEST_METHOD(coreclr_capture_suspends_then_resumes_in_a_balanced_pair)
        {
            ScriptableSuspendResumeCorProfilerInfo corProfilerInfo; // must outlive the profiler

            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, /*isCoreClr*/ true);

            profiler.CaptureOnceForTesting();

            Assert::AreEqual(1, corProfilerInfo.SuspendCallCount, L"a CoreCLR tick must SuspendRuntime exactly once");
            Assert::AreEqual(1, corProfilerInfo.ResumeCallCount, L"a CoreCLR tick must ResumeRuntime exactly once");
            Assert::AreEqual(0, corProfilerInfo.NetSuspendDepth, L"suspend and resume must be balanced");

            profiler.Shutdown();
        }

        // Branch 1 (the critical one): an exception thrown INSIDE the suspend window must NOT escape
        // CaptureAllThreads AND must still let ResumeRuntime run. If the catch(...) were removed or narrowed,
        // or ResumeRuntime moved inside the try, the exception would skip ResumeRuntime and leave the runtime
        // permanently suspended -- a full-process hang on the next tick. This test fails (unbalanced /
        // resume-not-called, or an escaped exception) against any such regression.
        TEST_METHOD(coreclr_exception_in_suspend_window_still_resumes)
        {
            ScriptableSuspendResumeCorProfilerInfo corProfilerInfo; // must outlive the profiler

            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, /*isCoreClr*/ true);
            profiler.SetCaptureWindowFailpointForTesting([]() { throw std::runtime_error("forced fault inside suspend window (test seam)"); });

            // Must not throw out of the capture -- the catch(...) inside the suspend window swallows it.
            profiler.CaptureOnceForTesting();

            Assert::AreEqual(1, corProfilerInfo.SuspendCallCount, L"the runtime was suspended before the fault");
            Assert::AreEqual(1, corProfilerInfo.ResumeCallCount, L"ResumeRuntime MUST still run after an exception in the window");
            Assert::AreEqual(0, corProfilerInfo.NetSuspendDepth, L"an exception must not leave the runtime suspended");

            profiler.SetCaptureWindowFailpointForTesting(nullptr);
            profiler.Shutdown();
        }

        // Branch 2: a FAILED SuspendRuntime means the runtime never actually stopped, so ResumeRuntime must
        // NOT run -- resuming a suspend we do not own would corrupt the CLR's own suspend refcount. The tick
        // bails early, publishing nothing. A regression that resumed unconditionally would make ResumeCallCount
        // == 1 here and fail this test.
        TEST_METHOD(coreclr_failed_suspend_does_not_resume)
        {
            ScriptableSuspendResumeCorProfilerInfo corProfilerInfo; // must outlive the profiler
            corProfilerInfo.SuspendRuntimeResult = E_FAIL;

            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, /*isCoreClr*/ true);

            profiler.CaptureOnceForTesting();

            Assert::AreEqual(1, corProfilerInfo.SuspendCallCount, L"SuspendRuntime is attempted once");
            Assert::AreEqual(0, corProfilerInfo.ResumeCallCount, L"a failed suspend must NOT be followed by a resume");
            Assert::AreEqual(0, corProfilerInfo.NetSuspendDepth, L"no suspend was owned, so nothing to resume");

            profiler.Shutdown();
        }

        // Branch 3: a FAILED ResumeRuntime must be handled (warning path) without crashing or throwing, and
        // must still count as the single resume for the tick. The suspend succeeded, so the pair is still
        // attempted once each; the failure is logged, not fatal.
        TEST_METHOD(coreclr_failed_resume_is_handled_without_crashing)
        {
            ScriptableSuspendResumeCorProfilerInfo corProfilerInfo; // must outlive the profiler
            corProfilerInfo.ResumeRuntimeResult = E_FAIL;

            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, /*isCoreClr*/ true);

            // Must not throw or crash -- the failed resume only warns.
            profiler.CaptureOnceForTesting();

            Assert::AreEqual(1, corProfilerInfo.SuspendCallCount, L"the runtime was suspended");
            Assert::AreEqual(1, corProfilerInfo.ResumeCallCount, L"ResumeRuntime is attempted once even though it fails");

            profiler.Shutdown();
        }

        // Branch at the top of the CoreCLR path: isCoreClr==true but the runtime does not expose
        // ICorProfilerInfo10 (RefuseInfo10). CaptureAllThreads must take the "cannot suspend" early return --
        // neither SuspendRuntime nor ResumeRuntime is called -- and must not crash. Repeated ticks stay quiet
        // (the warning is once-only), which this drives by capturing twice.
        TEST_METHOD(coreclr_without_icorprofilerinfo10_neither_suspends_nor_resumes)
        {
            ScriptableSuspendResumeCorProfilerInfo corProfilerInfo; // must outlive the profiler
            corProfilerInfo.RefuseInfo10 = true;

            ContinuousProfiler profiler;
            profiler.Init(&corProfilerInfo, /*isCoreClr*/ true);

            profiler.CaptureOnceForTesting();
            profiler.CaptureOnceForTesting();

            Assert::AreEqual(0, corProfilerInfo.SuspendCallCount, L"a runtime without Info10 must never be suspended");
            Assert::AreEqual(0, corProfilerInfo.ResumeCallCount, L"a runtime without Info10 must never be resumed");

            profiler.Shutdown();
        }

        // THE regression test for the original production bug.
        //
        // Thread A pushes a trace context and then goes idle forever (its slot is never cleared, because
        // Transaction.End ran on a different thread). Thread B retires the span. Thread A's very NEXT
        // sample must read as no-link -- immediately, with no delay, no clock, and no second sampling tick.
        // The reverted TTL fix could only satisfy this after 3x the sampling interval had elapsed.
        TEST_METHOD(a_span_retired_from_another_thread_is_not_linked_on_the_very_next_read)
        {
            ContinuousProfiler profiler;
            const ThreadID idleThread = static_cast<ThreadID>(0x1000);
            const int64_t spanId = 0x1122334455667788LL;

            // Thread A's push. The slot belongs to idleThread, not to whatever thread runs this test --
            // exactly the cross-thread shape of the bug.
            profiler.SeedTraceContextForTesting(idleThread, 111, 222, spanId);

            // Baseline: the link is live before retirement.
            const auto before = profiler.DriveStackFrameCallbackForTesting(idleThread, { 1, 2, 3 });
            Assert::AreEqual(spanId, before.StampedContext.SpanId);
            Assert::AreEqual(static_cast<int64_t>(111), before.StampedContext.TraceIdHigh);

            // Thread B retires it -- a DIFFERENT OS thread from the one that pushed, and one that never
            // touches idleThread's slot.
            std::thread retirer([&profiler, spanId]() {
                profiler.RetireSpansForTesting({ spanId });
            });
            retirer.join();

            // The very next read: no link. No sleep, no tick, no clock anywhere in the decision.
            const auto after = profiler.DriveStackFrameCallbackForTesting(idleThread, { 1, 2, 3 });
            Assert::AreEqual(static_cast<int64_t>(0), after.StampedContext.SpanId);
            Assert::AreEqual(static_cast<int64_t>(0), after.StampedContext.TraceIdHigh);
            Assert::AreEqual(static_cast<int64_t>(0), after.StampedContext.TraceIdLow);

            // The frames themselves are unaffected -- only the link is dropped, matching the existing
            // zero-context behaviour.
            Assert::AreEqual(static_cast<size_t>(3), after.RetainedFunctionIds.size());
        }

        // Retiring one span must not disturb any other thread's live link.
        TEST_METHOD(retiring_one_span_leaves_other_threads_links_intact)
        {
            ContinuousProfiler profiler;
            const ThreadID threadA = static_cast<ThreadID>(0x1000);
            const ThreadID threadB = static_cast<ThreadID>(0x2000);

            profiler.SeedTraceContextForTesting(threadA, 1, 2, 111);
            profiler.SeedTraceContextForTesting(threadB, 3, 4, 222);

            profiler.RetireSpansForTesting({ 111 });

            const auto a = profiler.DriveStackFrameCallbackForTesting(threadA, { 1 });
            const auto b = profiler.DriveStackFrameCallbackForTesting(threadB, { 1 });

            Assert::AreEqual(static_cast<int64_t>(0), a.StampedContext.SpanId);
            Assert::AreEqual(static_cast<int64_t>(222), b.StampedContext.SpanId);
            Assert::AreEqual(static_cast<int64_t>(3), b.StampedContext.TraceIdHigh);
        }

        TEST_METHOD(retiring_a_span_nobody_pushed_changes_nothing)
        {
            ContinuousProfiler profiler;
            const ThreadID thread = static_cast<ThreadID>(0x1000);
            profiler.SeedTraceContextForTesting(thread, 1, 2, 111);

            // Over-retirement is expected and harmless: the managed side retires every materialized span
            // id, which is a superset of the pushed ones.
            profiler.RetireSpansForTesting({ 999, 888, 777 });

            const auto result = profiler.DriveStackFrameCallbackForTesting(thread, { 1 });
            Assert::AreEqual(static_cast<int64_t>(111), result.StampedContext.SpanId);
        }

        TEST_METHOD(retire_spans_tolerates_an_empty_batch_and_zero_ids)
        {
            ContinuousProfiler profiler;
            const ThreadID thread = static_cast<ThreadID>(0x1000);
            profiler.SeedTraceContextForTesting(thread, 1, 2, 111);

            profiler.RetireSpansForTesting({});
            profiler.RetireSpansForTesting({ 0, 0 });

            // A zero span id must never be retired -- it is the "no context" value every unset slot holds,
            // so treating it as a real key would make every unlinked slot read as retired.
            const auto result = profiler.DriveStackFrameCallbackForTesting(thread, { 1 });
            Assert::AreEqual(static_cast<int64_t>(111), result.StampedContext.SpanId);
        }

        // THE differentiator the TTL design structurally could not satisfy: a segment held open far
        // longer than any plausible fixed interval keeps its link for its entire duration, because nothing
        // in this design consults a clock. At the MINIMUM configurable sampling interval (1000ms) the
        // reverted TTL was 3000ms, so this exact scenario lost its link while still executing.
        //
        // Deliberately a real 11-second wall-clock test. It is the only way to prove the absence of a
        // duration-dependent cutoff empirically rather than by inspection. Do not shorten it.
        TEST_METHOD(a_segment_held_open_for_over_ten_seconds_stays_linked_the_whole_time)
        {
            ContinuousProfiler profiler;
            const ThreadID thread = static_cast<ThreadID>(0x1000);
            const int64_t spanId = 0x0BADC0DE0BADC0DELL;

            profiler.SeedTraceContextForTesting(thread, 111, 222, spanId);

            const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(11000);
            int reads = 0;
            while (std::chrono::steady_clock::now() < deadline)
            {
                const auto result = profiler.DriveStackFrameCallbackForTesting(thread, { 1 });
                Assert::AreEqual(spanId, result.StampedContext.SpanId);
                Assert::AreEqual(static_cast<int64_t>(111), result.StampedContext.TraceIdHigh);
                ++reads;
                std::this_thread::sleep_for(std::chrono::milliseconds(500));
            }

            Assert::IsTrue(reads >= 20);

            // And it still ends on an event, not a timer: retirement is what drops the link, whenever it
            // finally arrives.
            profiler.RetireSpansForTesting({ spanId });
            const auto afterRetire = profiler.DriveStackFrameCallbackForTesting(thread, { 1 });
            Assert::AreEqual(static_cast<int64_t>(0), afterRetire.StampedContext.SpanId);
        }

        // Compaction must not resurrect a link: an entry kept because a live slot references it keeps
        // rejecting that slot's samples across as many passes as it takes.
        TEST_METHOD(a_retired_span_stays_rejected_across_compaction_passes)
        {
            ContinuousProfiler profiler;
            const ThreadID idleThread = static_cast<ThreadID>(0x1000);
            const int64_t spanId = 555;

            profiler.SeedTraceContextForTesting(idleThread, 1, 2, spanId);
            profiler.RetireSpansForTesting({ spanId });

            for (int pass = 0; pass < 3; ++pass)
            {
                profiler.CompactRetiredSpansForTesting();
                const auto result = profiler.DriveStackFrameCallbackForTesting(idleThread, { 1 });
                Assert::AreEqual(static_cast<int64_t>(0), result.StampedContext.SpanId);
            }
        }

        // A null batch with a positive count is the one input RetireSpans rejects outright: it is a managed
        // marshalling bug, not a transaction with nothing to retire, and silently succeeding would hide it.
        TEST_METHOD(retire_spans_rejects_a_null_batch_with_a_positive_count)
        {
            ContinuousProfiler profiler;

            Assert::AreEqual(E_INVALIDARG, profiler.RetireSpans(nullptr, 5));

            // A null batch with no count is simply an empty transaction, which is normal and must succeed.
            Assert::AreEqual(S_OK, profiler.RetireSpans(nullptr, 0));
            Assert::AreEqual(S_OK, profiler.RetireSpans(nullptr, -1));
        }

        // Overflow must degrade, not fail: retirements past the registry's capacity are DROPPED (warned
        // about and counted, never silent), every retirement that did land keeps rejecting its span, and no
        // unrelated thread's live link is disturbed. Drives the edge-triggered overflow warning too --
        // including the re-arm branch, by retiring again with no new drops.
        TEST_METHOD(retiring_far_more_spans_than_the_registry_holds_degrades_without_disturbing_live_links)
        {
            ContinuousProfiler profiler;
            const ThreadID thread = static_cast<ThreadID>(0x1000);
            const int64_t liveSpan = 0x5A5A5A5A5A5A5A5ALL;

            profiler.SeedTraceContextForTesting(thread, 111, 222, liveSpan);

            // Comfortably past the registry's 65536 slots, so the probe budget is genuinely exhausted.
            std::vector<int64_t> spanIds;
            spanIds.reserve(150000);
            for (int64_t i = 1; i <= 150000; ++i)
            {
                spanIds.push_back(i);
            }
            profiler.RetireSpansForTesting(spanIds);

            // The live link is untouched: overflow drops RETIREMENTS, it never invents one.
            const auto stillLinked = profiler.DriveStackFrameCallbackForTesting(thread, { 1 });
            Assert::AreEqual(liveSpan, stillLinked.StampedContext.SpanId);

            // A span that did land is still rejected, and a second batch with no new drops re-arms the
            // warning latch rather than crashing or looping.
            profiler.SeedTraceContextForTesting(thread, 111, 222, 1);
            profiler.RetireSpansForTesting({ 1 });
            const auto rejected = profiler.DriveStackFrameCallbackForTesting(thread, { 1 });
            Assert::AreEqual(static_cast<int64_t>(0), rejected.StampedContext.SpanId);
        }
    };
}}}
