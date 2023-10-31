/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once

#include "FunctionManipulator.h"
#include "InstrumentationSettings.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    // Writes the methods on our agent api shim to call the actual 
    // implementation in NewRelic.Agent.Core.AgentApi.
    class ApiFunctionManipulator : FunctionManipulator
    {
    public:
        ApiFunctionManipulator(IFunctionPtr function, InstrumentationSettingsPtr instrumentationSettings, const bool isCoreClr, const AgentCallStyle::Strategy agentCallStrategy) :
            FunctionManipulator(function, isCoreClr, agentCallStrategy),
            _instrumentationSettings(instrumentationSettings)
        {
            Initialize();
        }

        // instrument this method with the API stuff
        void InstrumentApi()
        {
            BuildApiInstructions();
            Instrument();
        }

    private:
        InstrumentationSettingsPtr _instrumentationSettings;

        void BuildApiInstructions()
        {
            LogTrace(_function->ToString(), L": Generating API bytecode instrumentation.");
            // set the max stack size to be big enough for our code
            GetHeader()->SetMaxStack(10);

            auto tokenizer = _function->GetTokenizer();
            uint16_t resultLocalIndex = 0;
            if (_methodSignature->_returnType->_kind != SignatureParser::ReturnType::Kind::VOID_RETURN_TYPE)
                resultLocalIndex = AppendReturnTypeLocal(_newLocalVariablesSignature, _methodSignature);
            
            TryCatch(
                [&]()
                {
                    // delegate = System.CannotUnloadAppDomainException.GetMethodFromAppDomainStorageOrReflectionOrThrow("NewRelic_Delegate_API_<function name><function signature>", "C:\path\to\NewRelic.Agent.Core", "NewRelic.Core.AgentApi", "<function name>", new object[] { <method parameter types> })
                    LoadMethodInfo(_instrumentationSettings->GetCorePath(), _X("NewRelic.Agent.Core.AgentApi"), _function->GetFunctionName(), _function->GetFunctionId(), GetArrayOfTypeParametersLamdba());
                    
                    _instructions->Append(_X("ldnull"));
                    BuildObjectArrayOfParameters();
                    _instructions->Append(_X("call   instance object [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Reflection.MethodBase::Invoke(object, object[])"));

                    if (_methodSignature->_returnType->_kind == SignatureParser::ReturnType::Kind::VOID_RETURN_TYPE)
                    {
                        _instructions->Append(_X("pop"));
                    }
                    else {
                        // we can't leave an object on the stack and CEE_LEAVE a protected block.
                        // we have to store it in a local and reload it outside of the try..catch.
                        _instructions->AppendStoreLocal(resultLocalIndex);
                    }
                },
                [&]()
                {
                    // pop the exception off of the stack
                    _instructions->Append(CEE_POP);

                    // the original code should end with a RET instruction
                    if (*(_oldCodeBytes.data() + _oldCodeBytes.size() - 1) == CEE_RET) {
                        *(_oldCodeBytes.data() + _oldCodeBytes.size() - 1) = CEE_NOP;
                    }
                    else {
                        LogError(L"Unexpected instruction in method ", _function->ToString());
                    }
                    _instructions->AppendUserCode(_oldCodeBytes);
                    if (_methodSignature->_returnType->_kind != SignatureParser::ReturnType::Kind::VOID_RETURN_TYPE)
                    {
                        _instructions->AppendStoreLocal(resultLocalIndex);
                    }
                }
            );

            if (_methodSignature->_returnType->_kind != SignatureParser::ReturnType::Kind::VOID_RETURN_TYPE)
            {
                _instructions->AppendLoadLocal(resultLocalIndex);
            }
            _instructions->Append(CEE_RET);
        }

    };
}}}
