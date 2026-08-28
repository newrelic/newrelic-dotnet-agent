/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include "stdafx.h"
#include "CppUnitTest.h"

#include <cstdint>

#include "../ContinuousProfiler/AgentWorkMap.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace ContinuousProfiler
{
    TEST_CLASS(AgentWorkMapTest)
    {
    public:

        // A matched Increment/Decrement pair returns the thread to the untagged (depth 0) state.
        TEST_METHOD(increment_then_decrement_returns_to_untagged)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x1000);

            Assert::IsFalse(map.IsAgentWork(id)); // never incremented
            map.Increment(id);
            Assert::IsTrue(map.IsAgentWork(id));
            map.Decrement(id);
            Assert::IsFalse(map.IsAgentWork(id));
        }

        // Nesting: two Increments need two Decrements before the thread reads as untagged.
        TEST_METHOD(nested_increments_require_matching_decrements)
        {
            AgentWorkMap map;
            const ThreadID id = static_cast<ThreadID>(0x2000);

            map.Increment(id);
            map.Increment(id);
            map.Decrement(id);
            Assert::IsTrue(map.IsAgentWork(id)); // still nested one level deep
            map.Decrement(id);
            Assert::IsFalse(map.IsAgentWork(id));
        }

        // Decrementing a thread that is already at depth 0 (or was never incremented) is a safe no-op.
        TEST_METHOD(decrement_below_zero_is_a_safe_no_op)
        {
            AgentWorkMap map;
            const ThreadID never = static_cast<ThreadID>(0x3000);
            map.Decrement(never); // never incremented -> no slot -> no-op
            Assert::IsFalse(map.IsAgentWork(never));

            const ThreadID id = static_cast<ThreadID>(0x4000);
            map.Increment(id);
            map.Decrement(id);
            map.Decrement(id); // already at 0 -> must not underflow
            Assert::IsFalse(map.IsAgentWork(id));

            // A slot pinned at 0 must still respond correctly to a fresh Increment.
            map.Increment(id);
            Assert::IsTrue(map.IsAgentWork(id));
        }

        // The reserved empty key (0) is rejected by every operation without crashing.
        TEST_METHOD(empty_key_is_rejected)
        {
            AgentWorkMap map;
            const ThreadID emptyKey = static_cast<ThreadID>(0);

            map.Increment(emptyKey); // no-op
            map.Decrement(emptyKey); // no-op
            Assert::IsFalse(map.IsAgentWork(emptyKey));
        }

        // Distinct threads are tracked independently.
        TEST_METHOD(multiple_threads_tracked_independently)
        {
            AgentWorkMap map;
            const ThreadID a = static_cast<ThreadID>(0x1000);
            const ThreadID b = static_cast<ThreadID>(0x2000);

            map.Increment(a);
            Assert::IsTrue(map.IsAgentWork(a));
            Assert::IsFalse(map.IsAgentWork(b)); // b untouched

            map.Increment(b);
            map.Decrement(a);
            Assert::IsFalse(map.IsAgentWork(a));
            Assert::IsTrue(map.IsAgentWork(b)); // b still tagged
        }
    };
}}}
