// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <functional>
#include <string>
#include <memory>
#include <unordered_map>
#include "../Common/Strings.h"
#include "../MethodRewriter/ISystemCalls.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    struct MockSystemCalls : ISystemCalls
    {
        std::unordered_map<xstring_t, xstring_t> environmentVariables;

        MockSystemCalls() = default;
        virtual ~MockSystemCalls() = default;

        std::unique_ptr<xstring_t> TryGetEnvironmentVariable(const xstring_t& variableName) override
        {
            // return nullptr if variableName isn't in the collection
            if (environmentVariables.find(variableName) == environmentVariables.end())
            {
                return nullptr;
            }
            return std::make_unique<xstring_t>(environmentVariables[variableName]);
        }

        bool FileExists(const xstring_t&) override
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

        // implement pure virtual methods from IFileDestinationSystemCalls, unused in these tests
        uint32_t GetCurrentProcessId() override { return 0;}

        std::shared_ptr<std::wostream> OpenFile(const xstring_t&, std::ios_base::openmode) override { return nullptr; }
        void CloseFile(std::shared_ptr<std::wostream> ) override {}
        bool DirectoryExists(const xstring_t& ) override { return false; }
        void DirectoryCreate(const xstring_t& ) override {}
        xstring_t GetCommonAppDataFolderPath() override { return L""; }
    };
}}}}
