/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include "../ContinuousProfiler/ThreadDescriptionResolver.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using NewRelic::Profiler::ContinuousProfiler::GetThreadDescriptionResolver;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    TEST_CLASS(ThreadDescriptionResolverTest)
    {
    public:

        // Regression guard for the "statically imported GetThreadDescription can crash the DLL load on
        // pre-1607 Windows" bug: a null function pointer (the exact state seen on an OS without the
        // export) must fall back to an empty name instead of dereferencing a null pointer.
        TEST_METHOD(resolve_thread_name_returns_empty_when_function_pointer_is_null)
        {
            const xstring_t result = GetThreadDescriptionResolver::ResolveThreadName(::GetCurrentThreadId(), nullptr);

            Assert::IsTrue(result.empty());
        }

        // Happy path on this (Win10+) test machine: the real export resolves and returns a non-null
        // function pointer that can be invoked.
        TEST_METHOD(resolve_returns_non_null_function_pointer_on_current_os)
        {
            auto func = GetThreadDescriptionResolver::Resolve();

            Assert::IsNotNull(reinterpret_cast<void*>(func));
        }

        // Resolve() caches its result -- repeated calls must return the same pointer, not re-resolve.
        TEST_METHOD(resolve_returns_same_cached_pointer_across_calls)
        {
            auto first = GetThreadDescriptionResolver::Resolve();
            auto second = GetThreadDescriptionResolver::Resolve();

            Assert::IsTrue(first == second);
        }

        // Happy path end-to-end: resolving the current thread's description via the real, lazily-resolved
        // function pointer does not throw and returns whatever description the OS reports (possibly empty
        // if this thread has none set), proving the resolved pointer is actually callable.
        TEST_METHOD(resolve_thread_name_succeeds_with_real_function_pointer)
        {
            auto func = GetThreadDescriptionResolver::Resolve();

            const xstring_t result = GetThreadDescriptionResolver::ResolveThreadName(::GetCurrentThreadId(), func);

            // No crash/throw is the assertion; result content is OS/thread-state dependent.
            Assert::IsTrue(result.empty() || !result.empty());
        }

        // An invalid/unknown OS thread id must fail closed (empty name), not throw.
        TEST_METHOD(resolve_thread_name_returns_empty_for_unknown_thread_id)
        {
            auto func = GetThreadDescriptionResolver::Resolve();

            const xstring_t result = GetThreadDescriptionResolver::ResolveThreadName(static_cast<DWORD>(-1), func);

            Assert::IsTrue(result.empty());
        }
    };
}}}
