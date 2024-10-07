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

        // TODO: remove in v11
        virtual xstring_t GetLegacyNewRelicHomePathVariable()
        {
            return _isCoreClr
                ? _X("CORECLR_NEWRELIC_HOME")
                : _X("NEWRELIC_HOME");
        }

        virtual xstring_t GetNewRelicHomePathVariable()
        {
            return _isCoreClr
                ? _X("CORECLR_NEW_RELIC_HOME")
                : _X("NEW_RELIC_HOME");
        }

        // TODO: remove in v11
        virtual xstring_t GetLegacyNewRelicInstallPathVariable()
        {
            return _X("NEWRELIC_INSTALL_PATH");
        }

        virtual xstring_t GetNewRelicInstallPathVariable()
        {
            return _X("NEW_RELIC_INSTALL_PATH");
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicHomePath()
        {
            auto homePath = TryGetEnvironmentVariable(GetNewRelicHomePathVariable());

            // TODO: remove in v11
            if (homePath == nullptr)
            {
                homePath = TryGetEnvironmentVariable(GetLegacyNewRelicHomePathVariable());
            }
            return homePath;
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicInstallPath()
        {
            auto installPath = TryGetEnvironmentVariable(GetNewRelicInstallPathVariable());
            // TODO: remove in v11
            if (installPath == nullptr)
            {
                installPath = TryGetEnvironmentVariable(GetLegacyNewRelicInstallPathVariable());
            }
            return installPath;
        }

        virtual bool GetForceProfiling()
        {
            return TryGetEnvironmentVariable(_X("NEW_RELIC_FORCE_PROFILING")) != nullptr
                // TODO: remove in v11
                || TryGetEnvironmentVariable(_X("NEWRELIC_FORCE_PROFILING")) != nullptr;
        }

        virtual bool GetIsAppDomainCachingDisabled()
        {
            return GetEnvironmentBool(_X("NEW_RELIC_DISABLE_APPDOMAIN_CACHING"), false);
        }

        virtual std::unique_ptr<xstring_t> GetProfilerDelay()
        {
            auto modernValue = TryGetEnvironmentVariable(_X("NEW_RELIC_PROFILER_DELAY_IN_SEC"));
            return modernValue != nullptr
                ? std::move(modernValue)
                // TODO: remove in v11
                : TryGetEnvironmentVariable(_X("NEWRELIC_PROFILER_DELAY_IN_SEC"));
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicProfilerLogDirectory()
        {
            auto modernValue = TryGetEnvironmentVariable(_X("NEW_RELIC_PROFILER_LOG_DIRECTORY"));
            return modernValue != nullptr
                ? std::move(modernValue)
                // TODO: remove in v11
                : TryGetEnvironmentVariable(_X("NEWRELIC_PROFILER_LOG_DIRECTORY"));
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicLogDirectory()
        {
            auto modernValue = TryGetEnvironmentVariable(_X("NEW_RELIC_LOG_DIRECTORY"));
            return modernValue != nullptr
                ? std::move(modernValue)
                // TODO: remove in v11
                :TryGetEnvironmentVariable(_X("NEWRELIC_LOG_DIRECTORY"));
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicLogLevel()
        {
            auto modernValue = TryGetEnvironmentVariable(_X("NEW_RELIC_LOG_LEVEL"));
            return modernValue != nullptr
                ? std::move(modernValue)
                // TODO: remove in v11
                : TryGetEnvironmentVariable(_X("NEWRELIC_LOG_LEVEL"));
        }

        virtual std::unique_ptr<xstring_t> GetAppPoolId()
        {
            return TryGetEnvironmentVariable(_X("APP_POOL_ID"));
        }

        virtual bool GetLoggingEnabled(bool fallback)
        {
            return GetEnvironmentBool(_X("NEW_RELIC_LOG_ENABLED"), fallback);
        }

        virtual bool GetConsoleLoggingEnabled(bool fallback)
        {
            return GetEnvironmentBool(_X("NEW_RELIC_LOG_CONSOLE"), fallback);
        }

        virtual bool IsAzureFunction()
        {
            // Azure Functions sets the FUNCTIONS_WORKER_RUNTIME environment variable to "dotnet-isolated" when running in the .NET worker.
            auto functionsWorkerRuntime = TryGetEnvironmentVariable(_X("FUNCTIONS_WORKER_RUNTIME"));
            return functionsWorkerRuntime != nullptr && !functionsWorkerRuntime->empty();
        }

        virtual bool IsAzureFunctionLogLevelOverrideEnabled() {
            return GetEnvironmentBool(_X("NEW_RELIC_AZURE_FUNCTION_LOG_LEVEL_OVERRIDE"), false);
        }



    private:
        bool _isCoreClr = false;
        /// <summary>
        /// Gets an environment variable that should be a boolean
        /// </summary>
        /// <param name="variableName">Name of the environment variable to fetch</param>
        /// <param name="fallback">If the environment variable doesn't
        /// exist, or the value isn't 0/1/true/false, return the given fallback value</param>
        /// <returns>The value of the environment variable, or the fallback</returns>
        bool GetEnvironmentBool(const xstring_t& variableName, bool fallback)
        {
            auto value = TryGetEnvironmentVariable(variableName);

            if (value == nullptr)
            {
                return fallback;
            }

            if (Strings::AreEqualCaseInsensitive(*value, _X("true")) || Strings::AreEqualCaseInsensitive(*value, _X("1")))
            {
                return true;
            }

            if (Strings::AreEqualCaseInsensitive(*value, _X("false")) || Strings::AreEqualCaseInsensitive(*value, _X("0")))
            {
                return false;
            }

            return fallback;
        }
    };
    typedef std::shared_ptr<ISystemCalls> ISystemCallsPtr;
    typedef std::set<xstring_t> FilePaths;
}}}
