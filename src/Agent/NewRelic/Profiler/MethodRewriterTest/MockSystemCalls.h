// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <functional>
#include <string>
#include <memory>
#include "../MethodRewriter/ISystemCalls.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test {
    struct MockSystemCalls : ISystemCalls
    {
        std::function<std::unique_ptr<std::wstring>(const std::wstring&)> EnvironmentVariableResult;

        MockSystemCalls()
        {
            EnvironmentVariableResult = [](const std::wstring&)
            {
                return std::unique_ptr<std::wstring>(new std::wstring(L"C:\\foo\\bar"));
            };
        }

        virtual std::unique_ptr<std::wstring> TryGetEnvironmentVariable(const std::wstring& variableName) override
        {
            return EnvironmentVariableResult(variableName);
        }

        virtual bool FileExists(const xstring_t&) override
        {
            return true;
        }

        virtual uint32_t GetCurrentProcessId() override { return 0; }
        virtual std::shared_ptr<std::wostream> OpenFile(const xstring_t&, std::ios_base::openmode) override { return nullptr; }
        virtual void CloseFile(std::shared_ptr<std::wostream>) override { }
        virtual bool DirectoryExists(const xstring_t&) override { return false; }
        virtual void DirectoryCreate(const xstring_t&) override { }
        virtual xstring_t GetCommonAppDataFolderPath() override { return _X(""); }

        virtual std::unique_ptr<xstring_t> GetNewRelicHomePath() override { return nullptr; }
        virtual std::unique_ptr<xstring_t> GetNewRelicProfilerLogDirectory() override { return nullptr; }
        virtual std::unique_ptr<xstring_t> GetNewRelicLogDirectory() override { return nullptr; }
        virtual std::unique_ptr<xstring_t> GetNewRelicLogLevel() override { return nullptr; }
    };
}}}}
