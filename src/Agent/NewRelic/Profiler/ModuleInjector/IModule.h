/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <memory>
#include <atlcomcli.h>
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
        virtual void InjectSystemPrivateCoreLibSecuritySafeMethodReference(const std::wstring& methodName, const std::wstring& className, const ByteVector& signature) = 0;
        virtual void InjectNRHelperType() = 0;

        virtual bool GetHasRefMscorlib() = 0;
        virtual bool GetHasRefSysRuntime() = 0;
        virtual bool GetHasRefNetStandard() = 0;
        virtual bool GetHasRefSystemPrivateCoreLib() = 0;

        virtual void SetMscorlibAssemblyRef(mdAssembly assemblyRefToken) = 0;
        virtual void SetSystemPrivateCoreLibAssemblyRef(mdAssembly assemblyRefToken) = 0;

        virtual bool GetIsThisTheMscorlibAssembly() = 0;
        virtual bool GetIsThisTheNetStandardAssembly() = 0;
        virtual bool GetIsThisTheSystemPrivateCoreLibAssembly() = 0;

        virtual CComPtr<IMetaDataAssemblyEmit> GetMetaDataAssemblyEmit() = 0;

        virtual sicily::codegen::ITokenizerPtr GetTokenizer() = 0;
    };

    typedef std::shared_ptr<IModule> IModulePtr;
}}}
