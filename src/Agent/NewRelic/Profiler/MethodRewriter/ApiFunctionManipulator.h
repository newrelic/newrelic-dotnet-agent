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
                    if (_agentCallStrategy == AgentCallStyle::Strategy::InAgentCache)
                    {
                        // Ensure that the managed agent is loaded
                        _instructions->AppendString(_instrumentationSettings->GetCorePath());
                        _instructions->Append(_X("call void [") + _instructions->GetCoreLibAssemblyName() + _X("]System.CannotUnloadAppDomainException::EnsureInitialized(string)"));

                        // Get the Func holding a reference to the ProfilerAgentMethodCallCache.GetAndInvokeMethodFromCache method
                        _instructions->Append(_X("call object [") + _instructions->GetCoreLibAssemblyName() + _X("]System.CannotUnloadAppDomainException::GetMethodCacheLookupMethod()"));
                        _instructions->Append(CEE_CASTCLASS, _X("class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Func`7<string, string, string, class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type[], class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type, object[], object>"));

                        xstring_t className = _X("NewRelic.Agent.Core.AgentApi");
                        xstring_t methodName = _function->GetFunctionName();
                        xstring_t keyName = className + _X(".") + methodName + _X("_") + to_xstring((unsigned long)_function->GetFunctionId());
                        auto argumentTypesLambda = GetArrayOfTypeParametersLamdba();

                        _instructions->AppendString(keyName);
                        _instructions->AppendString(className);
                        _instructions->AppendString(methodName);

                        if (argumentTypesLambda == NULL)
                        {
                            _instructions->Append(CEE_LDNULL);
                        }
                        else
                        {
                            argumentTypesLambda();
                        }

                        _instructions->AppendTypeOfArgument(_methodSignature->_returnType);

                        BuildObjectArrayOfParameters();

                        _instructions->Append(CEE_CALLVIRT, _X("instance !6 class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Func`7<string, string, string, class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type[], class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type, object[], object>::Invoke(!0, !1, !2, !3, !4, !5)"));

                        if (_methodSignature->_returnType->_kind == SignatureParser::ReturnType::Kind::VOID_RETURN_TYPE)
                        {
                            _instructions->Append(_X("pop"));
                        }
                        else {
                            // we can't leave an object on the stack and CEE_LEAVE a protected block.
                            // we have to store it in a local and reload it outside of the try..catch.
                            _instructions->AppendStoreLocal(resultLocalIndex);
                        }
                    }
                    else
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
