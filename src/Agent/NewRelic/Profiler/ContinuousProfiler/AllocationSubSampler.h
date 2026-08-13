// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <atomic>
#include <chrono>
#include <cstdint>
#include <functional>
#include <random>

// Ports OTel's cycle-based allocation-tick sub-sampler: instead of true reservoir sampling (which
// needs to know N in advance), each tick rolls a "die" whose odds are derived from how many samples
// are still wanted this cycle vs. how many ticks the PREVIOUS cycle saw. Startup pacing (the first
// few cycles) spaces samples out rather than letting them cluster at the very start of cycle 1.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    class AllocationSubSampler
    {
    private:
        using Clock = std::chrono::steady_clock;

    public:
        AllocationSubSampler(uint32_t targetPerCycle, uint32_t cycleSeconds) noexcept
            : _targetPerCycle(targetPerCycle), _cycleSeconds(cycleSeconds),
              _cycleStart(Clock::now()), _rng(InitializeRng()),
              _clockFunc([]() { return Clock::now(); })
        {
        }

        // Allows tests to inject a custom clock function for deterministic cycle-boundary testing.
        void SetClockFunction(std::function<std::chrono::steady_clock::time_point()> clockFunc) noexcept
        {
            _clockFunc = clockFunc;
        }

        // Call once per AllocationTick, BEFORE any stack walk. Returns true if this tick should be
        // captured. Never throws, never blocks, never allocates on the heap (uses a stack-local
        // uniform_real_distribution -- no dynamic state beyond the member RNG).
        bool ShouldSample() noexcept
        {
            MaybeRollCycle();

            ++_ticksThisCycle;

            if (_targetPerCycle == 0)
            {
                return false;
            }

            if (_sampledThisCycle >= _targetPerCycle)
            {
                return false;
            }

            // Odds based on the LAST cycle's total tick count (0 on the very first cycle -> pace
            // conservatively instead of sampling everything until the first real estimate exists).
            const uint64_t estimatedTotal = _lastCycleTicks > 0 ? _lastCycleTicks : StartupPacedEstimate();
            const uint32_t remaining = _targetPerCycle - _sampledThisCycle;
            const double odds = estimatedTotal > 0
                ? (static_cast<double>(remaining) / static_cast<double>(estimatedTotal))
                : 1.0;

            std::uniform_real_distribution<double> dist(0.0, 1.0);
            if (dist(_rng) <= odds)
            {
                ++_sampledThisCycle;
                return true;
            }
            return false;
        }

    private:
        // Safely initialize the RNG, falling back to a deterministic seed if std::random_device
        // throws (which can happen in restricted/sandboxed environments).
        static std::mt19937 InitializeRng() noexcept
        {
            try
            {
                std::random_device rd;
                return std::mt19937(rd());
            }
            catch (...)
            {
                // Fallback: use steady_clock ticks as seed for deterministic initialization.
                // This ensures the constructor is truly noexcept and never crashes the app,
                // even in entropy-starved or sandboxed environments.
                return std::mt19937(static_cast<uint32_t>(
                    std::chrono::steady_clock::now().time_since_epoch().count() & 0xFFFFFFFFULL));
            }
        }

        // First few cycles have no _lastCycleTicks history yet; assume a large-ish tick volume so we
        // don't accidentally sample everything in cycle 1 (matches OTel's startup-pacing intent).
        uint64_t StartupPacedEstimate() const noexcept
        {
            return static_cast<uint64_t>(_targetPerCycle) * 1000;
        }

        void MaybeRollCycle() noexcept
        {
            const auto now = _clockFunc();
            if (std::chrono::duration_cast<std::chrono::seconds>(now - _cycleStart).count() >= _cycleSeconds)
            {
                _lastCycleTicks = _ticksThisCycle;
                _ticksThisCycle = 0;
                _sampledThisCycle = 0;
                _cycleStart = now;
            }
        }

        uint32_t _targetPerCycle;
        uint32_t _cycleSeconds;
        Clock::time_point _cycleStart;
        uint64_t _ticksThisCycle{ 0 };
        uint64_t _lastCycleTicks{ 0 };
        uint32_t _sampledThisCycle{ 0 };
        std::mt19937 _rng;
        std::function<Clock::time_point()> _clockFunc;
    };
}}}
