/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include "../Common/xplat.h"
#include "ISystemCalls.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    class AgentCallStyle
    {
    public:
        enum class Strategy
        {
            Reflection,
            AppDomainFallbackCache
        };

        AgentCallStyle(std::shared_ptr<ISystemCalls> systemCalls) : _systemCalls(systemCalls) {}

        const Strategy GetConfiguredCallingStrategy()
        {
            if (_systemCalls->GetIsAppDomainCachingDisabled())
            {
                return Strategy::Reflection;
            }

            return Strategy::AppDomainFallbackCache;
        }

        static const xstring_t ToString(const Strategy agentCallStrategy)
        {
            if (agentCallStrategy == Strategy::AppDomainFallbackCache)
            {
                return _X("AppDomain Fallback Cache");
            }

            return _X("Reflection");
        }

        // (F) graceful degradation: the AppDomainFallbackCache fast-path calls helper members
        // that must be DEFINED into the core library. If that injection did not take, the process
        // must fall back to Reflection (self-contained IL that needs no injected helpers).
        // Pure decision, unit-tested; the metadata re-read that produces coreLibInjectionSucceeded
        // lives in the profiler callback (integration-only).
        static const Strategy ResolveEffectiveStrategy(const Strategy configuredStrategy, const bool coreLibInjectionSucceeded)
        {
            if (configuredStrategy == Strategy::AppDomainFallbackCache && !coreLibInjectionSucceeded)
            {
                return Strategy::Reflection;
            }

            return configuredStrategy;
        }

    private:
        std::shared_ptr<ISystemCalls> _systemCalls;
    };
}}}
