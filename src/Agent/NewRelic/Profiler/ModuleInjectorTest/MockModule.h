// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include "../ModuleInjector/IModule.h"
#include "../Sicily/SicilyTest/RealisticTokenizer.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    class MockModule : public ModuleInjector::IModule
    {
    public:
        MockModule() :
            _moduleName(_X("MyModule")),
            _needsReferenceToCoreLib(false),
            _injectReferenceToCoreLib(true),
            _isThisTheCoreLibAssembly(false),
            _tokenizer(std::make_shared<sicily::codegen::RealisticTokenizer>())
        {
        }

        xstring_t _moduleName;
        virtual xstring_t GetModuleName() override
        {
            return _moduleName;
        }

        std::function<void(const xstring_t&, const xstring_t&, const xstring_t&, const ByteVector&)> _injectPlatformInvoke;
        virtual void InjectPlatformInvoke(const xstring_t& methodName, const xstring_t& className, const xstring_t& moduleName, const ByteVector& signature) override
        {
            if (_injectPlatformInvoke)
            {
                _injectPlatformInvoke(methodName, className, moduleName, signature);
            }
        }

        std::function<void(const xstring_t&, const xstring_t&, const ByteVector&)> _injectStaticSecuritySafeMethod;
        virtual void InjectStaticSecuritySafeMethod(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) override
        {
            if (_injectStaticSecuritySafeMethod)
            {
                _injectStaticSecuritySafeMethod(methodName, className, signature);
            }
        }

        std::function<void(const xstring_t&, const xstring_t&, const ByteVector&)> _injectStaticSecuritySafeCtor;
        virtual void InjectStaticSecuritySafeCtor(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) override
        {
            if (_injectStaticSecuritySafeCtor)
            {
                _injectStaticSecuritySafeCtor(methodName, className, signature);
            }
        }

        std::function<void(const xstring_t&, const xstring_t&, const ByteVector&)> _injectCoreLibSecuritySafeMethodReference;
        virtual void InjectCoreLibSecuritySafeMethodReference(const xstring_t& methodName, const xstring_t& className, const ByteVector& signature) override
        {
            if (_injectCoreLibSecuritySafeMethodReference)
            {
                _injectCoreLibSecuritySafeMethodReference(methodName, className, signature);
            }
        }

        std::function<void(void)> _injectNRHelperType;
        virtual void InjectNRHelperType() override
        {
            if (_injectNRHelperType)
            {
                _injectNRHelperType();
            }
        }

        bool _injectReferenceToCoreLib;
        virtual bool InjectReferenceToCoreLib() override
        {
            return _injectReferenceToCoreLib;
        }

        bool _needsReferenceToCoreLib;
        virtual bool NeedsReferenceToCoreLib() override
        {
            return _needsReferenceToCoreLib;
        }

        bool _isThisTheCoreLibAssembly;
        virtual bool GetIsThisTheCoreLibAssembly() override
        {
            return _isThisTheCoreLibAssembly;
        }

        sicily::codegen::ITokenizerPtr _tokenizer;
        virtual sicily::codegen::ITokenizerPtr GetTokenizer() override
        {
            return _tokenizer;
        }
    };
}}}}
