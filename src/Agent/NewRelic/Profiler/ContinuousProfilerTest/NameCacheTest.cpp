/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <string>

#include "../ContinuousProfiler/namecache.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using NewRelic::Profiler::ContinuousProfiler::NameCache;
using NewRelic::Profiler::ContinuousProfiler::PreallocTypeName;
using NewRelic::Profiler::ContinuousProfiler::PreallocMethodName;
using NewRelic::Profiler::ContinuousProfiler::TypeAndMethodNames;
using NewRelic::Profiler::ThreadProfiler::MAX_TYPE_NAME_LENGTH;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    namespace
    {
        // .second is the length INCLUDING the null terminator (see namecache.h).
        PreallocTypeName MakePreallocName(const std::wstring& value)
        {
            PreallocTypeName result{};
            wcscpy_s(result.first.data(), result.first.size(), value.c_str());
            result.second = static_cast<ULONG>(value.size() + 1);
            return result;
        }

        // Fills the whole buffer with a repeated character (no null terminator written) and sets
        // .second directly, independent of the buffer contents, to probe to_xstring_t's clamping at
        // and past the buffer boundary.
        PreallocTypeName MakePreallocNameWithRawLength(wchar_t fillChar, ULONG rawLength)
        {
            PreallocTypeName result{};
            result.first.fill(fillChar);
            result.second = rawLength;
            return result;
        }
    }

    TEST_CLASS(NameCacheTest)
    {
    public:

        TEST_METHOD(insert_then_lookup_round_trip)
        {
            NameCache cache;
            cache.insert(1, 100, 5, MakePreallocName(L"MyType"), MakePreallocName(L"MyMethod"));

            Assert::IsTrue(cache.has_fid(100));
            const auto& entry = cache[100];
            Assert::AreEqual(L"MyType", entry.TypeName());
            Assert::AreEqual(L"MyMethod", entry.MethodName());

            const auto typeName = cache.typename_for(1, 5);
            Assert::AreEqual(L"MyType", typeName->c_str());
        }

        // Two functions that share a (moduleId, typeDef) must share the same cached type-name string --
        // verified via pointer identity, not just equal content, so a regression that re-allocates the
        // type name on every insert is caught even though the *values* would still compare equal.
        TEST_METHOD(type_name_is_reused_across_functions_sharing_a_type)
        {
            NameCache cache;
            cache.insert(1, 100, 5, MakePreallocName(L"SharedType"), MakePreallocName(L"MethodA"));
            cache.insert(1, 101, 5, MakePreallocName(L"SharedType"), MakePreallocName(L"MethodB"));

            const xchar_t* firstTypeNamePtr = cache[100].TypeName();
            const xchar_t* secondTypeNamePtr = cache[101].TypeName();

            Assert::IsTrue(firstTypeNamePtr == secondTypeNamePtr);
        }

        TEST_METHOD(has_fid_is_false_and_lookup_falls_back_to_unknown_on_miss)
        {
            NameCache cache;

            Assert::IsFalse(cache.has_fid(999));
            const auto& entry = cache[999];
            Assert::AreEqual(L"UnknownClass", entry.TypeName());
            Assert::AreEqual(L"UnknownMethod(error)", entry.MethodName());
        }

        TEST_METHOD(typename_for_falls_back_to_unknown_type_name_on_miss)
        {
            NameCache cache;

            const auto typeName = cache.typename_for(1, 999);
            Assert::IsTrue(typeName == TypeAndMethodNames::GetUnknownTypeName());
        }

        // prealloc.second == 0 must produce an empty string rather than underflowing the
        // prealloc.second - 1 computation in to_xstring_t.
        TEST_METHOD(zero_length_prealloc_produces_empty_string)
        {
            NameCache cache;
            const auto zeroLengthName = MakePreallocNameWithRawLength(L'A', 0);
            cache.insert(1, 100, 5, zeroLengthName, zeroLengthName);

            const auto& entry = cache[100];
            Assert::AreEqual(L"", entry.TypeName());
            Assert::AreEqual(L"", entry.MethodName());
        }

        // A length past the buffer's capacity must clamp to the buffer bound rather than reading (or
        // computing a size that would read) past the end of the fixed-size array.
        TEST_METHOD(out_of_range_length_clamps_to_buffer_bound)
        {
            NameCache cache;
            const ULONG farPastBuffer = static_cast<ULONG>(MAX_TYPE_NAME_LENGTH) + 500;
            const auto oversizedName = MakePreallocNameWithRawLength(L'A', farPastBuffer);
            cache.insert(1, 100, 5, oversizedName, oversizedName);

            const auto& entry = cache[100];
            const std::wstring expected(MAX_TYPE_NAME_LENGTH - 1, L'A');
            Assert::AreEqual(expected.c_str(), entry.TypeName());
            Assert::AreEqual(expected.c_str(), entry.MethodName());
        }

        // Sustained inserts well past the cache's configured 5000-entry bound must evict the oldest
        // entries rather than growing unbounded, while recently-used entries stay resolvable -- the
        // mirror image of ThreadProfilerNameCacheTest's proof that TP's cache never evicts.
        TEST_METHOD(cache_stays_bounded_under_sustained_inserts_past_capacity)
        {
            NameCache cache;
            constexpr int EntryCount = 6000;

            for (int i = 0; i < EntryCount; ++i)
            {
                const auto typeName = MakePreallocName(L"Type" + std::to_wstring(i));
                const auto methodName = MakePreallocName(L"Method" + std::to_wstring(i));
                cache.insert(1, static_cast<FunctionID>(i + 1), static_cast<mdTypeDef>(i + 1), typeName, methodName);
            }

            // The earliest entries must have been evicted by the bounded LRU.
            Assert::IsFalse(cache.has_fid(1));
            Assert::IsFalse(cache.has_fid(2));

            // Recently-inserted entries must still resolve correctly.
            Assert::IsTrue(cache.has_fid(EntryCount));
            const auto& lastEntry = cache[EntryCount];
            Assert::AreEqual((L"Type" + std::to_wstring(EntryCount - 1)).c_str(), lastEntry.TypeName());
            Assert::AreEqual((L"Method" + std::to_wstring(EntryCount - 1)).c_str(), lastEntry.MethodName());
        }
    };
}}}
