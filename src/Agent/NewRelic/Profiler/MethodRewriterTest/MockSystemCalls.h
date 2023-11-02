// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <functional>
#include <string>
#include <memory>
#include <unordered_map>
#include "../MethodRewriter/ISystemCalls.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test {
    struct MockSystemCalls : ISystemCalls
    {
        MockSystemCalls() { }

        virtual std::unique_ptr<xstring_t> TryGetEnvironmentVariable(const xstring_t& variableName) override
        {
            auto search = _environmentVariables.find(variableName);
            if (search != _environmentVariables.end())
            {
                return std::make_unique<xstring_t>(search->second);
            }

            return nullptr;
        }

        void SetEnvironmentVariable(const xstring_t& name, const xstring_t& value)
        {
            _environmentVariables[name] = value;
        }

        void ResetEnvironmentVariables()
        {
            _environmentVariables.clear();
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

    private:
        std::unordered_map<xstring_t, xstring_t> _environmentVariables;
    };
}}}}
