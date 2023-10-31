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
            RunTest(_X("false"), _X("false"), AgentCallStyle::Strategy::InAgentCache);
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

        TEST_METHOD(AgentCallStrategy_ToString_InAgentCache)
        {
            const xstring_t expectedValue = _X("In Agent Cache");
            Assert::AreEqual(expectedValue, AgentCallStyle::ToString(AgentCallStyle::Strategy::InAgentCache));
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
            systemCalls->EnvironmentVariableResult = [&legacyCachingEnabled, &disableAppDomainCache](const xstring_t& variableName)
            {
                if (variableName == _X("NEW_RELIC_ENABLE_LEGACY_CACHING"))
                {
                    return std::make_unique<xstring_t>(legacyCachingEnabled);
                }
                else if (variableName == _X("NEW_RELIC_DISABLE_APPDOMAIN_CACHING"))
                {
                    return std::make_unique<xstring_t>(disableAppDomainCache);
                }
                else
                {
                    return std::make_unique<xstring_t>(_X(""));
                }
            };

            AgentCallStyle agentCallStyle(systemCalls);

            const auto callStrategy = agentCallStyle.GetConfiguredCallingStrategy();

            Assert::AreEqual(static_cast<int>(expectedStrategy), static_cast<int>(callStrategy));
        }
    };
}}}}
