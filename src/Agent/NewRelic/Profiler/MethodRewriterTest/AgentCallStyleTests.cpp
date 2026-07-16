// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <stdint.h>
#include <memory>
#include <exception>
#include <functional>
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include "CppUnitTest.h"
#include "MockSystemCalls.h"
#include "../MethodRewriter/AgentCallStyle.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    TEST_CLASS(AgentCallStyleTests)
    {
    public:
        TEST_METHOD(GetConfiguredCallingStrategy_Default_ReturnsAppDomainFallbackCache)
        {
            auto systemCalls = std::make_shared<MockSystemCalls>();

            AgentCallStyle agentCallStyle(systemCalls);

            const auto strategy = agentCallStyle.GetConfiguredCallingStrategy();

            Assert::AreEqual(
                static_cast<int>(AgentCallStyle::Strategy::AppDomainFallbackCache),
                static_cast<int>(strategy));
        }

        TEST_METHOD(GetConfiguredCallingStrategy_WhenAppDomainCachingDisabled_ReturnsReflection)
        {
            auto systemCalls = std::make_shared<MockSystemCalls>();
            systemCalls->SetEnvironmentVariable(_X("NEW_RELIC_DISABLE_APPDOMAIN_CACHING"), _X("true"));

            AgentCallStyle agentCallStyle(systemCalls);

            const auto strategy = agentCallStyle.GetConfiguredCallingStrategy();

            Assert::AreEqual(
                static_cast<int>(AgentCallStyle::Strategy::Reflection),
                static_cast<int>(strategy));
        }

        TEST_METHOD(ToString_AppDomainFallbackCache_ReturnsExpectedString)
        {
            const xstring_t expected = _X("AppDomain Fallback Cache");
            Assert::AreEqual(expected, AgentCallStyle::ToString(AgentCallStyle::Strategy::AppDomainFallbackCache));
        }

        TEST_METHOD(ToString_Reflection_ReturnsExpectedString)
        {
            const xstring_t expected = _X("Reflection");
            Assert::AreEqual(expected, AgentCallStyle::ToString(AgentCallStyle::Strategy::Reflection));
        }

        TEST_METHOD(ResolveEffectiveStrategy_FallbackCache_InjectionFailed_DowngradesToReflection)
        {
            Assert::IsTrue(AgentCallStyle::ResolveEffectiveStrategy(
                AgentCallStyle::Strategy::AppDomainFallbackCache, false) == AgentCallStyle::Strategy::Reflection);
        }
        TEST_METHOD(ResolveEffectiveStrategy_FallbackCache_InjectionSucceeded_StaysFallbackCache)
        {
            Assert::IsTrue(AgentCallStyle::ResolveEffectiveStrategy(
                AgentCallStyle::Strategy::AppDomainFallbackCache, true) == AgentCallStyle::Strategy::AppDomainFallbackCache);
        }
        TEST_METHOD(ResolveEffectiveStrategy_Reflection_InjectionFailed_StaysReflection)
        {
            Assert::IsTrue(AgentCallStyle::ResolveEffectiveStrategy(
                AgentCallStyle::Strategy::Reflection, false) == AgentCallStyle::Strategy::Reflection);
        }
        TEST_METHOD(ResolveEffectiveStrategy_Reflection_InjectionSucceeded_StaysReflection)
        {
            Assert::IsTrue(AgentCallStyle::ResolveEffectiveStrategy(
                AgentCallStyle::Strategy::Reflection, true) == AgentCallStyle::Strategy::Reflection);
        }
    };
}}}}
