/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <array>
#include "../Common/CorStandIn.h"
#include "../Common/Strings.h"
#include "../Logging/Logger.h"
#include "../Profiler/Exceptions.h"
#include "../Sicily/Sicily.h"
#include "IModule.h"

namespace NewRelic { namespace Profiler { namespace ModuleInjector
{
    class ModuleInjector
    {
    private:
        struct ManagedMethodToInject
        {
            //null terminated character string NTCS
            using ntcs_t = const xchar_t* const;
            ntcs_t TypeName;
            ntcs_t MethodName;
            ntcs_t ReturnType;
            ntcs_t ParameterTypes;
            constexpr ManagedMethodToInject(ntcs_t typeName, ntcs_t methodName, ntcs_t returnType, ntcs_t parameterTypes) :
                TypeName(typeName), MethodName(methodName), ReturnType(returnType), ParameterTypes(parameterTypes)
            {}
        };

    public:
        static void InjectIntoModule(IModule& module, const bool isCoreClr)
        {
            // When injecting method REFERENCES into an assembly, theses references should have
            // the external assembly identifier to System.Private.CoreLib
            constexpr std::array<ManagedMethodToInject, 12> methodReferencesToInjectCoreClr{
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("LoadAssemblyOrThrow"), _X("class [System.Private.CoreLib]System.Reflection.Assembly"), _X("string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetTypeViaReflectionOrThrow"), _X("class [System.Private.CoreLib]System.Type"), _X("string,string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetMethodViaReflectionOrThrow"), _X("class [System.Private.CoreLib]System.Reflection.MethodInfo"), _X("string,string,string,class [System.Private.CoreLib]System.Type[]")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorage"), _X("class [System.Private.CoreLib]System.Reflection.MethodInfo"), _X("string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"), _X("class [System.Private.CoreLib]System.Reflection.MethodInfo"), _X("string,string,string,string,class [System.Private.CoreLib]System.Type[]")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("StoreMethodInAppDomainStorageOrThrow"), _X("void"), _X("class [System.Private.CoreLib]System.Reflection.MethodInfo,string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("StoreMethodCacheLookupMethod"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("StoreAgentShimFinishTracerDelegateMethod"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"),_X("EnsureInitialized"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetMethodInfoFromAgentCache"), _X("class [System.Private.CoreLib]System.Reflection.MethodInfo"), _X("string,string,string,string,class [System.Private.CoreLib]System.Type[]")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetMethodCacheLookupMethod"), _X("object"), _X("")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetAgentShimFinishTracerDelegateMethod"), _X("object"), _X(""))
            };

