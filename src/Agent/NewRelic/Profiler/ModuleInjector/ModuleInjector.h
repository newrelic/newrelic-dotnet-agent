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
        static void InjectIntoModule(IModule& module)
        {
            //for background on the methodology and rationale, see:
            //  https://social.msdn.microsoft.com/Forums/en-US/f8bf431a-7a83-4dfb-bbf7-ef23b1e30904/profiling-silverlight-with-securitysafecriticalattributesecuritycriticalattribute-and-injected-il

            // When injecting method REFERENCES into an assembly, theses references should have
            // the external assembly identifier to mscorlib
            constexpr std::array<ManagedMethodToInject, 9> methodReferencesToInject{
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("LoadAssemblyOrThrow"), _X("class [mscorlib]System.Reflection.Assembly"), _X("string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetTypeViaReflectionOrThrow"), _X("class [mscorlib]System.Type"), _X("string,string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetMethodViaReflectionOrThrow"), _X("class [mscorlib]System.Reflection.MethodInfo"), _X("string,string,string,class [mscorlib]System.Type[]")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorage"), _X("class [mscorlib]System.Reflection.MethodInfo"), _X("string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"), _X("class [mscorlib]System.Reflection.MethodInfo"), _X("string,string,string,string,class [mscorlib]System.Type[]")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("StoreMethodInAppDomainStorageOrThrow"), _X("void"), _X("class [mscorlib]System.Reflection.MethodInfo,string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("EnsureInitialized"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetMethodInfoFromAgentCache"), _X("class [mscorlib]System.Reflection.MethodInfo"), _X("string,string,string,string,class [mscorlib]System.Type[]")),
                ManagedMethodToInject(_X("[mscorlib]System.CannotUnloadAppDomainException"), _X("GetMethodCacheLookupMethod"), _X("object"), _X(""))
            };

            // When injecting HELPER METHODS into the mscorlib assembly, theses references should be local.
            // They cannot reference [mscorlib] since these methods are being rewritten in mscorlib.
            constexpr std::array<ManagedMethodToInject, 9> methodImplsToInject {
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("LoadAssemblyOrThrow"), _X("class System.Reflection.Assembly"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetTypeViaReflectionOrThrow"), _X("class System.Type"), _X("string,string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodViaReflectionOrThrow"), _X("class System.Reflection.MethodInfo"), _X("string,string,string,class System.Type[]")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorage"), _X("class System.Reflection.MethodInfo"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"), _X("class System.Reflection.MethodInfo"), _X("string,string,string,string,class System.Type[]")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("StoreMethodInAppDomainStorageOrThrow"), _X("void"), _X("class System.Reflection.MethodInfo,string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("EnsureInitialized"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodInfoFromAgentCache"), _X("class System.Reflection.MethodInfo"), _X("string,string,string,string,class System.Type[]")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodCacheLookupMethod"), _X("object"), _X(""))
            };

            const auto is_mscorlib = module.GetIsThisTheMscorlibAssembly();

            // If instrumenting mscorlib, use local (to the assembly) references
            // otherwise use external references
            auto methods = is_mscorlib
                ? methodImplsToInject
                : methodReferencesToInject;

            if (!is_mscorlib && !EnsureReferenceToMscorlib(module))
            {
                LogInfo(L"Unable to inject reference to mscorlib into ", module.GetModuleName(), L".  This module will not be instrumented.");
                return;
            }

            if (is_mscorlib)
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

            LogDebug(L"Injecting ", ((is_mscorlib) ? L"" : L"references to "), L"helper methods into ", module.GetModuleName());

            //inject the methods if mscorlib and inject references into all other assemblies. (pointer to member function to select method to call in loop)
            const auto workerFunc{ (is_mscorlib) ? &IModule::InjectStaticSecuritySafeMethod : &IModule::InjectMscorlibSecuritySafeMethodReference };
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
                    //exception in an error if we are injecting methods, otherwise, just neat to know.
                    //if is mscorlib, allow the loop to proceed.  if not, break out of the loop
                    if (is_mscorlib)
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

        static void InjectIntoModuleCore(IModule& module)
        {
            // When injecting method REFERENCES into an assembly, theses references should have
            // the external assembly identifier to System.Private.CoreLib
            constexpr std::array<ManagedMethodToInject, 9> methodReferencesToInject{
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("LoadAssemblyOrThrow"), _X("class [System.Private.CoreLib]System.Reflection.Assembly"), _X("string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetTypeViaReflectionOrThrow"), _X("class [System.Private.CoreLib]System.Type"), _X("string,string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetMethodViaReflectionOrThrow"), _X("class [System.Private.CoreLib]System.Reflection.MethodInfo"), _X("string,string,string,class [System.Private.CoreLib]System.Type[]")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorage"), _X("class [System.Private.CoreLib]System.Reflection.MethodInfo"), _X("string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"), _X("class [System.Private.CoreLib]System.Reflection.MethodInfo"), _X("string,string,string,string,class [System.Private.CoreLib]System.Type[]")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("StoreMethodInAppDomainStorageOrThrow"), _X("void"), _X("class [System.Private.CoreLib]System.Reflection.MethodInfo,string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("EnsureInitialized"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetMethodInfoFromAgentCache"), _X("class [System.Private.CoreLib]System.Reflection.MethodInfo"), _X("string,string,string,string,class [System.Private.CoreLib]System.Type[]")),
                ManagedMethodToInject(_X("[System.Private.CoreLib]System.CannotUnloadAppDomainException"), _X("GetMethodCacheLookupMethod"), _X("object"), _X(""))
            };

            // When injecting HELPER METHODS into the System.Private.CoreLib assembly, theses references should be local.
            // They cannot reference [System.Private.CoreLib] since these methods are being rewritten in System.Private.CoreLib.
            constexpr std::array<ManagedMethodToInject, 9> methodImplsToInject{
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("LoadAssemblyOrThrow"), _X("class System.Reflection.Assembly"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetTypeViaReflectionOrThrow"), _X("class System.Type"), _X("string,string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodViaReflectionOrThrow"), _X("class System.Reflection.MethodInfo"), _X("string,string,string,class System.Type[]")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorage"), _X("class System.Reflection.MethodInfo"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"), _X("class System.Reflection.MethodInfo"), _X("string,string,string,string,class System.Type[]")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("StoreMethodInAppDomainStorageOrThrow"), _X("void"), _X("class System.Reflection.MethodInfo,string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("EnsureInitialized"), _X("void"), _X("string")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodInfoFromAgentCache"), _X("class System.Reflection.MethodInfo"), _X("string,string,string,string,class System.Type[]")),
                ManagedMethodToInject(_X("System.CannotUnloadAppDomainException"), _X("GetMethodCacheLookupMethod"), _X("object"), _X(""))
            };

            const auto is_systemPrivateCoreLib = module.GetIsThisTheSystemPrivateCoreLibAssembly();

            // If instrumenting System.Private.CoreLib, use local (to the assembly) references
            // otherwise use external references
            auto methods = is_systemPrivateCoreLib
                ? methodImplsToInject
                : methodReferencesToInject;

            if (!is_systemPrivateCoreLib && !EnsureReferenceToSystemPrivateCoreLib(module))
            {
                LogInfo(L"Unable to inject reference to System.Private.CoreLib into ", module.GetModuleName(), L".  This module will not be instrumented.");
                return;
            }

            if (is_systemPrivateCoreLib)
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

            LogDebug(L"Injecting ", ((is_systemPrivateCoreLib) ? L"" : L"references to "), L"helper methods into ", module.GetModuleName());

            //inject the methods if System.Private.CoreLib and inject references into all other assemblies. (pointer to member function to select method to call in loop)
            const auto workerFunc{ (is_systemPrivateCoreLib) ? &IModule::InjectStaticSecuritySafeMethod : &IModule::InjectSystemPrivateCoreLibSecuritySafeMethodReference };
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
                    //exception in an error if we are injecting methods, otherwise, just neat to know.
                    //if is System.Private.CoreLib, allow the loop to proceed.  if not, break out of the loop
                    if (is_systemPrivateCoreLib)
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

        static bool EnsureReferenceToMscorlib(IModule& module)
        {
            // If this is the NetStandard assembly, it already has a reference to mscorlib, so no need to do anything
            // Otherwise if this already has a reference to mscorlib, nothing to do.
            if (module.GetIsThisTheNetStandardAssembly() || module.GetHasRefMscorlib())
            {
                return true;
            }

            try
            {
                LogDebug(L"Attempting to Inject reference to mscorlib into netstandard Module  ", module.GetModuleName());

                // if the assembly wasn't in the existing references try to define a new one
                ASSEMBLYMETADATA amd;
                ZeroMemory(&amd, sizeof(amd));
                amd.usMajorVersion = 4;
                amd.usMinorVersion = 0;
                amd.usBuildNumber = 0;
                amd.usRevisionNumber = 0;

                auto metaDataAssemblyEmit = module.GetMetaDataAssemblyEmit();
                mdAssemblyRef assemblyToken;
                const BYTE pubToken[] = { 0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89 };

                auto injectResult = metaDataAssemblyEmit->DefineAssemblyRef(pubToken, sizeof(pubToken), _X("mscorlib"), &amd, NULL, 0, 0, &assemblyToken);
                if (injectResult == S_OK)
                {
                    module.SetMscorlibAssemblyRef(assemblyToken);
                    LogDebug(L"Attempting to Inject reference to mscorlib into netstandard Module  ", module.GetModuleName(), L" - Success: ", assemblyToken);

                    return true;
                }
                else
                {
                    LogDebug(L"Attempting to Inject reference to mscorlib into netstandard Module  ", module.GetModuleName(), L" - FAIL: ", injectResult);

                    return false;
                }
            }
            catch (NewRelic::Profiler::Win32Exception& ex)
            {
                LogError(L"Attempting to Inject reference to mscorlib into netstandard Module  ", module.GetModuleName(), L" - ERROR: ", ex._message);
                return false;
            }
        }

        static bool EnsureReferenceToSystemPrivateCoreLib(IModule& module)
        {
            // If this is the NetStandard or mscorelib assembly, it already has a reference to System.Private.CoreLib, so no need to do anything
            // Otherwise if this already has a reference to System.Private.CoreLib, nothing to do.
            if (module.GetIsThisTheNetStandardAssembly() || module.GetIsThisTheMscorlibAssembly() || module.GetHasRefSystemPrivateCoreLib())
            {
                return true;
            }

            try
            {
                LogDebug(L"Attempting to Inject reference to System.Private.CoreLib into Module  ", module.GetModuleName());

                // if the assembly wasn't in the existing references try to define a new one
                ASSEMBLYMETADATA amd;
                ZeroMemory(&amd, sizeof(amd));
                amd.usMajorVersion = 6;
                amd.usMinorVersion = 0;
                amd.usBuildNumber = 0;
                amd.usRevisionNumber = 0;

                auto metaDataAssemblyEmit = module.GetMetaDataAssemblyEmit();
                mdAssemblyRef assemblyToken;
                const BYTE pubToken[] = { 0x7C, 0xEC, 0x85, 0xD7, 0xBE, 0xA7, 0x79, 0x8E };

                auto injectResult = metaDataAssemblyEmit->DefineAssemblyRef(pubToken, sizeof(pubToken), _X("System.Private.CoreLib"), &amd, NULL, 0, 0, &assemblyToken);
                if (injectResult == S_OK)
                {
                    module.SetSystemPrivateCoreLibAssemblyRef(assemblyToken);
                    LogDebug(L"Attempting to Inject reference to System.Private.CoreLib into Module  ", module.GetModuleName(), L" - Success: ", assemblyToken);

                    return true;
                }
                else
                {
                    LogDebug(L"Attempting to Inject reference to System.Private.CoreLib into Module  ", module.GetModuleName(), L" - FAIL: ", injectResult);

                    return false;
                }
            }
            catch (NewRelic::Profiler::Win32Exception& ex)
            {
                LogError(L"Attempting to Inject reference to System.Private.CoreLib into Module  ", module.GetModuleName(), L" - ERROR: ", ex._message);
                return false;
            }
        }

    };
}}}
