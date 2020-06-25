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

            constexpr std::array<ManagedMethodToInject, 6> managedMethodsToInject{
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"LoadAssemblyOrThrow", L"class System.Reflection.Assembly", L"string"),
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"GetTypeViaReflectionOrThrow", L"class System.Type", L"string,string"),
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"GetMethodViaReflectionOrThrow", L"class System.Reflection.MethodInfo", L"string,string,string,class System.Type[]"),
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"GetMethodFromAppDomainStorage", L"class System.Reflection.MethodInfo", L"string"),
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"GetMethodFromAppDomainStorageOrReflectionOrThrow", L"class System.Reflection.MethodInfo", L"string,string,string,string,class System.Type[]"),
                ManagedMethodToInject(L"System.CannotUnloadAppDomainException", L"StoreMethodInAppDomainStorageOrThrow", L"void", L"class System.Reflection.MethodInfo,string")
            };

            //inject the methods into mscorlib and inject references into all other assemblies.
            const auto is_mscorlib = Strings::EndsWith(module.GetModuleName(), L"mscorlib.dll");
            LogDebug(L"Injecting ", ((is_mscorlib) ? L"" : L"references to "), L"helper methods into ", module.GetModuleName());

            //inject the methods if mscorlib and inject references into all other assemblies. (pointer to member function to select method to call in loop)
            const auto workerFunc{ (is_mscorlib) ? &IModule::InjectStaticSecuritySafeMethod : &IModule::InjectMscorlibSecuritySafeMethodReference };
            std::wstring signatum;
            signatum.reserve(200);
            for (const auto& managedMethod : managedMethodsToInject) 
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
    };
}}}
