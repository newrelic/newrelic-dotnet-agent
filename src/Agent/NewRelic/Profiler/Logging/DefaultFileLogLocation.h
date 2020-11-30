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
        virtual xstring_t GetNewRelicHomePath() = 0;
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
            // use the environment variable if it is set
            auto logDirectory = _system->TryGetEnvironmentVariable(GetLogDirectoryEnvironmentVariableName());
            if (logDirectory)
            {
                return *logDirectory;
            }

            // if this is Azure WebSites use the special case for that
            if (IsAzureWebSites())
            {
                return GetAzureWebSiteLogDirectory();
            }

            // if there is a NEWRELIC_HOME environment variable log relative to it
            logDirectory = _system->TryGetEnvironmentVariable(_system->GetNewRelicHomePath());
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

        static const xstring_t GetLogDirectoryEnvironmentVariableName()
        {
            return _X("NEWRELIC_PROFILER_LOG_DIRECTORY");
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

#ifdef __NEVER__
    class FileDestination : public IDestination
    {
    public:
        FileDestination(IFileDestinationSystemCallsPtr system) :
            _system(system)
        {
#ifdef PAL_STDCPP_COMPAT
            auto logFilePath = GetLogFilePath();
            auto fileName = xstring_t(logFilePath) + PATH_SEPARATOR + _X("NewRelic.Profiler.") + to_xstring(_system->GetCurrentProcessId()) + _X(".log");

            if (!_system->DirectoryExists(logFilePath))
            {
                _system->DirectoryCreate(logFilePath);
            }

            _fileStream = _system->OpenFile(fileName, std::ios_base::trunc);
#else
            // name the file
            xostringstream fileName;
            fileName.exceptions(xostringstream::failbit | xostringstream::badbit);
            long processId = _system->GetCurrentProcessId();
            auto logFilePath = GetLogFilePath();
            fileName.width(2);
            fileName.fill('0');
            fileName.setf(std::ios::right);
            fileName << logFilePath << PATH_SEPARATOR << "NewRelic.Profiler." << processId << ".log";
            
            if(!_system->DirectoryExists(logFilePath))
            {
                _system->DirectoryCreate(logFilePath);
            }

            // Since we name with PID, eventually we are going to re-use file names. This is okay since we don't want to flood their logs directory but shouldn't append because that would be confusing so we delete the previous and create a new one when a PID is reused.
            _fileStream = _system->OpenFile(fileName.str(), std::ios_base::trunc);
#endif

            _fileStream->exceptions(std::wostream::failbit | std::wostream::badbit);
        }

        virtual ~FileDestination()
        {
            _system->CloseFile(_fileStream);
        }

//IDestination deprecated
        //void Flush() override
        //{
        //    _fileStream->flush();
        //}

        //void Write(const std::wstring& message) override
        //{
        //    *_fileStream << message;
        //}

    private:
        // returns path to an existing directory where the log file should be written
        xstring_t GetLogFilePath()
        {
            // use the environment variable if it is set
            auto logDirectory = _system->TryGetEnvironmentVariable(GetLogDirectoryEnvironmentVariableName());
            if (logDirectory != nullptr)
            {
                return *logDirectory;
            }

            // if this is Azure WebSites use the special case for that
            if (IsAzureWebSites())
            {
                return GetAzureWebSiteLogDirectory();
            }

            // if there is a NEWRELIC_HOME environment variable log relative to it
            logDirectory = _system->TryGetEnvironmentVariable(_system->GetNewRelicHomePath());
            if (logDirectory != nullptr)
            {

                return *logDirectory.get() + PATH_SEPARATOR + 
#ifdef PAL_STDCPP_COMPAT
                    _X("logs");
#else
                    _X("Logs");
#endif
            }

            // for everything else use the standard directory
            return GetStandardLogDirectory();
        }

        bool IsAzureWebSites()
        {
            auto home = _system->TryGetEnvironmentVariable(_X("HOME_EXPANDED"));
            if (home == nullptr) return false;
            
            if (StartsWith(*home, _X("C:\\DWASFiles\\Sites\\"))) return true;
            else return false;
        }

        const xstring_t GetAzureWebSiteLogDirectory()
        {
            auto home = _system->TryGetEnvironmentVariable(_X("HOME"));
            if (home == nullptr) return _X("");

            return *home + PATH_SEPARATOR + _X("LogFiles") + PATH_SEPARATOR + _X("NewRelic");
        }

        const xstring_t GetStandardLogDirectory()
        {
            return _system->GetCommonAppDataFolderPath() + PATH_SEPARATOR + _X("New Relic") + PATH_SEPARATOR + _X(".NET Agent") + PATH_SEPARATOR + _X("Logs");
        }

        static const xstring_t GetLogDirectoryEnvironmentVariableName()
        {
            return _X("NEWRELIC_PROFILER_LOG_DIRECTORY");
        }

        bool StartsWith(xstring_t longerString, xstring_t shorterString)
        {
            // if the shorter string is not actually shorter then the longer string doesn't start with the shorter one
            if (longerString.length() < shorterString.length())
            {
                return false;
            }

            // compare the strings up to the length of the shorter string, returning true if they are equal
            if (longerString.compare(0, shorterString.length(), shorterString) == 0)
            {
                return true;
            }

            // return false for everything else
            return false;
        }

    private:
        std::shared_ptr<std::wostream> _fileStream;
        IFileDestinationSystemCallsPtr _system;
    };

    typedef std::shared_ptr<FileDestination> FileDestinationPtr;
#endif
}}}

