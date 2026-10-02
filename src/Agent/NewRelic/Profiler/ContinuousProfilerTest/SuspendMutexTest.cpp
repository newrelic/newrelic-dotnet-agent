/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <atomic>
#include <cstdint>
#include <mutex>
#include <thread>
#include <vector>

#include "../ContinuousProfiler/SuspendMutex.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

// SuspendMutex is the process-wide lock the ThreadProfiler and ContinuousProfiler both hold across their
// SuspendRuntime -> DoStackSnapshot -> ResumeRuntime cycle so the two never suspend the runtime at once
// (see ContinuousProfiler.h CaptureAllThreads / ReleaseSamplingResources and ThreadProfiler.h, both of
// which take std::lock_guard<SuspendMutex>(SuspendMutex::Shared())). It carried no test coverage at all;
// these cover the singleton identity, basic acquire/release, try_lock semantics across threads, and the
// mutual-exclusion contract under concurrent contention that mirrors the two-profiler serialization.
namespace NewRelic { namespace Profiler
{
    TEST_CLASS(SuspendMutexTest)
    {
    public:

        // Shared() is a process-wide singleton: every call returns the SAME instance. That identity is the
        // whole point -- it is what lets the ThreadProfiler and ContinuousProfiler serialize against each
        // other, since both lock this one object.
        TEST_METHOD(shared_returns_the_same_instance)
        {
            Assert::IsTrue(&SuspendMutex::Shared() == &SuspendMutex::Shared());
        }

        // Basic acquire/release through a lock_guard (the way both profilers use it): after the guard leaves
        // scope the mutex is free, so the SAME thread can re-lock it without deadlocking. Reaching the second
        // guard's body at all proves the first guard released. (std::mutex is non-recursive, so this would
        // deadlock -- never return -- if the first lock had not been released.)
        TEST_METHOD(lock_guard_releases_on_scope_exit)
        {
            SuspendMutex m;
            {
                std::lock_guard<SuspendMutex> guard(m);
            }
            std::lock_guard<SuspendMutex> guard2(m);
            Assert::IsTrue(true);
        }

        // try_lock reports contention correctly: while one thread holds the mutex, a DIFFERENT thread's
        // try_lock must fail (return false) rather than block; once the holder releases, a try_lock from
        // another thread must succeed. The cross-thread framing matters -- std::mutex is non-recursive, so
        // a same-thread try_lock on an already-held mutex is undefined behavior and is deliberately avoided.
        TEST_METHOD(try_lock_fails_while_held_by_another_thread_then_succeeds_after_release)
        {
            SuspendMutex m;
            m.lock();

            std::atomic<bool> acquiredWhileHeld{ false };
            std::thread contender([&m, &acquiredWhileHeld]
            {
                if (m.try_lock())
                {
                    acquiredWhileHeld.store(true); // must NOT happen: the mutex is held by the main thread
                    m.unlock();
                }
            });
            contender.join();
            Assert::IsFalse(acquiredWhileHeld.load()); // the contending thread was correctly refused

            m.unlock();

            std::atomic<bool> acquiredAfterRelease{ false };
            std::thread taker([&m, &acquiredAfterRelease]
            {
                if (m.try_lock())
                {
                    acquiredAfterRelease.store(true);
                    m.unlock();
                }
            });
            taker.join();
            Assert::IsTrue(acquiredAfterRelease.load()); // free now -> try_lock succeeds
        }

        // The load-bearing contract: the two profilers must never be inside their suspend/stack-walk critical
        // section simultaneously. Model both as threads that repeatedly take the mutex around a critical
        // section guarded by an occupancy counter; if the lock ever admitted two holders at once, the counter
        // would be non-zero on entry and we flag it. A plain counter incremented only under the lock cross-
        // checks the same guarantee: a broken lock would race it and lose updates, so the exact total proves
        // strict serialization. Barrier-aligned start and a high iteration count exercise real contention
        // rather than relying on a timing window.
        TEST_METHOD(mutual_exclusion_holds_under_concurrent_contention)
        {
            constexpr int threadCount = 4;
            constexpr int iterations = 200000;

            SuspendMutex m;
            std::atomic<int> occupancy{ 0 };
            std::atomic<bool> overlap{ false };
            int64_t counter = 0; // touched ONLY under the mutex; a lost update would prove a broken lock

            std::atomic<int> ready{ 0 };
            std::atomic<bool> go{ false };

            std::vector<std::thread> threads;
            for (int t = 0; t < threadCount; ++t)
            {
                threads.emplace_back([&m, &occupancy, &overlap, &counter, &ready, &go, iterations]
                {
                    ready.fetch_add(1, std::memory_order_relaxed);
                    while (!go.load(std::memory_order_acquire)) { std::this_thread::yield(); }

                    for (int i = 0; i < iterations; ++i)
                    {
                        std::lock_guard<SuspendMutex> guard(m);
                        if (occupancy.fetch_add(1, std::memory_order_acq_rel) != 0)
                        {
                            overlap.store(true, std::memory_order_relaxed); // a second holder got in -> broken
                        }
                        ++counter; // safe: serialized by the mutex
                        occupancy.fetch_sub(1, std::memory_order_acq_rel);
                    }
                });
            }

            while (ready.load(std::memory_order_relaxed) < threadCount) { std::this_thread::yield(); }
            go.store(true, std::memory_order_release);
            for (auto& th : threads) { th.join(); }

            Assert::IsFalse(overlap.load(std::memory_order_relaxed)); // never two holders at once
            Assert::AreEqual(static_cast<int64_t>(threadCount) * iterations, counter); // no lost updates
        }
    };
}}