            // When injecting method REFERENCES into an assembly, theses references should have
            // the external assembly identifier to mscorlib
            constexpr std::array<ManagedMethodToInject, 12> methodReferencesToInjectNetFramework{
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("LoadAssemblyOrThrow"), _X("class [mscorlib]System.Reflection.Assembly"), _X("string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetTypeViaReflectionOrThrow"), _X("class [mscorlib]System.Type"), _X("string,string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetMethodViaReflectionOrThrow"), _X("class [mscorlib]System.Reflection.MethodInfo"), _X("string,string,string,class [mscorlib]System.Type[]")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorage"), _X("class [mscorlib]System.Reflection.MethodInfo"), _X("string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"), _X("class [mscorlib]System.Reflection.MethodInfo"), _X("string,string,string,string,class [mscorlib]System.Type[]")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("StoreMethodInAppDomainStorageOrThrow"), _X("void"), _X("class [mscorlib]System.Reflection.MethodInfo,string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("StoreMethodCacheLookupMethod"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("StoreAgentShimFinishTracerDelegateMethod"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("EnsureInitialized"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetMethodInfoFromAgentCache"), _X("class [mscorlib]System.Reflection.MethodInfo"), _X("string,string,string,string,class [mscorlib]System.Type[]")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetMethodCacheLookupMethod"), _X("object"), _X("")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetAgentShimFinishTracerDelegateMethod"), _X("object"), _X(""))
            };

            // When injecting HELPER METHODS into the System.Private.CoreLib assembly, theses references should be local.
            // They cannot reference [System.Private.CoreLib] since these methods are being rewritten in System.Private.CoreLib.
            constexpr std::array<ManagedMethodToInject, 12> methodImplsToInject{
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("LoadAssemblyOrThrow"), _X("class System.Reflection.Assembly"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetTypeViaReflectionOrThrow"), _X("class System.Type"), _X("string,string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodViaReflectionOrThrow"), _X("class System.Reflection.MethodInfo"), _X("string,string,string,class System.Type[]")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorage"), _X("class System.Reflection.MethodInfo"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"), _X("class System.Reflection.MethodInfo"), _X("string,string,string,string,class System.Type[]")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("StoreMethodInAppDomainStorageOrThrow"), _X("void"), _X("class System.Reflection.MethodInfo,string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("StoreMethodCacheLookupMethod"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("StoreAgentShimFinishTracerDelegateMethod"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("EnsureInitialized"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodInfoFromAgentCache"), _X("class System.Reflection.MethodInfo"), _X("string,string,string,string,class System.Type[]")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodCacheLookupMethod"), _X("object"), _X("")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetAgentShimFinishTracerDelegateMethod"), _X("object"), _X(""))
            };

            const auto is_coreLib = module.GetIsThisTheCoreLibAssembly();

            // If instrumenting System.Private.CoreLib, use local (to the assembly) references
            // otherwise use external references
            auto methods = is_coreLib
                ? methodImplsToInject
                : (isCoreClr ? methodReferencesToInjectCoreClr : methodReferencesToInjectNetFramework);

            if (!is_coreLib && !EnsureReferenceToCoreLib(module))
            {
                LogInfo(L"Unable to inject reference to Core Library into ", module.GetModuleName(), L".  This module will not be instrumented.");
                return;
            }

            if (is_coreLib)
            {
                LogDebug(L"Injecting New Relic helper type into ", module.GetModuleName());
                try
                {
                    module.InjectNRHelperType();
                }
                catch (NewRelic::Profiler::Win32Exception&)
                {
                    LogError(L"Failed to inject New Relic helper type into ", module.GetModuleName());
                }
            }

            LogDebug(L"Injecting ", ((is_coreLib) ? L"" : L"references to "), L"helper methods into ", module.GetModuleName());

            //inject the methods if this is the Core Lib and inject references into all other assemblies. (pointer to member function to select method to call in loop)
            const auto workerFunc{ (is_coreLib) ? &IModule::InjectStaticSecuritySafeMethod : &IModule::InjectCoreLibSecuritySafeMethodReference };
            xstring_t signatum;
            signatum.reserve(200);
            for (const auto& managedMethod : methods)
            {
                try
                {
                    //create standard signature string
                    signatum.assign(managedMethod.ReturnType)
                        .append(1, _X(' ')).append(managedMethod.TypeName)
                        .append(_X("::"), 2).append(managedMethod.MethodName)
                        .append(1, _X('(')).append(managedMethod.ParameterTypes)
                        .append(1, _X(')'));
                    auto signature = ToSignature(signatum, module.GetTokenizer());

                    //inject method or references...
                    (module.*workerFunc)(managedMethod.MethodName, managedMethod.TypeName, signature);
                }
                catch (NewRelic::Profiler::Win32Exception&)
                {
                    if (is_coreLib)
                    {
                        LogError(L"Failed to tokenize method signature: ", signatum, L". Proceeding to next method.");
                    }
                    else
                    {
                        LogTrace(L"Failed to tokenize method signature: ", signatum, L". Skipping injection of other method references for this module.");
                    }
                }
            }
        }

    private:
        static ByteVector ToSignature(const xstring_t& signature, const sicily::codegen::ITokenizerPtr& tokenizer)
        {
            sicily::Scanner scanner(signature);
            sicily::Parser parser;
            sicily::ast::TypePtr type = parser.Parse(scanner);
            sicily::codegen::ByteCodeGenerator generator(tokenizer);
            return generator.TypeToBytes(type);
        }

        static bool EnsureReferenceToCoreLib(IModule& module)
        {
            if (!module.NeedsReferenceToCoreLib())
            {
                return true;
            }

            return module.InjectReferenceToCoreLib();
        }
    };
}}}
