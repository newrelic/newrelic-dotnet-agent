// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <string>
#include <memory>
#include "../Common/Macros.h"
#include "../Sicily/codegen/ITokenizer.h"

namespace NewRelic { namespace Profiler { namespace ModuleInjector
{
    class IModule
    {
    public:
        virtual std::wstring GetModuleName() = 0;
        virtual void InjectPlatformInvoke(const std::wstring& methodName, const std::wstring& className, const std::wstring& moduleName, const ByteVector& signature) = 0;
        virtual void InjectStaticSecuritySafeMethod(const std::wstring& methodName, const std::wstring& className, const ByteVector& signature) = 0;
        virtual void InjectMscorlibSecuritySafeMethodReference(const std::wstring& methodName, const std::wstring& className, const ByteVector& signature) = 0;
        
        virtual sicily::codegen::ITokenizerPtr GetTokenizer() = 0;
    };

    typedef std::shared_ptr<IModule> IModulePtr;
}}}
