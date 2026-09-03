/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <cstdint>

// The STDLOG/logging globals ContinuousProfiler.h pulls in via Logger.h are defined in exactly ONE TU
// per test binary (ContinuousProfilerLifecycleTest.cpp owns LOGGER_DEFINE_STDLOG). This TU only
// references them, so it must NOT define the macro again.
#include "../ContinuousProfiler/ContinuousProfiler.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    // Tests for the per-sample on-CPU classification (ContinuousProfiler::IsOnCpu), which compares this
    // tick's cumulative CPU reading for an OS thread id against the previous tick's.
    //
    // The hazard being guarded: the previous-tick map is keyed by OS thread id, and the OS recycles a tid
    // shortly after its thread exits, so a comparison on CPU totals alone silently compares two different
    // threads. Each reading therefore carries the thread's creation stamp, and a stamp mismatch means
    // "no baseline" rather than a bogus delta. Pure arithmetic -- no live CLR needed.
    TEST_CLASS(OnCpuClassificationTest)
    {
    private:
        static ContinuousProfiler::ThreadCpuSample Sample(int64_t cpuMicros, uint64_t startStamp)
        {
            ContinuousProfiler::ThreadCpuSample sample;
            sample.CpuMicros = cpuMicros;
            sample.StartStamp = startStamp;
            return sample;
        }

    public:

        // The normal case: same thread, CPU total grew since the last tick -> on-CPU.
        TEST_METHOD(growing_cpu_on_the_same_thread_is_on_cpu)
        {
            Assert::IsTrue(ContinuousProfiler::IsOnCpu(Sample(1000, 42), Sample(1500, 42)));
        }

        // Same thread, no CPU consumed between ticks -> off-CPU (blocked/waiting).
        TEST_METHOD(unchanged_cpu_on_the_same_thread_is_off_cpu)
        {
            Assert::IsFalse(ContinuousProfiler::IsOnCpu(Sample(1000, 42), Sample(1000, 42)));
        }

        // The reuse case: a NEW thread on a recycled tid must not be judged against the dead thread's
        // total, in either direction. A different creation stamp means there is no usable baseline.
        TEST_METHOD(reused_thread_id_is_never_judged_against_the_previous_holder)
        {
            // New thread's own total happens to exceed the dead thread's -> must NOT report on-CPU.
            Assert::IsFalse(ContinuousProfiler::IsOnCpu(Sample(1000, 42), Sample(9000, 43)));

            // New thread's total is below the dead thread's -> also just "no baseline", not a negative delta.
            Assert::IsFalse(ContinuousProfiler::IsOnCpu(Sample(9000, 42), Sample(1000, 43)));
        }

        // An unreadable reading (thread gone, or the OS call failed) is never on-CPU, on either side of
        // the comparison.
        TEST_METHOD(unavailable_cpu_reading_is_off_cpu)
        {
            Assert::IsFalse(ContinuousProfiler::IsOnCpu(Sample(1000, 42), Sample(-1, 42)));
            Assert::IsFalse(ContinuousProfiler::IsOnCpu(Sample(-1, 42), Sample(1500, 42)));
        }

        // A default-constructed reading is the "no data" state: unavailable CPU and no stamp.
        TEST_METHOD(default_sample_is_unavailable)
        {
            ContinuousProfiler::ThreadCpuSample sample;
            Assert::AreEqual(static_cast<int64_t>(-1), sample.CpuMicros);
            Assert::AreEqual(static_cast<uint64_t>(0), sample.StartStamp);
        }
    };
}}}
