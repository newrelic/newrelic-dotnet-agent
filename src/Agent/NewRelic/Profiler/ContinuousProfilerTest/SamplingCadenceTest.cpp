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
    // Tests for the sampler's inter-tick wait arithmetic (ContinuousProfiler::NextWaitMs), which subtracts
    // the previous capture's wall-clock cost from the configured interval.
    //
    // What this protects: the managed side attributes exactly `period` nanoseconds of time to every sample,
    // with period = the configured interval, so the real tick-to-tick period has to BE that interval.
    // Waiting the full interval after each capture instead makes it interval + capture cost and silently
    // under-reports every profile's totals. Pure arithmetic -- no worker thread or live CLR needed.
    TEST_CLASS(SamplingCadenceTest)
    {
    public:

        // No preceding capture (first tick of a session, or one that threw): wait the whole interval.
        TEST_METHOD(no_previous_capture_waits_the_full_interval)
        {
            Assert::AreEqual(1000u, ContinuousProfiler::NextWaitMs(1000, 0));
        }

        // The case this exists for: a 150ms capture on a 1s interval waits 850ms, so capture start to
        // capture start stays at the configured 1000ms.
        TEST_METHOD(capture_cost_is_subtracted_from_the_interval)
        {
            Assert::AreEqual(850u, ContinuousProfiler::NextWaitMs(1000, 150));
        }

        // Half the interval is the most that can be compensated away -- see NextWaitMs's floor.
        TEST_METHOD(compensation_stops_at_half_the_interval)
        {
            Assert::AreEqual(500u, ContinuousProfiler::NextWaitMs(1000, 500));
            Assert::AreEqual(500u, ContinuousProfiler::NextWaitMs(1000, 900));
        }

        // A capture that overran its interval (or grossly overran it) still leaves the app half the
        // interval of idle time rather than being suspended back-to-back.
        TEST_METHOD(overrunning_capture_never_drives_the_wait_to_zero)
        {
            Assert::AreEqual(500u, ContinuousProfiler::NextWaitMs(1000, 1000));
            Assert::AreEqual(500u, ContinuousProfiler::NextWaitMs(1000, 60000));
            Assert::AreEqual(30000u, ContinuousProfiler::NextWaitMs(60000, 90000));
        }

        // Defensive: a nonsensical negative cost is treated as "no measurement", not as extra wait time.
        TEST_METHOD(negative_capture_cost_is_ignored)
        {
            Assert::AreEqual(1000u, ContinuousProfiler::NextWaitMs(1000, -250));
        }

        // Interval floors (MinIntervalMs, and the managed clamp's own bounds) compensate the same way.
        TEST_METHOD(short_and_long_intervals_compensate_the_same_way)
        {
            Assert::AreEqual(60u, ContinuousProfiler::NextWaitMs(100, 40));
            Assert::AreEqual(50u, ContinuousProfiler::NextWaitMs(100, 80));
            Assert::AreEqual(59850u, ContinuousProfiler::NextWaitMs(60000, 150));
        }
    };
}}}
