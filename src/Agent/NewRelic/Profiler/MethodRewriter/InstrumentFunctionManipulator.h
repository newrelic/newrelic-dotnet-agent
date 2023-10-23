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
        InstrumentFunctionManipulator(IFunctionPtr function, InstrumentationSettingsPtr instrumentationSettings, const bool isCoreClr) : 
            FunctionManipulator(function, isCoreClr), 
            _instrumentationSettings(instrumentationSettings)
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
            unsigned maxStackSize = std::max<unsigned>(std::max<unsigned>(originalStackSize, 10), unsigned(_methodSignature->_parameters->size() + 1));
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
                    if (_isCoreClr)
                    {
                        _instructions->Append(_X("castclass  class [System.Private.CoreLib]System.Action`2<object,class [mscorlib]System.Exception>"));
                    }
                    else
                    {
                        _instructions->Append(_X("castclass  class [mscorlib]System.Action`2<object,class [mscorlib]System.Exception>"));
                    }
                    loadReturnValueFunc();
                    loadExceptionFunc();
                    if (_isCoreClr)
                    {
                        _instructions->Append(CEE_CALLVIRT, _X("instance void [System.Private.CoreLib]System.Action`2<object,class [System.Private.CoreLib]System.Exception>::Invoke(!0,!1)"));
                    }
                    else
                    {
                        _instructions->Append(CEE_CALLVIRT, _X("instance void [mscorlib]System.Action`2<object,class [mscorlib]System.Exception>::Invoke(!0,!1)"));
                    }
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
            LoadMethodInfo(_instrumentationSettings->GetCorePath(), _X("NewRelic.Agent.Core.AgentShim"), _X("GetFinishTracerDelegate"), 0, nullptr, true);
              
            // tracer = delegates[0].Invoke(null, new object[] { tracerFactoryName, tracerFactoryArgs, metricName, assemblyName, type, typeName, functionName, argumentSignatureString, this, new object[], functionId });
            _instructions->Append(_X("ldnull"));
            _instructions->Append(_X("ldc.i4.s   11"));
            if (_isCoreClr)
            {
                _instructions->Append(_X("newarr     [System.Private.CoreLib]System.Object"));
            }
            else
            {
                _instructions->Append(_X("newarr     [mscorlib]System.Object"));
            }
            _instructions->Append(_X("dup"));
            _instructions->Append(_X("ldc.i4.0"));
            _instructions->Append(_X("ldstr      ") + instrumentationPoint->TracerFactoryName);
            _instructions->Append(_X("stelem.ref"));
            _instructions->Append(_X("dup"));
            _instructions->Append(_X("ldc.i4.1"));
            _instructions->Append(CEE_LDC_I4, instrumentationPoint->TracerFactoryArgs);
            if (_isCoreClr)
            {
                _instructions->Append(_X("box [mscorlib]System.UInt32"));
            }
            else
            {
                _instructions->Append(_X("box [mscorlib]System.UInt32"));
            }
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
            if (_isCoreClr)
            {
                _instructions->Append(_X("call class [System.Private.CoreLib]System.Type [System.Private.CoreLib]System.Type::GetTypeFromHandle(valuetype [System.Private.CoreLib]System.RuntimeTypeHandle)"));
            }
            else
            {
                _instructions->Append(_X("call class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)"));
            }
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
            if (_isCoreClr)
            {
                _instructions->Append(_X("box [System.Private.CoreLib]System.UInt64"));
            }
            else
            {
                _instructions->Append(_X("box [mscorlib]System.UInt64"));
            }
            _instructions->Append(_X("stelem.ref"));
            // make the call to GetTracer
            InvokeMethodInfo();

            _instructions->AppendStoreLocal(_tracerLocalIndex);
        }

        void AppendDefaultLocals()
        {
            LogTrace(_function->ToString() + _X(": Generating locals for default instrumentation."));
            auto tokenizer = _function->GetTokenizer();
            if (_isCoreClr)
            {
                _tracerLocalIndex = AppendToLocalsSignature(_X("class [System.Private.CoreLib]System.Object"), tokenizer, _newLocalVariablesSignature);
                _userExceptionLocalIndex = AppendToLocalsSignature(_X("class [System.Private.CoreLib]System.Exception"), tokenizer, _newLocalVariablesSignature);
            }
            else
            {
                _tracerLocalIndex = AppendToLocalsSignature(_X("class [mscorlib]System.Object"), tokenizer, _newLocalVariablesSignature);
                _userExceptionLocalIndex = AppendToLocalsSignature(_X("class [mscorlib]System.Exception"), tokenizer, _newLocalVariablesSignature);
            }
            
            if (_methodSignature->_returnType->_kind != SignatureParser::ReturnType::Kind::VOID_RETURN_TYPE)
                _resultLocalIndex = AppendReturnTypeLocal(_newLocalVariablesSignature, _methodSignature);
        }
    };
}}}
