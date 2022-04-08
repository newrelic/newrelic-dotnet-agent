// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once

#include <memory>
#include <string>
#include <shlobj.h>
#include "Exceptions.h"

#include "../Logging/Logger.h"
#include "../Logging/DefaultFileLogLocation.h"
#include "../MethodRewriter/ISystemCalls.h"

#include <sys/types.h>
#include <sys/stat.h>
#include <unistd.h>
#include <stdio.h>
#include <dirent.h>
#include <libgen.h>

namespace NewRelic { namespace Profiler
{
    using NewRelic::Profiler::MethodRewriter::FilePaths;

    struct SystemCalls : MethodRewriter::ISystemCalls
    {
        SystemCalls()
        {
        }

        static std::string ToCharString(xstring_t str)
        {
            return std::string(str.begin(), str.end());
        }

        static xstring_t ToWideString(char* chars)
        {
            if (chars == nullptr) return _X("");
            std::string str = std::string(chars);
            return xstring_t(str.begin(), str.end());
        }

        virtual std::unique_ptr<xstring_t> TryGetEnvironmentVariable(const xstring_t& variableName) override
        {
            auto envVal = std::getenv(ToCharString(variableName).c_str());

            if (envVal == nullptr)
            {
                return nullptr;
            }
            return std::make_unique<xstring_t>(ToWideString(envVal));
        }

        static std::unique_ptr<xstring_t> TryGetRegistryStringValue(HKEY rootKey, const xstring_t& path, const xstring_t& valueName)
        {
            return nullptr;
        }

        // Helper function for re-building the command line string in GetProgramCommandLine()
        static std::string QuoteIfNecessary(std::string arg)
        {
            bool inputDoesNotContainSpaces = arg.find(" ") == std::string::npos;
            return inputDoesNotContainSpaces ? arg : "'" + arg + "'";
        }

        virtual xstring_t GetProgramCommandLine()
        {
            try
            {
                std::ifstream procCmdline("/proc/self/cmdline"); // /proc/self/cmdline only exists on Linux.  Let's hope MSFT doesn't port .NET Core to FreeBSD...
                std::vector<std::string> args;
                for (std::string str; std::getline(procCmdline, str, '\0'); )
                {
                    args.emplace_back(std::move(str));
                }
                std::string formattedCommandLine = "";
                while (!args.empty())
                {
                    formattedCommandLine = QuoteIfNecessary(args.back()) + " " + formattedCommandLine;
                    args.pop_back();
                }
                return xstring_t(formattedCommandLine.begin(), formattedCommandLine.end());
            }
            catch (const std::exception &e)
            {
                LogError(L"Exception caught trying to read process command line: ", e.what());
                return _X("");
            }
            catch (...)
            {
                LogError(L"Unknown exception caught trying to read process command line.");
                return _X("");
            }
        }

        virtual xstring_t GetProcessPath()
        {
            /*
            char *argv[] = {};
            int err = PAL_Initialize(0, argv);
            if(0 != err)
            {
                LogDebug(L"Initialize failed");
                return _X("");
            }

            LogInfo(L"Initialize : " << err);

            const int MAX_PROCESS_PATH = 1024;
            xchar_t moduleName[MAX_PROCESS_PATH];
            auto result = GetModuleFileNameW(nullptr, moduleName, MAX_PROCESS_PATH);
            if (result == 0)
            {
                auto error = ::GetLastError();
                LogError(L"Unable to get the process name.  Error number: " << error);
                //throw ProfilerException();
            } else {
                LogDebug(L"Process path: " << xstring_t(moduleName));
            }
            //return moduleName;
            */
            
            return _X(".");
        }

        virtual xstring_t GetProcessDirectoryPath()
        {
            return _X(".");
        }

        virtual uint32_t GetCurrentProcessId() override
        {
            return getpid();
        }

        virtual std::shared_ptr<std::wostream> OpenFile(const xstring_t& fileName, std::ios_base::openmode openMode) override
        {
            std::shared_ptr<std::basic_ofstream<wchar_t>> file = std::make_shared<std::basic_ofstream<wchar_t>>();
            file->open(std::string(fileName.begin(), fileName.end()), openMode);
            return file;
        }

        virtual void CloseFile(std::shared_ptr<std::wostream> fileAsStream) override
        {
            try {
                auto file = std::static_pointer_cast<std::basic_ofstream<wchar_t>, std::wostream>(fileAsStream);
                // Um..  Sometimes closing the log file throws an exception that if uncaught will crash the process. ¯\_(ツ)_/¯
                file->close();
            } catch (...) {
            }
        }

        virtual bool FileExists(const xstring_t& filePath) override
        {
            struct stat path_stat = {};
            int res = stat(std::string(filePath.begin(), filePath.end()).c_str(), &path_stat);
            return (res != -1) && S_ISREG(path_stat.st_mode);
        }

        virtual bool DirectoryExists(const xstring_t& directoryName) override
        {
            bool exists = std::ifstream(ToCharString(directoryName)) ? true : false;
            return exists && !FileExists(directoryName);
        }

        // throws on failure
        virtual void DirectoryCreate(const xstring_t& directoryName) override
        {
            MakePath(ToCharString(directoryName));
        }

        void MakePath(std::string pathToCreate)
        {
            std::string tmp = pathToCreate; // have to make a copy of pathToCreate because dirname modifies its argument
            char* parent_path = dirname(const_cast<char*>(tmp.c_str()));

            if (!DirectoryExists(ToWideString(parent_path)))
            {
                MakePath(std::string(parent_path));
            }
            mkdir(pathToCreate.c_str(), S_IRWXU | S_IRWXG | S_IROTH | S_IXOTH);
        }

        // throws on failure
        virtual xstring_t GetCommonAppDataFolderPath() override
        {
            return _X("");
        }

        static bool has_suffix(const std::string& s, const std::string& suffix)
        {
            return (s.size() >= suffix.size()) && equal(suffix.rbegin(), suffix.rend(), s.rbegin());
        }

        static FilePaths GetFilesInDirectory(xstring_t directoryPath, xstring_t fileExtension)
        {
            FilePaths fileList;

            auto dir = opendir(ToCharString(directoryPath.c_str()).c_str());
            if(!dir)
            {
                return fileList;
            }

            auto ext = ToCharString(fileExtension);

            dirent *entry;
            while((entry = readdir(dir))!=nullptr) 
            {
                if (has_suffix(entry->d_name, ext))
                {
                    auto fileName = directoryPath + PATH_SEPARATOR + ToWideString(entry->d_name);
                    fileList.emplace(fileName);
                }
            }

            closedir(dir);

            return fileList;
        }

    };
}}
