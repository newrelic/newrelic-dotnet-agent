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
        virtual void InjectStaticSecuritySafeCtor(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) = 0;
        virtual void InjectCoreLibSecuritySafeMethodReference(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) = 0;
        virtual void InjectNRHelperType() = 0;
        virtual bool InjectReferenceToCoreLib() = 0;

        virtual bool NeedsReferenceToCoreLib() = 0;
        virtual bool GetIsThisTheCoreLibAssembly() = 0;

        // (F) After InjectIntoModule runs on the core library, re-read metadata to confirm the
        // __NRInitializer__ helper type was actually defined. Used to decide whether to keep the
        // AppDomainFallbackCache strategy or downgrade the process to Reflection.
        virtual bool VerifyNRHelperTypeInjected() = 0;

        virtual sicily::codegen::ITokenizerPtr GetTokenizer() = 0;
    };

    typedef std::shared_ptr<IModule> IModulePtr;
}}}
