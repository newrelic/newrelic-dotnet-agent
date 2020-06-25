/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../Common/CorStandIn.h"
#include "IFunction.h"
#include "../SignatureParser/SignatureParser.h"

using namespace NewRelic::Profiler::SignatureParser;

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    class InstantiatedGenericType
    {
    public:
        static uint32_t GetMethodToken(IFunctionPtr function, MethodSignaturePtr methodSignature)
        {
            auto typeSpecToken = GetTypeSpecToken(function);
            auto functionName = function->GetFunctionName();
            auto bytesSig = methodSignature->ToBytes();
            auto methodToken = function->GetTokenizer()->GetMemberRefOrDefToken(typeSpecToken, functionName, *bytesSig);
            return methodToken;
        }

    private:
        static uint32_t GetTypeSpecToken(IFunctionPtr function)
        {
            GenericTypePtr genericTypeSignature = GetGenericTypeSignature(function);
            auto typeSignatureBytes = genericTypeSignature->ToBytes();
            return function->GetTokenizer()->GetTypeSpecToken(*typeSignatureBytes);
        }

        static GenericTypePtr GetGenericTypeSignature(IFunctionPtr function)
        {
            TypePtr classType(new ClassType(function->GetTypeToken()));

            auto argumentCount = GetArgumentCount(function);
            auto arguments = GetArguments(argumentCount);

            return std::make_shared<GenericType>(classType, arguments);
        }

        // Returns the count of generic arguments on the generic class type.
        static uint32_t GetArgumentCount(IFunctionPtr function)
        {
            return function->GetTokenResolver()->GetTypeGenericArgumentCount(function->GetTypeToken());
        }

        // Returns the generic arguments on the generic class type.
        static TypesPtr GetArguments(uint32_t nArguments)
        {
            auto arguments(std::make_shared<Types>());

            for (uint32_t i = 0; i < nArguments; i++)
            {
                TypePtr varType(new VarType(i));
                arguments->push_back(varType);
            }

            return arguments;
        }
    };
}}}