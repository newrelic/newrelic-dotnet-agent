/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once

#include "FunctionManipulator.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    // This thing writes the helper methods that we add to
    // System.Type System.CannotUnloadAppDomainException
    class HelperFunctionManipulator : FunctionManipulator
    {
    public:
        HelperFunctionManipulator(IFunctionPtr function, const bool isCoreClr, const AgentCallStyle::Strategy agentCallStrategy) :
            FunctionManipulator(function, isCoreClr, agentCallStrategy)
        {
            Initialize();
        }

        // instrument this method with the mscorlib helper stuff
        void InstrumentHelper()
        {
            if (_function->GetFunctionName() == _X("LoadAssemblyOrThrow"))
            {
                BuildLoadAssemblyOrThrow();
            }
            else if (_function->GetFunctionName() == _X("GetTypeViaReflectionOrThrow"))
            {
                BuildGetTypeViaReflectionOrThrow();
            }
            else if (_function->GetFunctionName() == _X("GetMethodViaReflectionOrThrow"))
            {
                BuildGetMethodViaReflectionOrThrow();
            }
            else if (_function->GetFunctionName() == _X("StoreMethodInAppDomainStorageOrThrow"))
            {
                BuildStoreMethodInAppDomainStorageOrThrow();
            }
            else if (_function->GetFunctionName() == _X("GetMethodFromAppDomainStorage"))
            {
                BuildGetMethodFromAppDomainStorage();
            }
            else if (_function->GetFunctionName() == _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"))
            {
                BuildGetMethodFromAppDomainStorageOrReflectionOrThrow();
            }
            else if (_function->GetFunctionName() == _X("GetAgentShimMethodFromAppDomainStorageOrReflectionOrThrow"))
            {
                BuildGetAgentShimMethodFromAppDomainStorageOrReflectionOrThrow();
            }
            else if (_function->GetFunctionName() == _X("EnsureInitialized"))
            {
                BuildEnsureInitializedMethod();
            }
            else if (_function->GetFunctionName() == _X("GetAgentMethodInvokerObject"))
            {
                BuildGetAgentMethodInvokerObject();
            }
            else if (_function->GetFunctionName() == _X("GetAgentShimFinishTracerDelegateFunc"))
            {
                BuildGetAgentShimFinishTracerDelegateFunc();
            }
            else if (_function->GetFunctionName() == _X("InvokeAgentMethodInvokerFunc"))
            {
                BuildInvokeAgentMethodInvokerFunc();
            }
            else if (_function->GetFunctionName() == _X("StoreAgentMethodInvokerFunc"))
            {
                BuildStoreAgentMethodInvokerFunc();
            }
            else if (_function->GetFunctionName() == _X("StoreAgentShimFinishTracerDelegateFunc"))
            {
                BuildStoreAgentShimFinishTracerDelegateFunc();
            }
            else if (_function->GetFunctionName() == _X(".cctor"))
            {
                BuildStaticCtor();
            }
            else
            {
                LogError(L"Attempted to instrument an unknown helper method in ", _function->GetAssemblyName());
                return;
            }
            InstrumentTiny();
        }
    private:

        // System.Reflection.Assembly LoadAssemblyOrThrow(String assemblyPath)
        void BuildLoadAssemblyOrThrow()
        {
            _instructions->Append(CEE_LDARG_0);
            _instructions->Append(CEE_CALL, _X("class System.Reflection.Assembly System.Reflection.Assembly::LoadFrom(string)"));
            ThrowExceptionIfStackItemIsNull(_instructions, _X("Failed to load assembly."), true);
            _instructions->Append(CEE_RET);
        }

        // System.Type GetTypeViaReflectionOrThrowWithoutTypeParameters(String assemblyPath, String typeName)
        void BuildGetTypeViaReflectionOrThrow()
        {
            _instructions->Append(CEE_LDARG_0);
            _instructions->Append(CEE_CALL, _X("class System.Reflection.Assembly System.CannotUnloadAppDomainException::LoadAssemblyOrThrow(string)"));
            _instructions->Append(CEE_LDARG_1);
            _instructions->Append(CEE_CALLVIRT, _X("instance class System.Type System.Reflection.Assembly::GetType(string)"));
            ThrowExceptionIfStackItemIsNull(_instructions, _X("Failed to load type from assembly via reflection."), true);
            _instructions->Append(CEE_RET);
        }

        // System.Reflection.MethodInfo GetMethodViaReflectionOrThrow(String assemblyName, String typeName, String methodName, System.Type[] methodParameterTypes)
        void BuildGetMethodViaReflectionOrThrow()
        {
            _instructions->Append(CEE_LDARG_0);
            _instructions->Append(CEE_LDARG_1);
            _instructions->Append(CEE_CALL, _X("class System.Type System.CannotUnloadAppDomainException::GetTypeViaReflectionOrThrow(string,string)"));
            _instructions->Append(CEE_LDARG_2);
            _instructions->Append(CEE_LDARG_3);

            // if (methodParameterTypes != null)
            auto typeArrayIsNullLabel = _instructions->AppendJump(CEE_BRFALSE);
            {
                _instructions->Append(CEE_LDARG_3);
                _instructions->Append(CEE_CALLVIRT, _X("instance class System.Reflection.MethodInfo System.Type::GetMethod(string,class System.Type[])"));
                _instructions->AppendJump(CEE_BR, _X("after_GetMethod"));
            }
            // else
            _instructions->AppendLabel(typeArrayIsNullLabel);
            {
                _instructions->Append(CEE_CALLVIRT, _X("instance class System.Reflection.MethodInfo System.Type::GetMethod(string)"));
                _instructions->AppendJump(CEE_BR, _X("after_GetMethod"));
            }
            _instructions->AppendLabel(_X("after_GetMethod"));

            ThrowExceptionIfStackItemIsNull(_instructions, _X("Failed to load method from type via reflection."), true);
            _instructions->Append(CEE_RET);
        }

        // We can't directly inject calls to AppDomain.GetData and SetData in many cases
        // because of security permissions.  The methods added onto an mscorlib exception
        // work around this.
        //
        // void StoreMethodInAppDomainStorageOrThrow(MethodInfo method, String storageKey)
        // void StoreMethodInAppDomainStorageOrThrow(object method, String storageKey)
        void BuildStoreMethodInAppDomainStorageOrThrow()
        {
            _instructions->Append(CEE_CALL, _X("class System.AppDomain System.AppDomain::get_CurrentDomain()"));
            ThrowExceptionIfStackItemIsNull(_instructions, _X("System.AppDomain.CurrentDomain == null."), true);
            _instructions->Append(CEE_LDARG_1);
            _instructions->Append(CEE_LDARG_0);
            _instructions->Append(CEE_CALLVIRT, _X("instance void System.AppDomain::SetData(string, object)"));
            _instructions->Append(CEE_RET);
        }

        // MethodInfo GetMethodFromAppDomainStorage(String storageKey)
        void BuildGetMethodFromAppDomainStorage()
        {
            _instructions->Append(CEE_CALL, _X("class System.AppDomain System.AppDomain::get_CurrentDomain()"));
            ThrowExceptionIfStackItemIsNull(_instructions, _X("System.AppDomain.CurrentDomain == null."), true);
            _instructions->Append(CEE_LDARG_0);
            _instructions->Append(CEE_CALLVIRT, _X("instance object System.AppDomain::GetData(string)"));
            _instructions->Append(CEE_CASTCLASS, _X("class System.Reflection.MethodInfo"));
            _instructions->Append(CEE_RET);
        }

        // MethodInfo GetMethodFromAppDomainStorageOrReflectionOrThrow(String storageKey, String assemblyPath, String typeName, String methodName, Type[] methodParameters)
        void BuildGetMethodFromAppDomainStorageOrReflectionOrThrow()
        {
            _instructions->Append(CEE_LDARG_0);
            _instructions->Append(CEE_CALL, _X("class System.Reflection.MethodInfo System.CannotUnloadAppDomainException::GetMethodFromAppDomainStorage(string)"));

            _instructions->Append(CEE_DUP);
            auto methodEnd = _instructions->AppendJump(CEE_BRTRUE);

            _instructions->Append(CEE_POP);
            _instructions->Append(CEE_LDARG_1);
            _instructions->Append(CEE_LDARG_2);
            _instructions->Append(CEE_LDARG_3);
            _instructions->Append(CEE_LDARG_S, (uint8_t)4);
            _instructions->Append(CEE_CALL, _X("class System.Reflection.MethodInfo System.CannotUnloadAppDomainException::GetMethodViaReflectionOrThrow(string,string,string,class System.Type[])"));
            _instructions->Append(CEE_DUP);
            _instructions->Append(CEE_LDARG_0);
            //_instructions->Append(CEE_CALL, _X("void System.CannotUnloadAppDomainException::StoreMethodInAppDomainStorageOrThrow(class System.Reflection.MethodInfo,string)"));
            _instructions->Append(CEE_CALL, _X("void System.CannotUnloadAppDomainException::StoreMethodInAppDomainStorageOrThrow(object,string)"));

            _instructions->AppendLabel(methodEnd);
            _instructions->Append(CEE_RET);
        }

        void BuildGetAgentShimMethodFromAppDomainStorageOrReflectionOrThrow()
        {
            _instructions->Append(CEE_LDSFLD, _X("object __NRInitializer__::_agentShimMethodInfo"));

            _instructions->Append(CEE_DUP);
            auto afterFallback = _instructions->AppendJump(CEE_BRTRUE);

            _instructions->Append(CEE_POP); // Pops the previous null value
            _instructions->Append(CEE_LDARG_0);
            _instructions->Append(CEE_LDARG_1);
            _instructions->Append(CEE_LDARG_2);
            _instructions->Append(CEE_LDARG_3);
            _instructions->Append(CEE_LDARG_S, (uint8_t)4);
            _instructions->Append(CEE_CALL, _X("class System.Reflection.MethodInfo System.CannotUnloadAppDomainException::GetMethodFromAppDomainStorageOrReflectionOrThrow(string,string,string,string,class System.Type[])"));

            _instructions->Append(CEE_DUP);
            _instructions->Append(CEE_STSFLD, _X("object __NRInitializer__::_agentShimMethodInfo"));

            _instructions->AppendLabel(afterFallback);

            _instructions->Append(CEE_RET);
        }

        //void BuildGetAgentInvokerMethodFromAppDomainStorageOrReflectionOrThrow()
        //{
        //    _instructions->Append(_X("call object System.CannotUnloadAppDomainException::GetAgentMethodInvokerObject()"));

        //    _instructions->Append(CEE_DUP);
        //    auto afterFallback = _instructions->AppendJump(CEE_BRTRUE);

        //    _instructions->Append(CEE_POP); // Pops the previous null value
        //    _instructions->AppendString(_X("object __NRInitializer__::_agentMethodFunc"));
        //    _instructions->AppendString(_X("NewRelic.Agent.Core.ProfilerAgentMethodInvoker"));
        //    _instructions->AppendString(_X("GetInvoker"));
        //    _instructions->Append(CEE_LDNULL);
        //    _instructions->Append(CEE_CALL, _X("class System.Reflection.MethodInfo System.CannotUnloadAppDomainException::GetMethodViaReflectionOrThrow(string,string,string,class System.Type[])"));

        //    _instructions->Append(CEE_LDNULL);
        //    _instructions->Append(CEE_LDNULL);
        //    _instructions->Append(CEE_CALLVIRT, _X("instance object System.Reflection.MethodBase::Invoke(object, object[])"));

        //    _instructions->Append(CEE_DUP);
        //    _instructions->Append(CEE_STSFLD, _X("object __NRInitializer__::_agentMethodFunc"));
        //    _instructions->AppendString(_X("__NRInitializer__::_agentMethodFunc"));
        //    _instructions->Append(CEE_CALL, _X("void System.CannotUnloadAppDomainException::StoreMethodInAppDomainStorageOrThrow(object,string)"));
        //    _instructions->Append(CEE_DUP);
        //    _instructions->Append(CEE_STSFLD, _X("object __NRInitializer__::_agentMethodFunc"));

        //    _instructions->AppendLabel(afterFallback);

        //    _instructions->Append(CEE_RET);
        //}

        // resultObject InvokeAgentMethodInvokerFunc(string assemblyPath, string storageKey, string typeName, string methodName, Type[] parameterTypes, Type returnType, object[] methodParameters)
        void BuildInvokeAgentMethodInvokerFunc()
        {
            if (_agentCallStrategy != AgentCallStyle::Strategy::AppDomainFallbackCache)
            {
                _instructions->Append(CEE_LDARG_0);
                _instructions->Append(_X("call void System.CannotUnloadAppDomainException::EnsureInitialized(string)"));
            }

            _instructions->Append(_X("call object System.CannotUnloadAppDomainException::GetAgentMethodInvokerObject()"));

            _instructions->Append(CEE_CASTCLASS, _X("class System.Func`7<string, string, string, class System.Type[], class System.Type, object[], object>"));
            _instructions->Append(CEE_LDARG_1);
            _instructions->Append(CEE_LDARG_2);
            _instructions->Append(CEE_LDARG_3);
            _instructions->Append(CEE_LDARG_S, (uint8_t)4);
            _instructions->Append(CEE_LDARG_S, (uint8_t)5);
            _instructions->Append(CEE_LDARG_S, (uint8_t)6);
            _instructions->Append(CEE_CALLVIRT, _X("instance !6 class System.Func`7<string, string, string, class System.Type[], class System.Type, object[], object>::Invoke(!0, !1, !2, !3, !4, !5)"));

            _instructions->Append(CEE_RET);
        }

        void BuildGetAgentMethodInvokerObject()
        {
            //_instructions->Append(CEE_VOLATILE);
            //_instructions->Append(CEE_LDSFLD, _X("object modreq(System.Runtime.CompilerServices.IsVolatile) __NRInitializer__::_agentMethodFunc"));
            _instructions->Append(CEE_LDSFLD, _X("object __NRInitializer__::_agentMethodFunc"));

            _instructions->Append(CEE_DUP);
            auto afterFallback = _instructions->AppendJump(CEE_BRTRUE);

            _instructions->Append(CEE_POP); // Pops the previous null value
            _instructions->Append(CEE_CALL, _X("class System.AppDomain System.AppDomain::get_CurrentDomain()"));
            ThrowExceptionIfStackItemIsNull(_instructions, _X("System.AppDomain.CurrentDomain == null."), true);
            _instructions->AppendString(_X("__NRInitializer__::_agentMethodFunc"));
            _instructions->Append(CEE_CALLVIRT, _X("instance object System.AppDomain::GetData(string)"));

            _instructions->AppendLabel(afterFallback);

            _instructions->Append(CEE_RET);
        }

        void BuildGetAgentShimFinishTracerDelegateFunc()
        {
            //_instructions->Append(CEE_VOLATILE);
            //_instructions->Append(CEE_LDSFLD, _X("object modreq(System.Runtime.CompilerServices.IsVolatile) __NRInitializer__::_agentShimFunc"));
            _instructions->Append(CEE_LDSFLD, _X("object __NRInitializer__::_agentShimFunc"));

            //_instructions->AppendString(_X("^^^^^checking agentshim"));
            //_instructions->Append(CEE_CALL, _X("void System.Console::WriteLine(string)"));

            _instructions->Append(CEE_DUP);
            auto afterFallback = _instructions->AppendJump(CEE_BRTRUE);

            //_instructions->AppendString(_X("^^^^^appdomain fallback"));
            //_instructions->Append(CEE_CALL, _X("void System.Console::WriteLine(string)"));

            _instructions->Append(CEE_POP); // Pops the previous null value
            _instructions->Append(CEE_CALL, _X("class System.AppDomain System.AppDomain::get_CurrentDomain()"));
            ThrowExceptionIfStackItemIsNull(_instructions, _X("System.AppDomain.CurrentDomain == null."), true);
            _instructions->AppendString(_X("__NRInitializer__::_agentShimFunc"));
            _instructions->Append(CEE_CALLVIRT, _X("instance object System.AppDomain::GetData(string)"));

            _instructions->AppendLabel(afterFallback);

            _instructions->Append(CEE_RET);
        }

        void BuildStoreAgentMethodInvokerFunc()
        {
            _instructions->Append(CEE_LDARG_0);
            _instructions->AppendString(_X("NewRelic.Agent.Core.ProfilerAgentMethodInvoker"));
            _instructions->AppendString(_X("GetInvoker"));
            _instructions->Append(CEE_LDNULL);
            _instructions->Append(CEE_CALL, _X("class System.Reflection.MethodInfo System.CannotUnloadAppDomainException::GetMethodViaReflectionOrThrow(string,string,string,class System.Type[])"));

            _instructions->Append(CEE_LDNULL);
            _instructions->Append(CEE_LDNULL);
            _instructions->Append(CEE_CALLVIRT, _X("instance object System.Reflection.MethodBase::Invoke(object, object[])"));

            //_instructions->Append(CEE_VOLATILE);
            //_instructions->Append(CEE_STSFLD, _X("object modreq(System.Runtime.CompilerServices.IsVolatile) __NRInitializer__::_agentMethodFunc"));
            _instructions->Append(CEE_DUP);
            _instructions->Append(CEE_STSFLD, _X("object __NRInitializer__::_agentMethodFunc"));
            _instructions->AppendString(_X("__NRInitializer__::_agentMethodFunc"));
            _instructions->Append(CEE_CALL, _X("void System.CannotUnloadAppDomainException::StoreMethodInAppDomainStorageOrThrow(object,string)"));
            _instructions->Append(CEE_RET);
        }

        void BuildStoreAgentShimFinishTracerDelegateFunc()
        {
            _instructions->Append(CEE_LDARG_0);
            _instructions->AppendString(_X("NewRelic.Agent.Core.AgentShim"));
            _instructions->AppendString(_X("GetFinishTracerDelegateFunc"));
            _instructions->Append(CEE_LDNULL);
            _instructions->Append(CEE_CALL, _X("class System.Reflection.MethodInfo System.CannotUnloadAppDomainException::GetMethodViaReflectionOrThrow(string,string,string,class System.Type[])"));

            _instructions->Append(CEE_LDNULL);
            _instructions->Append(CEE_LDNULL);
            _instructions->Append(CEE_CALLVIRT, _X("instance object System.Reflection.MethodBase::Invoke(object, object[])"));

            //_instructions->Append(CEE_VOLATILE);
            //_instructions->Append(CEE_STSFLD, _X("object  modreq(System.Runtime.CompilerServices.IsVolatile)__NRInitializer__::_agentShimFunc"));
            _instructions->Append(CEE_DUP);
            _instructions->Append(CEE_STSFLD, _X("object __NRInitializer__::_agentShimFunc"));
            _instructions->AppendString(_X("__NRInitializer__::_agentShimFunc"));
            _instructions->Append(CEE_CALL, _X("void System.CannotUnloadAppDomainException::StoreMethodInAppDomainStorageOrThrow(object,string)"));
            _instructions->Append(CEE_RET);
        }

        void BuildEnsureInitializedMethod()
        {
            //_instructions->Append(CEE_LDSFLDA, _X("int32 __NRInitializer__::_isInitialized"));
            //_instructions->Append(_X("call void System.Threading.Interlocked::MemoryBarrier()"));
            //_instructions->Append(CEE_LDSFLDA, _X("object __NRInitializer__::_agentMethodFunc"));
            //_instructions->Append(CEE_LDNULL);
            //_instructions->Append(CEE_LDNULL);
            //_instructions->Append(CEE_LDC_I4_1);
            //_instructions->Append(CEE_LDC_I4_0);
            //_instructions->Append(_X("call object System.Threading.Interlocked::CompareExchange(object&, object, object)"));
            //_instructions->Append(CEE_VOLATILE);
            //_instructions->Append(CEE_LDSFLD, _X("object modreq(System.Runtime.CompilerServices.IsVolatile) __NRInitializer__::_agentMethodFunc"));

            //_instructions->Append(CEE_LDSFLD, _X("object __NRInitializer__::_agentMethodFunc"));
            _instructions->Append(_X("call object System.CannotUnloadAppDomainException::GetAgentMethodInvokerObject()"));
            auto afterInit = _instructions->AppendJump(CEE_BRTRUE);

            _instructions->Append(CEE_LDARG_0);
            _instructions->Append(_X("call void System.CannotUnloadAppDomainException::StoreAgentShimFinishTracerDelegateFunc(string)"));

            _instructions->Append(CEE_LDARG_0);
            _instructions->Append(_X("call void System.CannotUnloadAppDomainException::StoreAgentMethodInvokerFunc(string)"));

            //_instructions->Append(CEE_LDSFLDA, _X("int32 __NRInitializer__::_isInitialized"));
            //_instructions->Append(CEE_LDC_I4_1);
            //_instructions->Append(CEE_LDC_I4_0);
            //_instructions->Append(_X("call int32 System.Threading.Interlocked::CompareExchange(int32&, int32, int32)"));
            //_instructions->Append(CEE_POP);
            //call int32 [System.Threading]System.Threading.Interlocked::CompareExchange(int32&, int32, int32)
            //_instructions->Append(CEE_STSFLD, _X("int32 __NRInitializer__::_isInitialized"));

            //_instructions->Append(_X("call void System.GC::Collect()"));

            _instructions->AppendLabel(afterInit);

            _instructions->Append(CEE_RET);

        }

        void BuildStaticCtor()
        {
            _instructions->Append(CEE_RET);
        }
    };
}}}
