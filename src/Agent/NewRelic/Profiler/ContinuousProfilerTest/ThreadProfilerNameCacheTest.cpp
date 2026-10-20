/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <string>
// namecache.h uses std::forward_as_tuple but doesn't include <tuple> itself -- pulled in transitively
// everywhere else it's used today, but not here. Include explicitly rather than touch that file: it's
// deliberately left byte-for-byte reverted, see ContinuousProfiler/namecache.h's header comment.
#include <tuple>

#include "../ThreadProfiler/namecache.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using NewRelic::Profiler::ThreadProfiler::NameCache;
using NewRelic::Profiler::ThreadProfiler::PreallocTypeName;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    namespace
    {
        // PreallocTypeName and PreallocMethodName are the same underlying
        // std::array<xchar_t, 1023>/ULONG pair shape; .second is the length INCLUDING the null
        // terminator (see namecache.h).
        PreallocTypeName MakePreallocName(const std::wstring& value)
        {
            PreallocTypeName result{};
            wcscpy_s(result.first.data(), result.first.size(), value.c_str());
            result.second = static_cast<ULONG>(value.size() + 1);
            return result;
        }
    }

    TEST_CLASS(ThreadProfilerNameCacheTest)
    {
    public:
        // Regression test: the thread profiler's NameCache was briefly bounded to a 5000-entry LRU
        // (shared with the continuous profiler's cache via namecache.h). That broke TP's deferred
        // name-resolution model -- ThreadProfilingService.cs resolves every FunctionID name once, at the
        // END of a profiling session (RequestFunctionNames), so the cache must hold every method sampled
        // during that whole session. Any real ASP.NET (Core) app easily exceeds 5000 distinct methods in
        // a default profile window; an evicted entry silently renders as "UnknownClass"/
        // "UnknownMethod(error)" in the delivered profile tree. TP's cache must never evict.
        TEST_METHOD(cache_holds_more_than_five_thousand_entries_without_eviction)
        {
            NameCache cache;
            constexpr int EntryCount = 6000;

            for (int i = 0; i < EntryCount; ++i)
            {
                const auto typeName = MakePreallocName(L"Type" + std::to_wstring(i));
                const auto methodName = MakePreallocName(L"Method" + std::to_wstring(i));
                cache.insert(static_cast<FunctionID>(i + 1), static_cast<mdTypeDef>(i + 1), typeName, methodName);
            }

            // The very first entry inserted is the one an LRU bounded to 5000 would have evicted first.
            Assert::IsTrue(cache.has_fid(1));
            Assert::IsTrue(cache.has_fid(EntryCount));

            const auto& firstEntry = cache[1];
            Assert::AreEqual(L"Type0", firstEntry.TypeName());
            Assert::AreEqual(L"Method0", firstEntry.MethodName());
        }
    };
}}}
