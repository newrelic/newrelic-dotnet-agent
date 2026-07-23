/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <mutex>

namespace NewRelic { namespace Profiler
{
    // SuspendMutex is a process-wide lockable held around SuspendRuntime + DoStackSnapshot by BOTH
    // the ThreadProfiler and the ContinuousProfiler so the two never suspend the runtime concurrently.
    //
    // Only one runtime suspend/stack-walk cycle may be in flight at a time; the two profilers share
    // the single native stack-walk machinery, so they must serialize on this mutex. Acquire via the
    // shared instance (SuspendMutex::Shared()) and hold a std::lock_guard/std::unique_lock over the
    // whole SuspendRuntime -> DoStackSnapshot -> ResumeRuntime sequence.
    class SuspendMutex
    {
    public:
        // The process-wide shared instance. Both profilers lock this same mutex.
        static SuspendMutex& Shared() noexcept
        {
            static SuspendMutex s_instance;
            return s_instance;
        }

        void lock()
        {
            _mtx.lock();
        }

        bool try_lock()
        {
            return _mtx.try_lock();
        }

        void unlock()
        {
            _mtx.unlock();
        }

        SuspendMutex() noexcept = default;
        ~SuspendMutex() noexcept = default;
        SuspendMutex(const SuspendMutex&) = delete;
        SuspendMutex(SuspendMutex&&) = delete;
        SuspendMutex& operator=(const SuspendMutex&) = delete;
        SuspendMutex& operator=(SuspendMutex&&) = delete;

    private:
        std::mutex _mtx;
    };
}}
