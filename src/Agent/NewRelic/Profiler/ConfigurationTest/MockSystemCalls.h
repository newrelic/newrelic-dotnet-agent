// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <functional>
#include <string>
#include <memory>
#include <unordered_map>
#ifdef PAL_STDCPP_COMPAT
#include "../Profiler/UnixSystemCalls.h"
#else
#include "../Profiler/SystemCalls.h"
#endif

namespace NewRelic { namespace Profiler { namespace Configuration { namespace Test
{
    struct MockSystemCalls : NewRelic::Profiler::SystemCalls
    {
        std::unordered_map<xstring_t, xstring_t> environmentVariables;

        MockSystemCalls()
        {

        }

        virtual std::unique_ptr<xstring_t> TryGetEnvironmentVariable(const xstring_t& variableName) override
        {
            return std::make_unique<xstring_t>(environmentVariables[variableName]);
        }

        virtual bool FileExists(const xstring_t&) override
        {
            return true;
        }

        void SetEnvironmentVariable(xstring_t name, xstring_t value)
        {
            environmentVariables[name] = value;
        }

        void ResetEnvironmentVariables()
        {
            environmentVariables.clear();
        }
    };
}}}}
