/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <functional>
#include <stdint.h>
#include "../MethodRewriter/IFunction.h"
#include "MockFunctionHeaderInfo.h"
#include "../Common/Macros.h"
#include "../sicily/SicilyTest/RealisticTokenizer.h"
#include "../Configuration/InstrumentationPoint.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    struct MockTokenResolver : public SignatureParser::ITokenResolver
    {
        MockTokenResolver() : 
            _typeString(L"MyNamespace.MyTypeName"),
            _typeGenericArgumentCount(0)
        {}
        std::wstring _typeString;
        virtual std::wstring GetTypeStringsFromTypeDefOrRefOrSpecToken(uint32_t /*typeDefOrRefOrSPecToken*/)
        {
            return _typeString;
        }

        uint32_t _typeGenericArgumentCount;
        virtual uint32_t GetTypeGenericArgumentCount(uint32_t /*typeDefOrMethodDefToken*/)
        {
            return _typeGenericArgumentCount;
        }
    };

    struct MockFunction : IFunction
    {
        MockFunction(bool isGeneric = false, std::wstring version = std::wstring()) :
            _functionId(0x12345678),
            _assemblyName(L"MyAssembly"),
            _moduleName(L"MyModule"),
            _appDomainName(L"MyApplicationDomain"),
            _typeName(L"MyNamespace.MyClass"),
            _functionName(L"MyMethod"),
            _methodToken(0x123),
            _tokenizer(new sicily::codegen::RealisticTokenizer()),
            _tokenResolver(new MockTokenResolver()),
            _signatureToken(0x456),
            _string(L"[MyAssembly]MyNamespace.MyClass.MyMethod"),
            _isGenericType(false),
            _typeToken(0x045612345),
            _isCoreClr(false),
            _shouldInjectMethodInstrumentation(false),
            _shouldTrace(false),
            _classAttributes(0),
            _methodAttributes(0),
            _isValid(true)
        {
            if (version.empty())
            {
                version = L"1.2.3.4";
            }
            _version = version;

            _signature = std::make_shared<ByteVector>();
            _tokenSignature = std::make_shared<ByteVector>();

            if (isGeneric)
            {
                _signature->push_back(0x10);  // generic calling convention
                _tokenSignature->push_back(0x10);  // generic calling convention
            }
            else
            {
                _signature->push_back(0x00);  // default calling convention
                _tokenSignature->push_back(0x00);  // default calling convention
            }

            _signature->push_back(0x01);  // 1 parameter
            _signature->push_back(0x01);  // void return
            _signature->push_back(0x12);  // parameter 1 class
            _signature->push_back(0x49);  // class token (compressed 0x01000012)

            _tokenSignature->push_back(0x00);  // no parameters
            _tokenSignature->push_back(0x01);  // void return


            BYTEVECTOR(methodBytes,
                0x2 | (0x1 << 2), // TinyFormat (0x2), code size 1 (0x1 << 2)
                0x2a // return
                );
            _methodBytes = std::make_shared<ByteVector>(methodBytes);

            BYTEVECTOR(tokenSignatureBytes,
                0x00, // default calling convention
                0x00, // no parameters
                0x01  // void return
                );
            _tokenSignature = std::make_shared<ByteVector>(tokenSignatureBytes);
        }

        uintptr_t _functionId;
        virtual uintptr_t GetFunctionId() override
        {
            return _functionId;
        }

        std::wstring _assemblyName;
        virtual std::wstring GetAssemblyName() override
        {
            return _assemblyName;
        }

        std::wstring _moduleName;
        virtual std::wstring GetModuleName() override
        {
            return _moduleName;
        }

        std::wstring _appDomainName;
        virtual std::wstring GetAppDomainName()override
        {
            return _appDomainName;
        }

        std::wstring _typeName;
        virtual std::wstring GetTypeName() override
        {
            return _typeName;
        }

        std::wstring _functionName;
        virtual std::wstring GetFunctionName() override 
        {
            return _functionName;
        }

        uint32_t _methodToken;
        virtual uint32_t GetMethodToken() override
        {
            return _methodToken;
        }

        virtual FunctionHeaderInfoPtr GetFunctionHeaderInfo() override {
            return std::make_shared<MockFunctionHeaderInfo>((uint16_t)1);
        }

        unsigned long _classAttributes;
        virtual unsigned long GetClassAttributes() override
        {
            return _classAttributes;
        }

        unsigned long _methodAttributes;
        virtual unsigned long GetMethodAttributes() override
        {
            return _methodAttributes;
        }

        virtual bool Preprocess() override
        {
            return true;
        }

        bool _shouldTrace;
        virtual bool ShouldTrace() override
        {
            return _shouldTrace;
        }

        std::wstring _version;
        virtual ASSEMBLYMETADATA GetAssemblyProps() override
        {
            auto pVersion = std::unique_ptr<AssemblyVersion>(AssemblyVersion::Create(_version));
            return ASSEMBLYMETADATA
            {
                pVersion->Major,
                pVersion->Minor,
                pVersion->Build,
                pVersion->Revision
            };
        }

        bool _shouldInjectMethodInstrumentation;
        virtual bool ShouldInjectMethodInstrumentation() override
        {
            return _shouldInjectMethodInstrumentation;
        }

        virtual uint32_t GetTracerFlags() override
        {
            return 0;
        }

        bool _isValid;
        virtual bool IsValid() override
        {
            return _isValid;
        }

        bool _isCoreClr;
        virtual bool IsCoreClr() override
        {
            return _isCoreClr;
        }

        uint32_t _typeToken;
        virtual uint32_t GetTypeToken() override
        {
            return _typeToken;
        }

        // get the signature for this method
        ByteVectorPtr _signature;
        virtual ByteVectorPtr GetSignature() override
        {
            return _signature;
        }
        
        // returns the bytes that make up this method, this includes the header and the code
        ByteVectorPtr _methodBytes;
        virtual ByteVectorPtr GetMethodBytes() override
        {
            return _methodBytes;
        }

        // get the tokenizer that should be used to modify the code bytes
        sicily::codegen::ITokenizerPtr _tokenizer;
        virtual sicily::codegen::ITokenizerPtr GetTokenizer() override
        {
            return _tokenizer;
        }

        // get the token resolver that should be used to modify the code bytes
        SignatureParser::ITokenResolverPtr _tokenResolver;
        virtual SignatureParser::ITokenResolverPtr GetTokenResolver() override
        {
            return _tokenResolver;
        }

        // get a signature given a signature token
        ByteVectorPtr _tokenSignature;
        virtual ByteVectorPtr GetSignatureFromToken(uint32_t /*token*/) override
        {
            return _tokenSignature;
        }

        // get a token for a given signature
        uint32_t _signatureToken;
        virtual uint32_t GetTokenFromSignature(const ByteVector& /*signature*/) override
        {
            return _signatureToken;
        }

        std::function<void(const ByteVector&)> _writeMethodHandler;
        virtual void WriteMethod(const ByteVector& method) override
        {
            if (_writeMethodHandler) return _writeMethodHandler(method);
        }

        // stringify the object for error logging
        std::wstring _string;
        virtual std::wstring ToString() override
        {
            return _string;
        }

        bool _isGenericType;
        virtual bool IsGenericType()
        {
            return _isGenericType;
        }

        // get the instrumentation point that will match this function
        Configuration::InstrumentationPointPtr GetInstrumentationPoint()
        {
            auto instrumentationPoint = std::make_shared<Configuration::InstrumentationPoint>();
            instrumentationPoint->AssemblyName = GetAssemblyName();
            instrumentationPoint->ClassName = GetTypeName();
            instrumentationPoint->MethodName = GetFunctionName();
            return instrumentationPoint;
        }
    };

    typedef std::shared_ptr<MockFunction> MockFunctionPtr; 
}}}}
