/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once

#ifdef PAL_STDCPP_COMPAT
// Linux: /proc/self/task/<tid>/comm is the OS-tid-keyed source of a thread's name (pthread_getname_np
// needs a pthread_t, which we do not have for an arbitrary sampled OS thread id). Read AFTER resume.
#include <cstdio>
#include <unistd.h>
#endif

#include <cor.h>
#include <corprof.h>

#include "../Common/xplat.h"

// Shared OS-thread-name lookup for the sampling paths. Extracted from ContinuousProfiler so the
// event-driven AllocationSampler stamps thread names with the SAME platform code instead of a second
// copy of it.
namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    // Resolve an OS thread's name (empty string when it has none). Windows: GetThreadDescription on a
    // handle opened for the OS thread id. Linux: read /proc/self/task/<tid>/comm (comm caps names at
    // ~15 chars). Both paths ALLOCATE and make syscalls, so callers must only run this OUTSIDE a runtime
    // suspend window (ContinuousProfiler calls it after ResumeRuntime). Never throws.
    inline xstring_t ResolveOsThreadName(DWORD osThreadId) noexcept
    {
        try
        {
#ifdef PAL_STDCPP_COMPAT
            // Linux: pthread_getname_np needs a pthread_t we do not have for an arbitrary sampled OS
            // thread id, so read the kernel-exposed comm file keyed directly by tid.
            char path[64] = { 0 };
            std::snprintf(path, sizeof(path), "/proc/self/task/%u/comm", static_cast<unsigned>(osThreadId));

            std::FILE* f = std::fopen(path, "r");
            if (f == nullptr)
            {
                return xstring_t(); // thread gone or comm unreadable -> "".
            }

            char name[64] = { 0 };
            const size_t read = std::fread(name, 1, sizeof(name) - 1, f);
            std::fclose(f);

            // comm is newline-terminated; trim the trailing '\n' and any tail.
            size_t len = read;
            while (len > 0 && (name[len - 1] == '\n' || name[len - 1] == '\r'))
            {
                --len;
            }
            name[len] = '\0';

            return ToWideString(name);
#else
            // Windows: GetThreadDescription (Win 10+). THREAD_QUERY_LIMITED_INFORMATION is the minimal
            // right needed and succeeds for threads in our own process.
            xstring_t result;
            HANDLE hThread = ::OpenThread(THREAD_QUERY_LIMITED_INFORMATION, FALSE, osThreadId);
            if (hThread == nullptr)
            {
                return xstring_t();
            }

            PWSTR description = nullptr;
            const HRESULT hr = ::GetThreadDescription(hThread, &description);
            if (SUCCEEDED(hr) && description != nullptr)
            {
                result.assign(description);
                ::LocalFree(description);
            }
            ::CloseHandle(hThread);
            return result;
#endif
        }
        catch (...)
        {
            return xstring_t();
        }
    }
}}}
