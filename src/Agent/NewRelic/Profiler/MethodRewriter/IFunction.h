/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <memory>
#include "../Common/CorStandIn.h"
#include "../Common/Macros.h"
#include "Exceptions.h"
#include "IFunctionHeaderInfo.h"
#include "../Sicily/codegen/ITokenizer.h"
#include "../SignatureParser/ITokenResolver.h"
#include "../SignatureParser/SignatureParser.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    class IFunction
    {
    public:
        virtual uintptr_t GetFunctionId() = 0;
        virtual xstring_t GetAssemblyName() = 0;
        virtual xstring_t GetModuleName() = 0;
        virtual xstring_t GetAppDomainName() = 0;
        virtual xstring_t GetTypeName() = 0;
        virtual xstring_t GetFunctionName() = 0;
        virtual uint32_t GetMethodToken() = 0;
        virtual uint32_t GetTypeToken() = 0;
        virtual DWORD GetClassAttributes() = 0;
        virtual DWORD GetMethodAttributes() = 0;
        virtual FunctionHeaderInfoPtr GetFunctionHeaderInfo() = 0;
        virtual bool Preprocess() = 0;
        virtual bool ShouldTrace() = 0;
        virtual ASSEMBLYMETADATA GetAssemblyProps() = 0;
        virtual bool IsValid() = 0;
        virtual bool IsCoreClr() = 0;
        virtual uint32_t GetTracerFlags() = 0;

        // get the signature for this method
        virtual ByteVectorPtr GetSignature() = 0;
        // returns the bytes that make up this method, this includes the header and the code
        virtual ByteVectorPtr GetMethodBytes() = 0;
        // get the tokenizer that should be used to modify the code bytes
        virtual sicily::codegen::ITokenizerPtr GetTokenizer() = 0;
        // get the token resolver that should be used to modify the code bytes
        virtual SignatureParser::ITokenResolverPtr GetTokenResolver() = 0;
        // get a signature given a signature token
        virtual ByteVectorPtr GetSignatureFromToken(uint32_t token) = 0;
        // get a token for a given signature
        virtual uint32_t GetTokenFromSignature(const ByteVector& signature) = 0;

        // writes the method to be JIT compiled, method consists of header bytes and code bytes
        virtual void WriteMethod(const ByteVector& method) = 0;

        // stringify the object for error logging
        virtual xstring_t ToString() = 0;

        virtual bool IsGenericType() = 0;
        virtual bool ShouldInjectMethodInstrumentation() = 0;
    };

    typedef std::shared_ptr<IFunction> IFunctionPtr;
}}}
