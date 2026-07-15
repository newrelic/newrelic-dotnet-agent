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

    private:
        std::shared_ptr<ISystemCalls> _systemCalls;
    };
}}}
