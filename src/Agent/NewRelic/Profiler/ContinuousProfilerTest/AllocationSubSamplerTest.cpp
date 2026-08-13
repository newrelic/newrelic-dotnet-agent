// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include "CppUnitTest.h"
#include "../ContinuousProfiler/AllocationSubSampler.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace NewRelic::Profiler::ContinuousProfiler;

TEST_CLASS(AllocationSubSamplerTest)
{
public:
    TEST_METHOD(ShouldSample_NeverExceedsTargetPerCycle_UnderHeavyLoad)
    {
        AllocationSubSampler sampler(/*targetPerCycle*/ 10, /*cycleSeconds*/ 60);
        int sampled = 0;
        for (int i = 0; i < 100000; ++i)
        {
            if (sampler.ShouldSample()) { ++sampled; }
        }
        // A single unbounded cycle (no time advance) must never sample dramatically more than the
        // target, even under a huge burst of ticks.
        Assert::IsTrue(sampled <= 10 * 3, L"sampled count grossly exceeded target under burst");
    }

    TEST_METHOD(ShouldSample_ZeroTarget_NeverSamples)
    {
        AllocationSubSampler sampler(0, 60);
        for (int i = 0; i < 1000; ++i)
        {
            Assert::IsFalse(sampler.ShouldSample());
        }
    }

    TEST_METHOD(ShouldSample_CycleRollover_ResetsCountersAndUsesHistoricalTicks)
    {
        // Test multi-cycle behavior: counters must reset at boundaries.
        // Key proof: cycle 1 exhausts its sampling cap, then cycle 2 resumes sampling
        // immediately after rollover (proving _sampledThisCycle was actually reset to 0).
        const uint32_t target = 5;

        // Capture the current time BEFORE construction so the fake clock is synchronized
        // with _cycleStart set in the constructor.
        auto now = std::chrono::steady_clock::now();
        AllocationSubSampler sampler(target, 1);  // 1-second cycles

        // Set fake clock to the captured time (approximately when _cycleStart was set).
        // This ensures _cycleStart ≈ fake_now, so we can control cycle boundaries.
        sampler.SetClockFunction([&now]() { return now; });

        // === Cycle 1: Run enough ticks to exhaust the sampling cap ===
        // With startup-paced estimate (target * 1000 = 5000), we expect approximately
        // (target / 5000) * 50000 = 50 samples from 50000 ticks.
        // This guarantees we hit the cap of 5 and trigger the "already sampled >= target" early-exit.
        int sampledCycle1 = 0;
        for (int i = 0; i < 50000; ++i)
        {
            if (sampler.ShouldSample()) { ++sampledCycle1; }
        }

        // Prove cycle 1 actually hit its cap (sampling stopped due to _sampledThisCycle >= target).
        Assert::AreEqual(target, static_cast<uint32_t>(sampledCycle1),
                         L"cycle 1 must sample exactly to target, then stop due to cap");

        // === Advance clock past cycle boundary (>= 1 second) ===
        now += std::chrono::seconds(2);

        // === Cycle 2: Verify counters were reset by immediately sampling again ===
        // With _lastCycleTicks = 50,000 now known from cycle 1, cycle 2's odds are (5 - 0) / 50,000 = 0.0001.
        // Run enough ticks to guarantee at least one sample: with expected value ~2.5 for 50,000 ticks,
        // we have > 99% confidence of at least one sample (proving counters reset).
        // If _sampledThisCycle wasn't reset, all calls would return false (stuck at >= target from carry-over).
        int sampledCycle2 = 0;
        for (int i = 0; i < 50000; ++i)
        {
            if (sampler.ShouldSample()) { ++sampledCycle2; }
            if (sampledCycle2 > 0) break;  // Exit as soon as we prove sampling works again
        }

        Assert::IsTrue(sampledCycle2 > 0,
                       L"cycle 2 must resume sampling immediately after rollover; if _sampledThisCycle wasn't reset, this would be 0");
    }
};
