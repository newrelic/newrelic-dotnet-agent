/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <memory>

#ifdef PAL_STDCPP_COMPAT
#include <atl.h>
#else // PAL_STDCPP_COMPAT (not)
#include <atlcomcli.h>
#endif // PAL_STDCPP_COMPAT

#include "../Common/Macros.h"
#include "../Sicily/codegen/ITokenizer.h"

namespace NewRelic { namespace Profiler { namespace ModuleInjector
{
    class IModule
    {
    public:
        virtual xstring_t GetModuleName() = 0;
        virtual void InjectPlatformInvoke(const xstring_t& methodName, const xstring_t& className, const xstring_t& moduleName, const ByteVector& signature) = 0;
        virtual void InjectStaticSecuritySafeMethod(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) = 0;
        virtual void InjectMscorlibSecuritySafeMethodReference(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) = 0;
        virtual void InjectSystemPrivateCoreLibSecuritySafeMethodReference(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) = 0;
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
