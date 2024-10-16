/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once

#include <string>
#include <memory>
#include "../Common/xplat.h"

namespace NewRelic { namespace Profiler { namespace Logger
{
    struct IFileDestinationSystemCalls
    {
        virtual uint32_t GetCurrentProcessId() = 0;
        virtual std::shared_ptr<std::wostream> OpenFile(const xstring_t& fileName, std::ios_base::openmode openMode) = 0;
        virtual void CloseFile(std::shared_ptr<std::wostream> fileAsStream) = 0;
        virtual std::unique_ptr<xstring_t> TryGetEnvironmentVariable(const xstring_t& variableName) = 0;
        virtual bool DirectoryExists(const xstring_t& logFilePath) = 0;
        // can throw on failure
        virtual void DirectoryCreate(const xstring_t& logFilePath) = 0;
        // can throw on failure
        virtual xstring_t GetCommonAppDataFolderPath() = 0;

        virtual std::unique_ptr<xstring_t> GetNewRelicHomePath() = 0;
        virtual std::unique_ptr<xstring_t> GetNewRelicProfilerLogDirectory() = 0;
        virtual std::unique_ptr<xstring_t> GetNewRelicLogDirectory() = 0;
        virtual std::unique_ptr<xstring_t> GetNewRelicLogLevel() = 0;
    };
    typedef std::shared_ptr<IFileDestinationSystemCalls> IFileDestinationSystemCallsPtr;

    class DefaultFileLogLocation
    {
        IFileDestinationSystemCallsPtr _system;
    public:
        DefaultFileLogLocation(IFileDestinationSystemCallsPtr system) : _system(system) {}

        xstring_t GetPathAndFileName()
        {
            xstring_t filename(GetLogFilePath());
            if (!_system->DirectoryExists(filename))
            {
                _system->DirectoryCreate(filename);
            }
            filename += PATH_SEPARATOR;
            filename += _X("NewRelic.Profiler.");
            filename += to_xstring(_system->GetCurrentProcessId());
            filename += _X(".log");
            return filename;
        }

    private:
        // returns path to an existing directory where the log file should be written
        xstring_t GetLogFilePath()
        {
            // use the profiler environment variable if it is set
            auto logDirectory = _system->GetNewRelicProfilerLogDirectory();
            if (logDirectory)
            {
                return *logDirectory;
            }

            // use the general environment variable if it is set
            logDirectory = _system->GetNewRelicLogDirectory();
            if (logDirectory)
            {
                return *logDirectory;
            }

            // if this is Azure WebSites use the special case for that
            if (IsAzureWebSites())
            {
                return GetAzureWebSiteLogDirectory();
            }

            // if there is a NEW_RELIC_HOME environment variable log relative to it
            logDirectory = _system->GetNewRelicHomePath();
            if (logDirectory)
            {
                return *logDirectory + 
#ifdef PAL_STDCPP_COMPAT
                    PATH_SEPARATOR _X("logs");
#else
                    PATH_SEPARATOR _X("Logs");
#endif
            }

            // for everything else use the standard directory
            return GetStandardLogDirectory();
        }

        bool IsAzureWebSites()
        {
            auto home = _system->TryGetEnvironmentVariable(_X("HOME_EXPANDED"));

            // makes no assumption about drive letter (e.g. C:, D:)
            return home && Contains(*home, _X(":\\DWASFiles\\Sites")); 
        }

        const xstring_t GetAzureWebSiteLogDirectory()
        {
            auto home = _system->TryGetEnvironmentVariable(_X("HOME"));
            if (!home) 
                return xstring_t();

            return *home + PATH_SEPARATOR _X("LogFiles") PATH_SEPARATOR _X("NewRelic");
        }

        const xstring_t GetStandardLogDirectory()
        {
            return _system->GetCommonAppDataFolderPath() + PATH_SEPARATOR _X("New Relic") PATH_SEPARATOR _X(".NET Agent") PATH_SEPARATOR _X("Logs");
        }

        bool Contains(xstring_t envVarValue, xstring_t searchValue)
        {
            // if the envVarValue is shorter than searchValue, then obviously it is not contained within
            if (envVarValue.length() < searchValue.length())
            {
                return false;
            }

            // find the searchValue in the envVarValue
            return envVarValue.find(searchValue) != std::string::npos;
        }
    };
}}}

