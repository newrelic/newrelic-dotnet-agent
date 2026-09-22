/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <cstdint>
#include <set>
#include <vector>

// The STDLOG/logging globals ContinuousProfiler.h pulls in via Logger.h are defined in exactly ONE TU
// per test binary (ContinuousProfilerLifecycleTest.cpp owns LOGGER_DEFINE_STDLOG). This TU only
// references them, so it must NOT define the macro again.
#include "../ContinuousProfiler/ContinuousProfiler.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    // Tests for the round-robin thread-capture window planner (ContinuousProfiler::PlanCaptureWindow).
    //
    // The hazard being guarded: a process with more live managed threads than the fixed capture-slot
    // count used to drop the SAME tail of threads on every tick, because EnumThreads returns stable CLR
    // ThreadStore order. That turned overflow into a permanent sampling blind spot. PlanCaptureWindow
    // advances the start offset by the visited count each tick so the drop rotates -- every thread is
    // sampled over successive ticks. These tests drive the pure arithmetic directly; no live CLR needed.
    TEST_CLASS(ThreadCaptureRotationTest)
    {
    private:
        // Simulate one tick's visited thread-list indices given a rotation offset, and report the offset
        // to carry into the next tick. Mirrors exactly what ProfileAllThreads does: visit
        // (start + i) % threadCount for i in [0, visitCount).
        static std::vector<size_t> VisitedIndices(size_t threadCount, size_t capacity, size_t& offset)
        {
            const auto window = ContinuousProfiler::PlanCaptureWindow(threadCount, capacity, offset);
            std::vector<size_t> indices;
            indices.reserve(window.visitCount);
            for (size_t i = 0; i < window.visitCount; ++i)
            {
                indices.push_back((window.start + i) % threadCount);
            }
            offset = window.nextOffset;
            return indices;
        }

    public:
        // When live threads fit within the slot capacity, every thread is visited and the start offset
        // does NOT drift across ticks (advancing by a full cycle is a no-op mod threadCount) -- an
        // under-capacity process must see zero rotation churn.
        TEST_METHOD(within_capacity_visits_all_threads_without_rotating)
        {
            const size_t threadCount = 8;
            const size_t capacity = 100;
            size_t offset = 0;

            for (int tick = 0; tick < 5; ++tick)
            {
                const auto visited = VisitedIndices(threadCount, capacity, offset);
                Assert::AreEqual(threadCount, visited.size(), L"every thread must be visited when they fit");
                Assert::AreEqual(static_cast<size_t>(0), visited.front(), L"start must stay at 0 with no overflow");
                // Confirms full, in-order coverage each tick.
                for (size_t i = 0; i < threadCount; ++i)
                {
                    Assert::AreEqual(i, visited[i], L"within-capacity ticks visit indices 0..N-1 in order");
                }
            }
        }

        // capacity == threadCount is the boundary of the within-capacity case: still no overflow, no
        // rotation, full coverage.
        TEST_METHOD(capacity_equal_to_thread_count_visits_all)
        {
            const size_t threadCount = 100;
            size_t offset = 0;
            const auto visited = VisitedIndices(threadCount, /*capacity*/ 100, offset);
            Assert::AreEqual(threadCount, visited.size());
            Assert::AreEqual(threadCount, offset, L"offset advances by a full cycle == threadCount");
        }

        // The core anti-regression: with more threads than slots, consecutive ticks must visit DIFFERENT
        // windows. The old from-index-0 logic visited the same 0..capacity-1 set every tick (permanent
        // blind spot); rotation must break that.
        TEST_METHOD(overflow_rotates_window_across_consecutive_ticks)
        {
            const size_t threadCount = 250;
            const size_t capacity = 100;
            size_t offset = 0;

            const auto tick0 = VisitedIndices(threadCount, capacity, offset);
            const auto tick1 = VisitedIndices(threadCount, capacity, offset);
            const auto tick2 = VisitedIndices(threadCount, capacity, offset);

            Assert::AreEqual(capacity, tick0.size(), L"an overflow tick visits exactly capacity threads");
            Assert::AreEqual(capacity, tick1.size());
            Assert::AreEqual(capacity, tick2.size());

            // Different starting index each tick -> a different dropped subset each tick.
            Assert::AreEqual(static_cast<size_t>(0), tick0.front());
            Assert::AreEqual(static_cast<size_t>(100), tick1.front());
            Assert::AreEqual(static_cast<size_t>(200), tick2.front());

            const std::set<size_t> set0(tick0.begin(), tick0.end());
            const std::set<size_t> set1(tick1.begin(), tick1.end());
            Assert::IsFalse(set0 == set1, L"consecutive overflow ticks must not sample the identical thread set");
        }

        // Fairness: over enough ticks every thread is sampled at least once -- no permanent blind spot.
        // threadCount not a multiple of capacity exercises the wrap-around path.
        TEST_METHOD(overflow_covers_every_thread_over_successive_ticks)
        {
            const size_t threadCount = 250;
            const size_t capacity = 100;
            size_t offset = 0;

            std::set<size_t> covered;
            // ceil(250/100) = 3 ticks are sufficient given the contiguous advancing window; run a few
            // extra to be robust to the wrap alignment.
            for (int tick = 0; tick < 5 && covered.size() < threadCount; ++tick)
            {
                const auto visited = VisitedIndices(threadCount, capacity, offset);
                covered.insert(visited.begin(), visited.end());
            }

            Assert::AreEqual(threadCount, covered.size(), L"every thread index must be sampled over successive ticks");
        }

        // Every visited index this tick must be in range and unique -- the wrap-around must never revisit
        // a thread within a single tick nor produce an out-of-range index.
        TEST_METHOD(a_single_tick_visits_unique_in_range_indices)
        {
            const size_t threadCount = 137; // prime-ish, forces wrap misalignment across ticks
            const size_t capacity = 100;
            size_t offset = 0;

            for (int tick = 0; tick < 10; ++tick)
            {
                const auto visited = VisitedIndices(threadCount, capacity, offset);
                std::set<size_t> unique(visited.begin(), visited.end());
                Assert::AreEqual(visited.size(), unique.size(), L"no thread visited twice in one tick");
                for (const auto idx : visited)
                {
                    Assert::IsTrue(idx < threadCount, L"visited index must be within the thread list");
                }
            }
        }

        // A tick that sees no live threads is a safe no-op: nothing visited, offset unchanged (so a
        // transient zero-thread enumeration can't perturb the rotation).
        TEST_METHOD(zero_threads_is_a_safe_noop)
        {
            size_t offset = 42;
            const auto window = ContinuousProfiler::PlanCaptureWindow(/*threadCount*/ 0, /*capacity*/ 100, offset);
            Assert::AreEqual(static_cast<size_t>(0), window.visitCount, L"no threads to visit");
            Assert::AreEqual(static_cast<size_t>(42), window.nextOffset, L"offset must be preserved on an empty tick");
        }

        // A huge accumulated offset (as would build up over a long-lived process) still maps to a valid
        // in-range start via the modulo -- unbounded growth is harmless by design.
        TEST_METHOD(large_accumulated_offset_stays_in_range)
        {
            const size_t threadCount = 137;
            const size_t capacity = 100;
            // Near SIZE_MAX so the modulo reduction is exercised close to the accumulator's wrap point;
            // width-portable (x86 size_t is 32-bit) unlike a fixed large shift.
            size_t offset = SIZE_MAX - 5;

            const auto window = ContinuousProfiler::PlanCaptureWindow(threadCount, capacity, offset);
            Assert::IsTrue(window.start < threadCount, L"start must be reduced into range regardless of offset size");
            Assert::AreEqual(capacity, window.visitCount);
        }
    };
}}}
