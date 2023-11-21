/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../Common/xplat.h"
#ifdef PAL_STDCPP_COMPAT
#include "../Profiler/UnixSystemCalls.h"
#else
#include "../Profiler/SystemCalls.h"
#endif

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    class AgentCallStyle
    {
    public:
        enum class Strategy
        {
            Reflection,
            AppDomainCache,
            FuncInvoke
        };

        AgentCallStyle(std::shared_ptr<ISystemCalls> systemCalls) : _systemCalls(systemCalls) {}

        const Strategy GetConfiguredCallingStrategy()
        {
            if (_systemCalls->GetIsAppDomainCachingDisabled())
            {
                return Strategy::Reflection;
            }

            if (_systemCalls->GetIsLegacyCachingEnabled())
            {
                return Strategy::AppDomainCache;
            }

            return Strategy::FuncInvoke;
        }

        static const xstring_t ToString(const Strategy agentCallStrategy)
        {
            if (agentCallStrategy == Strategy::FuncInvoke)
            {
                return _X("Func Invoke");
            }

            if (agentCallStrategy == Strategy::AppDomainCache)
            {
                return _X("AppDomain Cache");
            }

            return _X("Reflection");
        }

    private:
        std::shared_ptr<ISystemCalls> _systemCalls;
    };
}}}
