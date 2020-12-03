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
            using ntcs_t = const wchar_t* const;
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
            constexpr std::array<ManagedMethodToInject, 6> methodReferencesToInject{
                ManagedMethodToInject(L"[mscorlib]System.CannotUnloadAppDomainException", L"LoadAssemblyOrThrow", L"class [mscorlib]System.Reflection.Assembly", L"string"),
                ManagedMethodToInject(L"[mscorlib]System.CannotUnloadAppDomainException", L"GetTypeViaReflectionOrThrow", L"class [mscorlib]System.Type", L"string,string"),
                ManagedMethodToInject(L"[mscorlib]System.CannotUnloadAppDomainException", L"GetMethodViaReflectionOrThrow", L"class [mscorlib]System.Reflection.MethodInfo", L"string,string,string,class [mscorlib]System.Type[]"),
                ManagedMethodToInject(L"[mscorlib]System.CannotUnloadAppDomainException", L"GetMethodFromAppDomainStorage", L"class [mscorlib]System.Reflection.MethodInfo", L"string"),
                ManagedMethodToInject(L"[mscorlib]System.CannotUnloadAppDomainException", L"GetMethodFromAppDomainStorageOrReflectionOrThrow", L"class [mscorlib]System.Reflection.MethodInfo", L"string,string,string,string,class [mscorlib]System.Type[]"),
                ManagedMethodToInject(L"[mscorlib]System.CannotUnloadAppDomainException", L"StoreMethodInAppDomainStorageOrThrow", L"void", L"class [mscorlib]System.Reflection.MethodInfo,string")
            };

            // When injecting HELPER METHODS into the mscorlib assembly, theses references should be local.
            // They cannot reference [mscorlib] since these methods are being rewritten in mscorlib.
            constexpr std::array<ManagedMethodToInject, 6> methodImplsToInject {
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"LoadAssemblyOrThrow", L"class System.Reflection.Assembly", L"string"),
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"GetTypeViaReflectionOrThrow", L"class System.Type", L"string,string"),
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"GetMethodViaReflectionOrThrow", L"class System.Reflection.MethodInfo", L"string,string,string,class System.Type[]"),
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"GetMethodFromAppDomainStorage", L"class System.Reflection.MethodInfo", L"string"),
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"GetMethodFromAppDomainStorageOrReflectionOrThrow", L"class System.Reflection.MethodInfo", L"string,string,string,string,class System.Type[]"),
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"StoreMethodInAppDomainStorageOrThrow", L"void", L"class System.Reflection.MethodInfo,string")
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

            LogDebug(L"Injecting ", ((is_mscorlib) ? L"" : L"references to "), L"helper methods into ", module.GetModuleName());

            //inject the methods if mscorlib and inject references into all other assemblies. (pointer to member function to select method to call in loop)
            const auto workerFunc{ (is_mscorlib) ? &IModule::InjectStaticSecuritySafeMethod : &IModule::InjectMscorlibSecuritySafeMethodReference };
            std::wstring signatum;
            signatum.reserve(200);
            for (const auto& managedMethod : methods)
            {
                try
                {
                    //create standard signature string
                    signatum.assign(managedMethod.ReturnType)
                        .append(1, L' ').append(managedMethod.TypeName)
                        .append(L"::", 2).append(managedMethod.MethodName)
                        .append(1, L'(').append(managedMethod.ParameterTypes)
                        .append(1, L')');
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

    private:
        static ByteVector ToSignature(const std::wstring& signature, const sicily::codegen::ITokenizerPtr& tokenizer)
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

                auto injectResult = metaDataAssemblyEmit->DefineAssemblyRef(pubToken, sizeof(pubToken), L"mscorlib", &amd, NULL, 0, 0, &assemblyToken);
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

    };
}}}
