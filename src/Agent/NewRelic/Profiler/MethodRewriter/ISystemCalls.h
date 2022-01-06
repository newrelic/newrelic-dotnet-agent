/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include <string>
#include <set>
#include "../Common/xplat.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter {
    struct ISystemCalls
    {
        virtual std::unique_ptr<xstring_t> TryGetEnvironmentVariable(const xstring_t& variableName) = 0;
        virtual void SetNewRelicHomeAndInstallPathEnvVar(bool isCoreClr) = 0;
        virtual xstring_t GetNewRelicHomePathEnvVar() = 0;
        virtual xstring_t GetNewRelicInstallPathEnvVar() = 0;
        virtual bool FileExists(const xstring_t& filePath) = 0;
    };
    typedef std::shared_ptr<ISystemCalls> ISystemCallsPtr;
    typedef std::set<xstring_t> FilePaths;
}}}
