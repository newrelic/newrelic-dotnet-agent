/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once

#ifndef PAL_STDCPP_COMPAT

#include <Windows.h>
#include "../Common/xplat.h"

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    // GetThreadDescription only exists on Windows 10 1607+ / Server 2016+. Statically importing it would
    // put it in NewRelic.Profiler.dll's import table, and a missing statically-imported export fails the
    // load of the ENTIRE DLL on older Windows (Server 2012 R2, Win7 SP1) -- regardless of whether
    // Continuous Profiling is even enabled. Resolve it lazily via GetProcAddress instead, so an absent
    // export just means no thread names, not a dead profiler.
    class GetThreadDescriptionResolver
    {
    public:
        using FunctionPointer = HRESULT(WINAPI*)(HANDLE, PWSTR*);

        static FunctionPointer Resolve() noexcept
        {
            static FunctionPointer cached = ResolveOnce();
            return cached;
        }

        // Split out from ResolveThreadName so tests can exercise the "export not present on this OS"
        // fallback (getThreadDescriptionFunc == nullptr) without needing an actual pre-Win10-1607 machine.
        static xstring_t ResolveThreadName(DWORD osThreadId, FunctionPointer getThreadDescriptionFunc) noexcept
        {
            if (getThreadDescriptionFunc == nullptr)
            {
                return xstring_t();
            }

            // THREAD_QUERY_LIMITED_INFORMATION is the minimal right needed and succeeds for threads in our own process.
            HANDLE hThread = ::OpenThread(THREAD_QUERY_LIMITED_INFORMATION, FALSE, osThreadId);
            if (hThread == nullptr)
            {
                return xstring_t();
            }

            xstring_t result;
            PWSTR description = nullptr;
            const HRESULT hr = getThreadDescriptionFunc(hThread, &description);
            if (SUCCEEDED(hr) && description != nullptr)
            {
                result.assign(description);
                ::LocalFree(description);
            }
            ::CloseHandle(hThread);
            return result;
        }

    private:
        static FunctionPointer ResolveOnce() noexcept
        {
            HMODULE hKernel32 = ::GetModuleHandleW(L"kernel32.dll");
            if (hKernel32 == nullptr)
            {
                return nullptr;
            }

            return reinterpret_cast<FunctionPointer>(::GetProcAddress(hKernel32, "GetThreadDescription"));
        }
    };
}}}

#endif
