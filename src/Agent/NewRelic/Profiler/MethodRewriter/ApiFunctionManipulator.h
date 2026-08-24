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
                    if (_agentCallStrategy == AgentCallStyle::Strategy::AppDomainFallbackCache)
                    {
                        BuildManagedInvokerCall();
                    }
                    else
                    {
                        BuildReflectionCall();
                    }

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

        // AppDomainFallbackCache: dispatch through the injected InvokeAgentMethodInvokerFunc
        // helper, which resolves a delegate cached per method on the managed side. This
        // replaces the generic MethodInfo getter, which called AppDomain.GetData on every
        // call. On .NET Core that maps to AppContext.GetData, whose store is guarded by a
        // single process-wide monitor, so every API call took that lock.
        void BuildManagedInvokerCall()
        {
            const xstring_t className = _X("NewRelic.Agent.Core.AgentApi");
            // The function id is a tie-breaker so overloads get distinct cache entries.
            const xstring_t keyName = className + _X(".") + _function->GetFunctionName() + _X("_") + to_xstring((unsigned long)_function->GetFunctionId());

            _instructions->AppendString(_instrumentationSettings->GetCorePath());
            _instructions->AppendString(keyName);
            _instructions->AppendString(className);
            _instructions->AppendString(_function->GetFunctionName());

            auto loadTypeParameters = GetArrayOfTypeParametersLamdba();
            loadTypeParameters();

            // Pushes a System.Type for the return type, or null when the method returns void.
            _instructions->AppendTypeOfArgument(_methodSignature->_returnType);

            BuildObjectArrayOfParameters();

            _instructions->Append(CEE_CALL, _X("object [") + _instructions->GetCoreLibAssemblyName() + _X("]System.CannotUnloadAppDomainException::InvokeAgentMethodInvokerFunc(string,string,string,string,class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type[],class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type,object[])"));
        }

        // Reflection: self-contained IL that resolves the target itself and dispatches
        // through MethodBase.Invoke. This is the graceful-degradation path used when core
        // library helper injection fails, so it must never call an injected helper.
        void BuildReflectionCall()
        {
            LoadMethodInfo(_instrumentationSettings->GetCorePath(), _X("NewRelic.Agent.Core.AgentApi"), _function->GetFunctionName(), GetArrayOfTypeParametersLamdba());

            _instructions->Append(_X("ldnull"));
            BuildObjectArrayOfParameters();

            _instructions->Append(_X("call   instance object [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Reflection.MethodBase::Invoke(object, object[])"));
        }

    };
}}}