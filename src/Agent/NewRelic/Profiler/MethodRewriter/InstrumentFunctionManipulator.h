/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once

#include "FunctionManipulator.h"
#include "InstrumentationSettings.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    // Re-writes methods so that the original method body is wrapped 
    // and calls are made to create and finish tracers.
    class InstrumentFunctionManipulator : FunctionManipulator
    {
    public:
        InstrumentFunctionManipulator(IFunctionPtr function, InstrumentationSettingsPtr instrumentationSettings, const bool isCoreClr, const AgentCallStyle::Strategy agentCallStrategy) :
            FunctionManipulator(function, isCoreClr, agentCallStrategy),
            _instrumentationSettings(instrumentationSettings),
            _tracerLocalIndex(0),
            _resultLocalIndex(0),
            _userExceptionLocalIndex(0)
        {
            if (_function->Preprocess()) {
                Initialize();
            }
        }

        // instrument this method with the usual stuff
        void InstrumentDefault(NewRelic::Profiler::Configuration::InstrumentationPointPtr instrumentationPoint)
        {
            BuildDefaultInstructions(instrumentationPoint);
            Instrument();
        }

    private:
        InstrumentationSettingsPtr _instrumentationSettings;
        uint16_t _tracerLocalIndex;
        uint16_t _resultLocalIndex;
        uint16_t _userExceptionLocalIndex;

        void BuildDefaultInstructions(Configuration::InstrumentationPointPtr instrumentationPoint)
        {
            // set the stack size required to handle these instructions (remember that we push all of this functions arguments onto the stack to recursively call)
            auto originalStackSize = GetHeader()->GetMaxStack();
            unsigned maxStackSize = std::max<unsigned>(std::max<unsigned>(originalStackSize, 20), unsigned(_methodSignature->_parameters->size() + 1));
            GetHeader()->SetMaxStack(maxStackSize);

            AppendDefaultLocals();
            InitializeLocalsToNull();

            SafeCallGetTracer(instrumentationPoint);

            // try {
            _instructions->AppendTryStart();

            // Inject the original method
            _instructions->AppendLabel(_X("user_code"));
            _instructions->AppendUserCode(_oldCodeBytes);

            if (_methodSignature->_returnType->_kind != SignatureParser::ReturnType::VOID_RETURN_TYPE)
                _instructions->AppendStoreLocal(_resultLocalIndex);

            // } catch (Exception exception) {
            auto afterOriginalMethodCatch = _instructions->AppendJump(CEE_LEAVE);
            _instructions->AppendTryEnd();
            _instructions->AppendCatchStart();

            // userException = exception;
            _instructions->AppendStoreLocal(_userExceptionLocalIndex);

            CallFinishTracerWithException();

            // throw
            _instructions->Append(_X("rethrow"));

            // } // end catch
            _instructions->AppendJump(afterOriginalMethodCatch, CEE_LEAVE);
            _instructions->AppendCatchEnd();
            _instructions->AppendLabel(afterOriginalMethodCatch);

            CallFinishTracerWithReturnValue();

            // return result;
            Return(_instructions, _methodSignature->_returnType, _resultLocalIndex);
        }

        // Invokes AgentShim.FinishTracer invoking the given argument lambdas to load the parameters
        // onto the stack.
        void CallFinishTracer(std::function<void()> loadTracerFunc, std::function<void()> loadReturnValueFunc, std::function<void()> loadExceptionFunc)
        {
            _instructions->AppendLoadLocal(_tracerLocalIndex);
            auto afterFinishLabel = _instructions->AppendJump(CEE_BRFALSE);

            TryCatch(
                [&]() 
                {
                    // directly invoke delegate to finish the tracer
                    loadTracerFunc();
                    _instructions->Append(_X("castclass  class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Action`2<object, class[") + _instructions->GetCoreLibAssemblyName() + _X("]System.Exception>"));
                    loadReturnValueFunc();
                    loadExceptionFunc();
                    _instructions->Append(CEE_CALLVIRT, _X("instance void [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Action`2<object,class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Exception>::Invoke(!0,!1)"));
                },
                [&]() { _instructions->Append(CEE_POP); }
            );

            _instructions->AppendLabel(afterFinishLabel);
        }

        void CallFinishTracerWithReturnValue()
        {
            std::function<void()> returnValueDelegate;
            if (_methodSignature->_returnType->_kind == SignatureParser::ReturnType::VOID_RETURN_TYPE)
            {
                returnValueDelegate = [&]() { _instructions->Append(CEE_LDNULL); };
            }
            else
            {
                returnValueDelegate = [&]() { _instructions->AppendLoadLocalAndBox(_resultLocalIndex, _methodSignature->_returnType); };
            }
            CallFinishTracer(
                [&]() { _instructions->AppendLoadLocal(_tracerLocalIndex); },
                returnValueDelegate,
                [&]() { _instructions->Append(CEE_LDNULL); }
            );
        }

        void CallFinishTracerWithException() {

            CallFinishTracer(
                [&]() { _instructions->AppendLoadLocal(_tracerLocalIndex); },
                [&]() { _instructions->Append(CEE_LDNULL); },
                [&]() { _instructions->AppendLoadLocal(_userExceptionLocalIndex); }
            );
        }

        // Call GetTracer within a try..catch block
        void SafeCallGetTracer(Configuration::InstrumentationPointPtr instrumentationPoint)
        {
            TryCatch(
                [&]() { CallGetTracer(instrumentationPoint); },
                [&]() { _instructions->Append(CEE_POP); }
            );
        }

        void InitializeLocalsToNull()
        {
            // Object tracer = null;
            _instructions->Append(_X("ldnull"));
            _instructions->AppendStoreLocal(_tracerLocalIndex);
            // Exception userException = null;
            _instructions->Append(_X("ldnull"));
            _instructions->AppendStoreLocal(_userExceptionLocalIndex);
        }

        void CallGetTracer(NewRelic::Profiler::Configuration::InstrumentationPointPtr instrumentationPoint)
        {
            if (_agentCallStrategy == AgentCallStyle::Strategy::InAgentCache)
            {
                // Ensure that the managed agent is loaded
                _instructions->AppendString(_instrumentationSettings->GetCorePath());
                _instructions->Append(_X("call void [") + _instructions->GetCoreLibAssemblyName() + _X("]System.CannotUnloadAppDomainException::EnsureInitialized(string)"));

                // Call the agent shim delegate

                // Get the Func holding a reference to NewRelic.Agent.Core.AgentShim.GetFinishTracerDelegate
                _instructions->Append(_X("call object [") + _instructions->GetCoreLibAssemblyName() + _X("]System.CannotUnloadAppDomainException::GetAgentShimFinishTracerDelegateMethod()"));
                _instructions->Append(CEE_CASTCLASS, _X("class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Func`12<string, uint32, string, string, class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type, string, string, string, object, object[], uint64, class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Action`2<object, class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Exception>>"));

                _instructions->Append(_X("ldstr      ") + instrumentationPoint->TracerFactoryName);

                _instructions->Append(CEE_LDC_I4, instrumentationPoint->TracerFactoryArgs);

                _instructions->Append(_X("ldstr      ") + instrumentationPoint->MetricName);

                _instructions->Append(_X("ldstr      ") + _function->GetAssemblyName());

                _instructions->Append(CEE_LDTOKEN, _function->GetTypeToken());
                _instructions->Append(_X("call class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type::GetTypeFromHandle(valuetype [") + _instructions->GetCoreLibAssemblyName() + _X("]System.RuntimeTypeHandle)"));

                _instructions->Append(_X("ldstr      ") + _function->GetTypeName());

                _instructions->Append(_X("ldstr      ") + _function->GetFunctionName());

                // pass the stringified method signature to GetTracer
                auto signatureString = _methodSignature->ToString(_function->GetTokenResolver());
                _instructions->Append(_X("ldstr      ") + signatureString);

                if (_methodSignature->_hasThis) _instructions->AppendLoadArgument(0);
                else _instructions->Append(_X("ldnull"));

                // turn the parameters passed into this method into an object array
                BuildObjectArrayOfParameters();

                // It's important to upcast the function id here.  It's an int on WIN32
                _instructions->Append(CEE_LDC_I8, (uint64_t)_function->GetFunctionId());

                // make the call to GetFinishTracerDelegate
                _instructions->Append(CEE_CALLVIRT, _X("instance !11 class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Func`12<string, uint32, string, string, class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type, string, string, string, object, object[], uint64, class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Action`2<object, class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Exception>>::Invoke(!0, !1, !2, !3, !4, !5, !6, !7, !8, !9, !10)"));
            }
            else
            {
                LoadMethodInfo(_instrumentationSettings->GetCorePath(), _X("NewRelic.Agent.Core.AgentShim"), _X("GetFinishTracerDelegate"), 0, nullptr);

                // tracer = delegates[0].Invoke(null, new object[] { tracerFactoryName, tracerFactoryArgs, metricName, assemblyName, type, typeName, functionName, argumentSignatureString, this, new object[], functionId });
                _instructions->Append(_X("ldnull"));
                _instructions->Append(_X("ldc.i4.s   11"));
                _instructions->Append(_X("newarr     [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Object"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.0"));
                _instructions->Append(_X("ldstr      ") + instrumentationPoint->TracerFactoryName);
                _instructions->Append(_X("stelem.ref"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.1"));
                _instructions->Append(CEE_LDC_I4, instrumentationPoint->TracerFactoryArgs);
                _instructions->Append(_X("box [") + _instructions->GetCoreLibAssemblyName() + _X("]System.UInt32"));
                _instructions->Append(_X("stelem.ref"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.2"));
                _instructions->Append(_X("ldstr      ") + instrumentationPoint->MetricName);
                _instructions->Append(_X("stelem.ref"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.3"));
                _instructions->Append(_X("ldstr      ") + _function->GetAssemblyName());
                _instructions->Append(_X("stelem.ref"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.4"));
                _instructions->Append(CEE_LDTOKEN, _function->GetTypeToken());
                _instructions->Append(_X("call class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Type::GetTypeFromHandle(valuetype [") + _instructions->GetCoreLibAssemblyName() + _X("]System.RuntimeTypeHandle)"));
                _instructions->Append(_X("stelem.ref"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.5"));
                _instructions->Append(_X("ldstr      ") + _function->GetTypeName());
                _instructions->Append(_X("stelem.ref"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.6"));
                _instructions->Append(_X("ldstr      ") + _function->GetFunctionName());
                _instructions->Append(_X("stelem.ref"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.7"));
                // pass the stringified method signature to GetTracer
                auto signatureString = _methodSignature->ToString(_function->GetTokenResolver());
                _instructions->Append(_X("ldstr      ") + signatureString);
                _instructions->Append(_X("stelem.ref"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.8"));
                if (_methodSignature->_hasThis) _instructions->AppendLoadArgument(0);
                else _instructions->Append(_X("ldnull"));
                _instructions->Append(_X("stelem.ref"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.s 9"));
                // turn the parameters passed into this method into an object array
                BuildObjectArrayOfParameters();
                _instructions->Append(_X("stelem.ref"));
                _instructions->Append(_X("dup"));
                _instructions->Append(_X("ldc.i4.s 10"));
                // It's important to upcast the function id here.  It's an int on WIN32
                _instructions->Append(CEE_LDC_I8, (uint64_t)_function->GetFunctionId());
                _instructions->Append(_X("box [") + _instructions->GetCoreLibAssemblyName() + _X("]System.UInt64"));
                _instructions->Append(_X("stelem.ref"));
                // make the call to GetTracer
                InvokeMethodInfo();
            }

            _instructions->AppendStoreLocal(_tracerLocalIndex);
        }

        void AppendDefaultLocals()
        {
            LogTrace(_function->ToString() + _X(": Generating locals for default instrumentation."));
            auto tokenizer = _function->GetTokenizer();
            _tracerLocalIndex = AppendToLocalsSignature(_X("class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Object"), tokenizer, _newLocalVariablesSignature);
            _userExceptionLocalIndex = AppendToLocalsSignature(_X("class [") + _instructions->GetCoreLibAssemblyName() + _X("]System.Exception"), tokenizer, _newLocalVariablesSignature);
            
            if (_methodSignature->_returnType->_kind != SignatureParser::ReturnType::Kind::VOID_RETURN_TYPE)
                _resultLocalIndex = AppendReturnTypeLocal(_newLocalVariablesSignature, _methodSignature);
        }
    };
}}}
