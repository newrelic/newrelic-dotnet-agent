// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <stdint.h>
#include <memory>
#include <exception>
#include <functional>
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include "MockSystemCalls.h"
#include "../MethodRewriter/AgentCallStyle.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter{ namespace Test
{
    TEST_CLASS(AgentCallStyleTest)
    {
    public:
        TEST_METHOD(AgentCallStrategy_LegacyCaching_false_DisableAppDomainCaching_false_Expected_InAgent)
        {
            RunTest(_X("false"), _X("false"), AgentCallStyle::Strategy::FuncInvoke);
        }

        TEST_METHOD(AgentCallStrategy_LegacyCaching_false_DisableAppDomainCaching_true_Expected_Reflection)
        {
            RunTest(_X("false"), _X("true"), AgentCallStyle::Strategy::Reflection);
        }

        TEST_METHOD(AgentCallStrategy__LegacyCaching_true_DisableAppDomainCaching_false_Expected_AppDomain)
        {
            RunTest(_X("true"), _X("false"), AgentCallStyle::Strategy::AppDomainCache);
        }

        TEST_METHOD(AgentCallStrategy_LegacyCaching_true_DisableAppDomainCaching_true_Expected_Relfection)
        {
            RunTest(_X("true"), _X("true"), AgentCallStyle::Strategy::Reflection);
        }

        TEST_METHOD(AgentCallStrategy_EnvironmentVariable_0)
        {
            RunTest(_X("0"), _X("false"), AgentCallStyle::Strategy::FuncInvoke);
        }

        TEST_METHOD(AgentCallStrategy_EnvironmentVariable_1)
        {
            RunTest(_X("1"), _X("false"), AgentCallStyle::Strategy::AppDomainCache);
        }

        TEST_METHOD(AgentCallStrategy_EnvironmentVariable_Empty)
        {
            RunTest(_X(""), _X("false"), AgentCallStyle::Strategy::FuncInvoke);
        }

        TEST_METHOD(AgentCallStrategy_EnvironmentVariable_NotSet)
        {
            RunTestNoEnvironmentVariablesSet(AgentCallStyle::Strategy::FuncInvoke);
        }

        TEST_METHOD(AgentCallStrategy_ToString_FuncInvoke)
        {
            const xstring_t expectedValue = _X("Func Invoke");
            Assert::AreEqual(expectedValue, AgentCallStyle::ToString(AgentCallStyle::Strategy::FuncInvoke));
        }

        TEST_METHOD(AgentCallStrategy_ToString_AppDomainCache)
        {
            const xstring_t expectedValue = _X("AppDomain Cache");
            Assert::AreEqual(expectedValue, AgentCallStyle::ToString(AgentCallStyle::Strategy::AppDomainCache));
        }

        TEST_METHOD(AgentCallStrategy_ToString_Reflection)
        {
            const xstring_t expectedValue = _X("Reflection");
            Assert::AreEqual(expectedValue, AgentCallStyle::ToString(AgentCallStyle::Strategy::Reflection));
        }

    private:
        void RunTest(const xstring_t& legacyCachingEnabled, const xstring_t& disableAppDomainCache, const AgentCallStyle::Strategy expectedStrategy)
        {
            auto systemCalls = std::make_shared<MockSystemCalls>();
            systemCalls->SetEnvironmentVariable(_X("NEW_RELIC_ENABLE_LEGACY_CACHING"), legacyCachingEnabled);
            systemCalls->SetEnvironmentVariable(_X("NEW_RELIC_DISABLE_APPDOMAIN_CACHING"), disableAppDomainCache);

            AgentCallStyle agentCallStyle(systemCalls);

            const auto callStrategy = agentCallStyle.GetConfiguredCallingStrategy();

            Assert::AreEqual(static_cast<int>(expectedStrategy), static_cast<int>(callStrategy));
        }

        void RunTestNoEnvironmentVariablesSet(const AgentCallStyle::Strategy expectedStrategy)
        {
            auto systemCalls = std::make_shared<MockSystemCalls>();
            AgentCallStyle agentCallStyle(systemCalls);

            const auto callStrategy = agentCallStyle.GetConfiguredCallingStrategy();

            Assert::AreEqual(static_cast<int>(expectedStrategy), static_cast<int>(callStrategy));
        }
    };
}}}}
