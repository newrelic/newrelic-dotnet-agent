// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <stdint.h>
#include <memory>
#include <exception>
#include <functional>
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#define LOGGER_DEFINE_STDLOG
#include <set>
#include <array>
#include "CppUnitTest.h"

#include "MockModule.h"
#include "../ModuleInjector/ModuleInjector.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    TEST_CLASS(ModuleInjectorTest)
    {
    public:
        TEST_METHOD(ModuleInjector_CoreClr_DoesNotCrash_CannotEnsureReferenceToCoreLib)
        {
            auto modulePtr = std::make_shared<MockModule>();
            modulePtr->_needsReferenceToCoreLib = true;
            modulePtr->_injectReferenceToCoreLib = false;

            ModuleInjector::ModuleInjector::InjectIntoModule(*modulePtr, true);
        }

        TEST_METHOD(ModuleInjector_NetFramework_DoesNotCrash_CannotEnsureReferenceToCoreLib)
        {
            auto modulePtr = std::make_shared<MockModule>();
            modulePtr->_needsReferenceToCoreLib = true;
            modulePtr->_injectReferenceToCoreLib = false;

            ModuleInjector::ModuleInjector::InjectIntoModule(*modulePtr, false);
        }

        TEST_METHOD(ModuleInjector_CoreClr_InjectsHelperType)
        {
            auto modulePtr = std::make_shared<MockModule>();
            modulePtr->_isThisTheCoreLibAssembly = true;
            bool wasHelperTypeInjected(false);
            modulePtr->_injectNRHelperType = [&wasHelperTypeInjected]() { wasHelperTypeInjected = true; };

            ModuleInjector::ModuleInjector::InjectIntoModule(*modulePtr, true);

            Assert::IsTrue(wasHelperTypeInjected);
        }

        TEST_METHOD(ModuleInjector_NetFramework_InjectsHelperType)
        {
            auto modulePtr = std::make_shared<MockModule>();
            modulePtr->_isThisTheCoreLibAssembly = true;
            bool wasHelperTypeInjected(false);
            modulePtr->_injectNRHelperType = [&wasHelperTypeInjected]() { wasHelperTypeInjected = true; };

            ModuleInjector::ModuleInjector::InjectIntoModule(*modulePtr, false);

            Assert::IsTrue(wasHelperTypeInjected);
        }

        TEST_METHOD(ModuleInjector_CoreClr_InjectsHelperMethods_DoesNotThrow)
        {
            auto modulePtr = std::make_shared<MockModule>();
            modulePtr->_isThisTheCoreLibAssembly = true;
            modulePtr->_injectNRHelperType = []() { throw NewRelic::Profiler::Win32Exception(E_UNEXPECTED); };

            ModuleInjector::ModuleInjector::InjectIntoModule(*modulePtr, true);
        }

        TEST_METHOD(ModuleInjector_NetFramework_InjectsHelperMethods_DoesNotThrow)
        {
            auto modulePtr = std::make_shared<MockModule>();
            modulePtr->_isThisTheCoreLibAssembly = true;
            modulePtr->_injectNRHelperType = []() { throw NewRelic::Profiler::Win32Exception(E_UNEXPECTED); };

            ModuleInjector::ModuleInjector::InjectIntoModule(*modulePtr, false);
        }

        TEST_METHOD(ModuleInjector_CoreClr_InjectsHelperMethods)
        {
            AssertHelperMethodsWereInjected(true);
        }

        TEST_METHOD(ModuleInjector_NetFramework_InjectsHelperMethods)
        {
            AssertHelperMethodsWereInjected(false);
        }

        TEST_METHOD(ModuleInjector_CoreClr_AlwaysAttemptsToInjectAllMethods)
        {
            AssertHelperMethodsWereInjected(/*isCoreClr*/ true, /*throwsException*/ true);
        }

        TEST_METHOD(ModuleInjector_NetFramework_AlwaysAttemptsToInjectAllMethods)
        {
            AssertHelperMethodsWereInjected(/*isCoreClr*/ false, /*throwsException*/ true);
        }

        TEST_METHOD(ModuleInjector_CoreClr_InjectsHelperMethodReferences)
        {
            AssertHelperMethodReferencesWereInjected(true);
        }

        TEST_METHOD(ModuleInjector_NetFramework_InjectsHelperMethodReferences)
        {
            AssertHelperMethodReferencesWereInjected(false);
        }

        TEST_METHOD(ModuleInjector_CoreClr_AlwaysAttemptsToInjectAllMethodReferences)
        {
            AssertHelperMethodReferencesWereInjected(/*isCoreClr*/ true, /*throwsException*/ true);
        }

        TEST_METHOD(ModuleInjector_NetFramework_AlwaysAttemptsToInjectAllMethodReferences)
        {
            AssertHelperMethodReferencesWereInjected(/*isCoreClr*/ false, /*throwsException*/ true);
        }

    private:
        void AssertHelperMethodsWereInjected(const bool isCoreClr)
        {
            AssertHelperMethodsWereInjected(isCoreClr, false);
        }

        void AssertHelperMethodsWereInjected(const bool isCoreClr, const bool throwsException)
        {
            std::array<xstring_t, 9> expectedMethods = {
                _X("System.CannotUnloadAppDomainException.LoadAssemblyOrThrow"),
                _X("System.CannotUnloadAppDomainException.GetTypeViaReflectionOrThrow"),
                _X("System.CannotUnloadAppDomainException.GetMethodViaReflectionOrThrow"),
                _X("System.CannotUnloadAppDomainException.GetMethodFromAppDomainStorage"),
                _X("System.CannotUnloadAppDomainException.GetMethodFromAppDomainStorageOrReflectionOrThrow"),
                _X("System.CannotUnloadAppDomainException.StoreMethodInAppDomainStorageOrThrow"),
                _X("System.CannotUnloadAppDomainException.EnsureInitialized"),
                _X("System.CannotUnloadAppDomainException.GetMethodInfoFromAgentCache"),
                _X("System.CannotUnloadAppDomainException.GetMethodCacheLookupMethod")
            };

            auto modulePtr = std::make_shared<MockModule>();
            modulePtr->_isThisTheCoreLibAssembly = true;
            std::set<xstring_t> injectedMethodNamesWithType;
            modulePtr->_injectStaticSecuritySafeMethod = [&injectedMethodNamesWithType, throwsException](const xstring_t& methodName, const xstring_t& className, const ByteVector& /*signature*/)
            {
                injectedMethodNamesWithType.emplace(className + _X(".") + methodName);
                if (throwsException)
                {
                    throw NewRelic::Profiler::Win32Exception(E_UNEXPECTED);
                }
            };

            ModuleInjector::ModuleInjector::InjectIntoModule(*modulePtr, isCoreClr);

            Assert::AreEqual(expectedMethods.size(), injectedMethodNamesWithType.size());

            for (const auto& expected : expectedMethods)
            {
                auto search = injectedMethodNamesWithType.find(expected);
                Assert::IsTrue(search != injectedMethodNamesWithType.end(), (expected + _X(" was not found")).c_str());
            }
        }

        void AssertHelperMethodReferencesWereInjected(const bool isCoreClr)
        {
            AssertHelperMethodReferencesWereInjected(isCoreClr, false);
        }

        void AssertHelperMethodReferencesWereInjected(const bool isCoreClr, const bool throwsException)
        {
            const xstring_t expectedAssembly = isCoreClr ? _X("[System.Private.CoreLib]") : _X("[mscorlib]");
            std::array<xstring_t, 9> expectedMethods = {
                expectedAssembly + _X("System.CannotUnloadAppDomainException.LoadAssemblyOrThrow"),
                expectedAssembly + _X("System.CannotUnloadAppDomainException.GetTypeViaReflectionOrThrow"),
                expectedAssembly + _X("System.CannotUnloadAppDomainException.GetMethodViaReflectionOrThrow"),
                expectedAssembly + _X("System.CannotUnloadAppDomainException.GetMethodFromAppDomainStorage"),
                expectedAssembly + _X("System.CannotUnloadAppDomainException.GetMethodFromAppDomainStorageOrReflectionOrThrow"),
                expectedAssembly + _X("System.CannotUnloadAppDomainException.StoreMethodInAppDomainStorageOrThrow"),
                expectedAssembly + _X("System.CannotUnloadAppDomainException.EnsureInitialized"),
                expectedAssembly + _X("System.CannotUnloadAppDomainException.GetMethodInfoFromAgentCache"),
                expectedAssembly + _X("System.CannotUnloadAppDomainException.GetMethodCacheLookupMethod")
            };

            auto modulePtr = std::make_shared<MockModule>();
            modulePtr->_isThisTheCoreLibAssembly = false;
            std::set<xstring_t> injectedMethodNamesWithType;
            modulePtr->_injectCoreLibSecuritySafeMethodReference = [&injectedMethodNamesWithType, throwsException](const xstring_t& methodName, const xstring_t& className, const ByteVector& /*signature*/)
            {
                injectedMethodNamesWithType.emplace(className + _X(".") + methodName);
                if (throwsException)
                {
                    throw NewRelic::Profiler::Win32Exception(E_UNEXPECTED);
                }
            };

            ModuleInjector::ModuleInjector::InjectIntoModule(*modulePtr, isCoreClr);

            Assert::AreEqual(expectedMethods.size(), injectedMethodNamesWithType.size());

            for (const auto& expected : expectedMethods)
            {
                auto search = injectedMethodNamesWithType.find(expected);
                Assert::IsTrue(search != injectedMethodNamesWithType.end(), (expected + _X(" was not found")).c_str());
            }
        }
    };
}}}}
