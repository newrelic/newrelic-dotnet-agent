// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <memory>
#include <string>
#include <atlbase.h>
#include <Windows.h>
#include <Shlobj.h>
#include "Exceptions.h"

#include "../Logging/Logger.h"
#include "../Logging/DefaultFileLogLocation.h"
#include "../MethodRewriter/ISystemCalls.h"

namespace NewRelic { namespace Profiler
{
    using NewRelic::Profiler::MethodRewriter::FilePaths;

    struct SystemCalls : Logger::IFileDestinationSystemCalls, MethodRewriter::ISystemCalls
    {
        xstring_t _newRelicHomePath;
        xstring_t _newRelicInstallPath;

        SystemCalls(xstring_t newRelicHomePath, xstring_t newRelicInstallPath)
        {
            _newRelicHomePath = newRelicHomePath;
            _newRelicInstallPath = newRelicInstallPath;
        }

        virtual xstring_t GetNewRelicHomePath() override
        {
            return _newRelicHomePath;
        }

        virtual xstring_t GetNewRelicInstallPath() override
        {
            return _newRelicInstallPath;
        }

        virtual std::unique_ptr<xstring_t> TryGetEnvironmentVariable(const xstring_t& variableName) override
        {
            // get the size of the buffer required to hold the result
            auto size = GetEnvironmentVariable(variableName.c_str(), nullptr, 0);
            if (size == 0) return nullptr;

            // allocate a string big enough to hold the result
            std::unique_ptr<wchar_t[]> value(new wchar_t[size]);

            // get the environment variable
            auto result = GetEnvironmentVariable(variableName.c_str(), value.get(), size);
            if (result == 0) return nullptr;

            return std::unique_ptr<xstring_t>(new xstring_t(value.get()));
        }

        static std::unique_ptr<xstring_t> TryGetRegistryStringValue(HKEY rootKey, const xstring_t& path, const xstring_t& valueName)
        {
            DWORD valueSize;
            CRegKey key;

            // open the key
            auto result = key.Open(rootKey, path.c_str(), KEY_QUERY_VALUE);
            if (result != ERROR_SUCCESS) return nullptr;

            // get the size of the value
            result = RegGetValue(key.m_hKey, nullptr, valueName.c_str(), RRF_RT_REG_SZ, nullptr, nullptr, &valueSize);
            if (result != ERROR_SUCCESS) return nullptr;

            // allocate enough space for the value
            std::unique_ptr<wchar_t[]> valueString(new wchar_t[valueSize]);

            // get the value as a string
            result = RegGetValue(key.m_hKey, nullptr, valueName.c_str(), RRF_RT_REG_SZ, nullptr, valueString.get(), &valueSize);
            if (result != ERROR_SUCCESS) return nullptr;

            return std::unique_ptr<xstring_t>(new xstring_t(valueString.get()));
        }

        virtual xstring_t GetProgramCommandLine()
        {
            auto commandLine = GetCommandLineW();
            return commandLine;
        }

        virtual xstring_t GetProcessPath()
        {
            const int MAX_PROCESS_PATH = 1024;
            wchar_t moduleName[MAX_PROCESS_PATH];
            auto result = ::GetModuleFileName(nullptr, moduleName, MAX_PROCESS_PATH);
            if (result == 0)
            {
                auto error = ::GetLastError();
                LogError(L"Unable to get the process name.  Error number: ", std::hex, error, std::resetiosflags(std::ios_base::basefield));
                throw ProfilerException();
            }

            return moduleName;
        }

        virtual xstring_t GetProcessDirectoryPath()
        {
            const int MAX_PROCESS_PATH = 1024;
            wchar_t path[MAX_PROCESS_PATH];
            auto result = ::GetModuleFileName(nullptr, path, MAX_PROCESS_PATH);
            if (result == 0)
            {
                auto error = ::GetLastError();
                LogError(L"Unable to get the process name.  Error number: ", std::hex, error, std::resetiosflags(std::ios_base::basefield));
                throw ProfilerException();
            }

            if (!::PathRemoveFileSpec(path))
            {
                LogError(L"Expected a path to a file but was unable to trim the file off suggesting a path to a file was not found.  Path: ", path);
                throw ProfilerException();
            }

            return path;
        }

        virtual uint32_t GetCurrentProcessId() override
        {
            return ::GetCurrentProcessId();
        }

        virtual std::shared_ptr<xostream> OpenFile(const xstring_t& fileName, std::ios_base::openmode openMode) override
        {
            auto file = std::make_shared<std::wofstream>();
            file->open(fileName, openMode);
            return file;
        }

        virtual void CloseFile(std::shared_ptr<xostream> fileAsStream) override
        {
            auto file = std::static_pointer_cast<std::wofstream, std::wostream>(fileAsStream);
            file->close();
        }

        virtual bool FileExists(const xstring_t& filePath)
        {
            auto attributes = ::GetFileAttributes(filePath.c_str());
            if (attributes == INVALID_FILE_ATTRIBUTES) return false;
            if (attributes & FILE_ATTRIBUTE_DIRECTORY) return false;
            return true;
        }

        virtual bool DirectoryExists(const xstring_t& directoryName)
        {
            auto fileType = GetFileAttributesW(directoryName.c_str());
            
            //something is wrong with your path!
            if (fileType == INVALID_FILE_ATTRIBUTES)
            {
                return false;  
            }

            //If the file type is a directory then true else false
            if (fileType & FILE_ATTRIBUTE_DIRECTORY){
                return true;
            }

            return false;

        }

        // throws on failure
        virtual void DirectoryCreate(const xstring_t& directoryName)
        {
            auto ret = [&]() { return HRESULT_FROM_WIN32(::SHCreateDirectoryEx(NULL, directoryName.c_str(), NULL)); };
            ThrowOnError(ret);
        }

        // throws on failure
        virtual xstring_t GetCommonAppDataFolderPath() override
        {
            std::unique_ptr<wchar_t[]> charPath(new wchar_t[MAX_PATH]);
            ThrowOnError(::SHGetFolderPath, nullptr, CSIDL_COMMON_APPDATA, nullptr, SHGFP_TYPE_CURRENT, charPath.get());
            
            return charPath.get();
        }

        static FilePaths GetFilesInDirectory(xstring_t directoryPath, xstring_t fileExtension)
        {
            FilePaths fileList;

            WIN32_FIND_DATA fileData;
            auto searchPattern = directoryPath + _X("\\*.") + fileExtension;
            auto handle = FindFirstFile(searchPattern.c_str(), &fileData);
            if (handle == INVALID_HANDLE_VALUE)
            {
                LogWarn("No instrumentation files found.");
                return fileList;
            }
            fileList.emplace(directoryPath + _X("\\") + fileData.cFileName);

            while (FindNextFile(handle, &fileData))
            {
                fileList.emplace(directoryPath + _X("\\") + fileData.cFileName);
            }

            FindClose(handle);

            return fileList;
        }
    };
}}