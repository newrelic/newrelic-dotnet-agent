/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include <string>
#include <set>
#include "../Common/xplat.h"
#include "../Logging/DefaultFileLogLocation.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter {
    struct ISystemCalls : Logger::IFileDestinationSystemCalls
    {
        virtual std::unique_ptr<xstring_t> TryGetEnvironmentVariable(const xstring_t& variableName) = 0;
        virtual bool FileExists(const xstring_t& filePath) = 0;

        virtual void SetCoreAgent(bool IsCore = false)
        {
            _isCoreClr = IsCore;
        }

        virtual xstring_t GetNewRelicHomePathVariable()
        {
            return _isCoreClr
                ? _X("CORECLR_NEWRELIC_HOME")
                : _X("NEWRELIC_HOME");
        }

        virtual xstring_t GetNewRelicInstallPathVariable()
        {
            return _X("NEWRELIC_INSTALL_PATH");
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicHomePath()
        {
            return TryGetEnvironmentVariable(GetNewRelicHomePathVariable());
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicInstallPath()
        {
            return TryGetEnvironmentVariable(GetNewRelicInstallPathVariable());
        }

        virtual bool GetForceProfiling()
        {
            return TryGetEnvironmentVariable(_X("NEWRELIC_FORCE_PROFILING")) != nullptr;
        }

        virtual bool GetIsAppDomainCachingDisabled()
        {
            auto value = TryGetEnvironmentVariable(_X("NEW_RELIC_DISABLE_APPDOMAIN_CACHING"));

            if (value == nullptr)
            {
                return false;
            }

            if(Strings::AreEqualCaseInsensitive(*value, _X("true")) || Strings::AreEqualCaseInsensitive(*value, _X("1")))
            {
                return true;
            }

            return false;
        }

        virtual std::unique_ptr<xstring_t> GetProfilerDelay()
        {
            return TryGetEnvironmentVariable(_X("NEWRELIC_PROFILER_DELAY_IN_SEC"));
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicProfilerLogDirectory()
        {
            return TryGetEnvironmentVariable(_X("NEWRELIC_PROFILER_LOG_DIRECTORY"));
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicLogDirectory()
        {
            return TryGetEnvironmentVariable(_X("NEWRELIC_LOG_DIRECTORY"));
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicLogLevel()
        {
            return TryGetEnvironmentVariable(_X("NEWRELIC_LOG_LEVEL"));
        }

        virtual std::unique_ptr<xstring_t> GetAppPoolId()
        {
            return TryGetEnvironmentVariable(_X("APP_POOL_ID"));
        }

    private:
        bool _isCoreClr = false;
    };
    typedef std::shared_ptr<ISystemCalls> ISystemCallsPtr;
    typedef std::set<xstring_t> FilePaths;
}}}
