// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "../Common/Macros.h"
#include "MockFunction.h"
#include "../MethodRewriter/FunctionManipulator.h"
#include "../MethodRewriter/InstrumentFunctionManipulator.h"
#include "../MethodRewriter/HelperFunctionManipulator.h"
#include "../MethodRewriter/ApiFunctionManipulator.h"
#include "RecordingTokenizer.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    // FunctionManipulator keeps LoadMethodInfo, LoadMethodInfoFromType, InvokeMethodInfo and
    // ThrowExceptionIfStackItemIsNull protected, since production callers only ever reach them
    // through a derived manipulator (Api/Instrument/HelperFunctionManipulator). This test-only
    // subclass forwards to them directly so their branches can be driven without going through
    // a full end-to-end instrumentation pass.
    struct TestableFunctionManipulator : public FunctionManipulator
    {
        TestableFunctionManipulator(IFunctionPtr function, const bool isCoreClr, const AgentCallStyle::Strategy agentCallStrategy) :
            FunctionManipulator(function, isCoreClr, agentCallStrategy)
        {
            Initialize();
        }

        void CallLoadMethodInfo(xstring_t assemblyPath, xstring_t className, xstring_t methodName, std::function<void()> argumentTypesLambda)
        {
            LoadMethodInfo(assemblyPath, className, methodName, argumentTypesLambda);
        }

        void CallLoadMethodInfoFromType(xstring_t methodName, std::function<void()> argumentTypesLambda)
        {
            LoadMethodInfoFromType(methodName, argumentTypesLambda);
        }

        void CallInvokeMethodInfo()
        {
            InvokeMethodInfo();
        }

        static void CallThrowExceptionIfStackItemIsNull(const InstructionSetPtr& instructions, const xstring_t& message, const bool& inCoreLib)
        {
            ThrowExceptionIfStackItemIsNull(instructions, message, inCoreLib);
        }
    };

    TEST_CLASS(FunctionManipulatorTest)
    {
    public:
        TEST_METHOD(construction)
        {
            auto function = std::make_shared<MockFunction>();
            FunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
        }

        TEST_METHOD(instrument_minimal_method)
        {
            auto function = std::make_shared<MockFunction>();
            InstrumentFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, L""), false, AgentCallStyle::Strategy::AppDomainFallbackCache);

            auto instrumentationPoint = CreateInstrumentationPointThatMatchesFunction(function);
            manipulator.InstrumentDefault(instrumentationPoint);
        }

        // ---------------------------------------------------------------------------------------
        // .NET 11 runtime-async return handling. See NR-610232 and RuntimeAsyncReturnType.h.
        //
        // A runtime-async method's IL body does not honour the return convention its signature
        // declares: Task/ValueTask push nothing before `ret`, Task<T>/ValueTask<T> push an
        // unwrapped T. Instrumenting against the declared type produced two distinct defects --
        // a stack underflow for Task (an injected `stloc` with nothing to store) and a type
        // mismatch for Task<T> (a T stored into a Task<T>-typed local, then returned as Task<T>).
        //
        // The strongest available statement of the fix is an equivalence: instrumenting a
        // runtime-async method must emit exactly what instrumenting a method that genuinely
        // returns the effective type emits. The return type reaches the generated IL only through
        // the four return-value sites, so if the substitution is right the byte streams match, and
        // if it is wrong they cannot. That holds without hard-coding IL layout, so these tests
        // survive unrelated changes to the injected preamble.
        // ---------------------------------------------------------------------------------------
    private:
        struct InstrumentationResult
        {
            ByteVector _methodBytes;
            ByteVector _localsSignature;
        };

        // 0 parameters keeps the injected argument-marshalling identical across every function
        // built here, so any difference in output has to come from the return type.
        static std::shared_ptr<MockFunction> MakeFunction(const ByteVector& returnTypeBytes, bool isRuntimeAsync, const wchar_t* resolvedTypeName)
        {
            auto function = std::make_shared<MockFunction>();
            function->_isRuntimeAsync = isRuntimeAsync;

            auto resolver = std::make_shared<MockTokenResolver>();
            resolver->_typeString = resolvedTypeName;
            function->_tokenResolver = resolver;

            function->_signature = std::make_shared<ByteVector>();
            function->_signature->push_back(0x00);  // default calling convention
            function->_signature->push_back(0x00);  // 0 parameters
            function->_signature->insert(function->_signature->end(), returnTypeBytes.begin(), returnTypeBytes.end());

            return function;
        }

        // not static: CreateInstrumentationPointThatMatchesFunction is a member of this fixture
        InstrumentationResult InstrumentAndCapture(std::shared_ptr<MockFunction> function)
        {
            InstrumentationResult result;
            function->_writeMethodHandler = [&result](const ByteVector& bytes) { result._methodBytes = bytes; };
            function->_tokenFromSignatureHandler = [&result](const ByteVector& bytes) { result._localsSignature = bytes; };

            InstrumentFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, L""), false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentDefault(CreateInstrumentationPointThatMatchesFunction(function));

            Assert::IsFalse(result._methodBytes.empty(), L"instrumentation did not write a method");
            Assert::IsFalse(result._localsSignature.empty(), L"instrumentation did not write a locals signature");
            return result;
        }

        static void AssertBytesEqual(const ByteVector& expected, const ByteVector& actual, const wchar_t* message)
        {
            Assert::AreEqual(expected.size(), actual.size(), message);
            for (size_t i = 0; i < expected.size(); ++i)
            {
                Assert::AreEqual(uint32_t(expected[i]), uint32_t(actual[i]), message);
            }
        }

        // ECMA-335 LocalVarSig: 0x07, compressed local count, then one type per local.
        static uint8_t LocalCount(const ByteVector& localsSignature)
        {
            Assert::AreEqual(uint32_t(0x07), uint32_t(localsSignature.at(0)), L"expected a LOCAL_SIG locals signature");
            return localsSignature.at(1);
        }

        // metadata type names, so the generic form carries its `1 arity suffix
        static const wchar_t* TaskTypeName() { return L"System.Threading.Tasks.Task"; }
        static const wchar_t* GenericTaskTypeName() { return L"System.Threading.Tasks.Task`1"; }

        // ELEMENT_TYPE_VOID / _I4 / _CLASS+token / _GENERICINST _CLASS+token 1 _I4
        static ByteVector VoidReturn() { return ByteVector{ 0x01 }; }
        static ByteVector Int32Return() { return ByteVector{ 0x08 }; }
        static ByteVector TaskReturn() { return ByteVector{ 0x12, 0x49 }; }
        static ByteVector GenericTaskOfInt32Return() { return ByteVector{ 0x15, 0x12, 0x49, 0x01, 0x08 }; }

    public:
        TEST_METHOD(runtime_async_task_is_instrumented_exactly_like_a_void_method)
        {
            // the shape the attached repro hits: nothing is pushed before ret, so the correct
            // instrumentation is byte-for-byte the void instrumentation -- no result local, no
            // store, a bare ret
            auto asyncResult = InstrumentAndCapture(MakeFunction(TaskReturn(), true, TaskTypeName()));
            auto voidResult = InstrumentAndCapture(MakeFunction(VoidReturn(), false, TaskTypeName()));

            AssertBytesEqual(voidResult._localsSignature, asyncResult._localsSignature,
                L"a runtime-async Task method must declare the same locals as a void method");
            AssertBytesEqual(voidResult._methodBytes, asyncResult._methodBytes,
                L"a runtime-async Task method must be instrumented exactly like a void method");
        }

        TEST_METHOD(runtime_async_task_declares_no_result_local)
        {
            // states the same fact directly, so the intent survives even if the baseline above moves
            auto asyncResult = InstrumentAndCapture(MakeFunction(TaskReturn(), true, TaskTypeName()));
            Assert::AreEqual(uint32_t(2), uint32_t(LocalCount(asyncResult._localsSignature)),
                L"only the tracer and userException locals belong to a runtime-async Task method");
        }

        TEST_METHOD(runtime_async_generic_task_is_instrumented_exactly_like_an_int_method)
        {
            // async Task<int> pushes an int, so the correct instrumentation is the int
            // instrumentation: an int-typed result local, a store into it, and ldloc + ret
            auto asyncResult = InstrumentAndCapture(MakeFunction(GenericTaskOfInt32Return(), true, GenericTaskTypeName()));
            auto intResult = InstrumentAndCapture(MakeFunction(Int32Return(), false, GenericTaskTypeName()));

            AssertBytesEqual(intResult._localsSignature, asyncResult._localsSignature,
                L"a runtime-async Task<int> method must declare an int result local, not a Task<int> one");
            AssertBytesEqual(intResult._methodBytes, asyncResult._methodBytes,
                L"a runtime-async Task<int> method must be instrumented exactly like an int method");
        }

        TEST_METHOD(runtime_async_generic_task_result_local_is_the_unwrapped_type)
        {
            auto asyncResult = InstrumentAndCapture(MakeFunction(GenericTaskOfInt32Return(), true, GenericTaskTypeName()));
            auto& localsSignature = asyncResult._localsSignature;

            Assert::AreEqual(uint32_t(3), uint32_t(LocalCount(localsSignature)),
                L"tracer, userException and an int result local");
            Assert::AreEqual(uint32_t(0x08), uint32_t(localsSignature.back()),
                L"the result local must be ELEMENT_TYPE_I4, the unwrapped type argument");

            // the declared Task<int> would have entered the signature as GENERICINST (0x15)
            for (size_t i = 0; i < localsSignature.size(); ++i)
            {
                Assert::AreNotEqual(uint32_t(0x15), uint32_t(localsSignature[i]),
                    L"no generic instantiation belongs in the locals of a runtime-async Task<int> method");
            }
        }

        TEST_METHOD(the_async_impl_flag_changes_the_emitted_instrumentation)
        {
            // negative control: the same Task-returning signature without the flag is an ordinary
            // synchronous method and must still be instrumented against its declared return type
            auto asyncResult = InstrumentAndCapture(MakeFunction(TaskReturn(), true, TaskTypeName()));
            auto syncResult = InstrumentAndCapture(MakeFunction(TaskReturn(), false, TaskTypeName()));

            Assert::AreEqual(uint32_t(2), uint32_t(LocalCount(asyncResult._localsSignature)),
                L"the runtime-async method needs no result local");
            Assert::AreEqual(uint32_t(3), uint32_t(LocalCount(syncResult._localsSignature)),
                L"the synchronous method still stores its returned Task in a result local");
            Assert::AreNotEqual(syncResult._methodBytes.size(), asyncResult._methodBytes.size(),
                L"the Async impl flag must change what is emitted");
        }

        //TEST_METHOD(test_method_with_no_code)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_no_extra_sections)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_invalid_header)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_simple_method)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_exceptions)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_multiple_returns)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_local_variables)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_one_extra_section)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_multiple_extra_sections)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_tiny_header)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_method_with_fat_header)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(test_fat_header_migration)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(load_argument_and_box_test)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(has_signature_test)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(locals_are_appended)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(local_offsets_are_correct)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(default_instrumentation_is_correct)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(max_local_variables)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        //TEST_METHOD(border_local_variables_byte_count)
        //{
        //    Assert::Fail(L"Test not implemented.");
        //}

        TEST_METHOD(helper_method_GetAgentShimFinishTracerDelegateFunc)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("GetAgentShimFinishTracerDelegateFunc");

            ByteVector capturedBytes;
            function->_writeMethodHandler = [&capturedBytes](const ByteVector& bytes) {
                capturedBytes = bytes;
            };

            HelperFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentHelper();

            // capturedBytes = 1-byte tiny header + IL body
            // expected IL size: 51 bytes; first IL byte: CEE_LDSFLD (0x7E)
            Assert::AreEqual((size_t)52, capturedBytes.size());
            Assert::AreEqual((uint8_t)0x7E, capturedBytes[1]);
        }

        TEST_METHOD(helper_method_StoreAgentShimFinishTracerDelegateFunc)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("StoreAgentShimFinishTracerDelegateFunc");

            ByteVector capturedBytes;
            function->_writeMethodHandler = [&capturedBytes](const ByteVector& bytes) {
                capturedBytes = bytes;
            };

            HelperFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentHelper();

            // capturedBytes = 1-byte tiny header + IL body
            // expected IL size: 41 bytes; first IL byte: CEE_LDARG_0 (0x02)
            Assert::AreEqual((size_t)42, capturedBytes.size());
            Assert::AreEqual((uint8_t)0x02, capturedBytes[1]);
        }

        TEST_METHOD(helper_method_InvokeAgentShimFinishTracerDelegateFunc)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("InvokeAgentShimFinishTracerDelegateFunc");

            ByteVector capturedBytes;
            function->_writeMethodHandler = [&capturedBytes](const ByteVector& bytes) {
                capturedBytes = bytes;
            };

            HelperFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentHelper();

            // capturedBytes = 1-byte tiny header + IL body
            // expected IL size: 23 bytes; first IL byte: CEE_LDARG_0 (0x02)
            Assert::AreEqual((size_t)24, capturedBytes.size());
            Assert::AreEqual((uint8_t)0x02, capturedBytes[1]);
        }

        TEST_METHOD(helper_method_EnsureInitialized)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("EnsureInitialized");

            ByteVector capturedBytes;
            function->_writeMethodHandler = [&capturedBytes](const ByteVector& bytes) {
                capturedBytes = bytes;
            };

            HelperFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentHelper();

            // capturedBytes = 1-byte tiny header + IL body
            // expected IL size: 23 bytes; first IL byte: CEE_CALL (0x28)
            Assert::AreEqual((size_t)24, capturedBytes.size());
            Assert::AreEqual((uint8_t)0x28, capturedBytes[1]);
        }

        TEST_METHOD(helper_method_GetAgentMethodInvokerObject)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("GetAgentMethodInvokerObject");

            ByteVector capturedBytes;
            function->_writeMethodHandler = [&capturedBytes](const ByteVector& bytes) {
                capturedBytes = bytes;
            };

            HelperFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentHelper();

            // capturedBytes = 1-byte tiny header + IL body
            // expected IL size: 51 bytes; first IL byte: CEE_LDSFLD (0x7E)
            Assert::AreEqual((size_t)52, capturedBytes.size());
            Assert::AreEqual((uint8_t)0x7E, capturedBytes[1]);
        }

        TEST_METHOD(helper_method_StoreAgentMethodInvokerFunc)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("StoreAgentMethodInvokerFunc");

            ByteVector capturedBytes;
            function->_writeMethodHandler = [&capturedBytes](const ByteVector& bytes) {
                capturedBytes = bytes;
            };

            HelperFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentHelper();

            // capturedBytes = 1-byte tiny header + IL body
            // expected IL size: 41 bytes; first IL byte: CEE_LDARG_0 (0x02)
            Assert::AreEqual((size_t)42, capturedBytes.size());
            Assert::AreEqual((uint8_t)0x02, capturedBytes[1]);
        }

        // The invoker helper self-initializes, so it emits the same IL under both
        // strategies. Both cases are kept to pin that the shape is deliberately
        // strategy-independent: nothing outside this helper initializes it.
        TEST_METHOD(helper_method_InvokeAgentMethodInvokerFunc_AppDomainFallbackCache)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("InvokeAgentMethodInvokerFunc");

            ByteVector capturedBytes;
            function->_writeMethodHandler = [&capturedBytes](const ByteVector& bytes) {
                capturedBytes = bytes;
            };

            HelperFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentHelper();

            // capturedBytes = 1-byte tiny header + IL body
            // expected IL size: 31 bytes; first IL byte: CEE_LDARG_0 (0x02)
            Assert::AreEqual((size_t)32, capturedBytes.size());
            Assert::AreEqual((uint8_t)0x02, capturedBytes[1]);
        }

        TEST_METHOD(helper_method_InvokeAgentMethodInvokerFunc_Reflection)
        {
            auto function = std::make_shared<MockFunction>();
            function->_functionName = _X("InvokeAgentMethodInvokerFunc");

            ByteVector capturedBytes;
            function->_writeMethodHandler = [&capturedBytes](const ByteVector& bytes) {
                capturedBytes = bytes;
            };

            HelperFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::Reflection);
            manipulator.InstrumentHelper();

            // Same IL as the AppDomainFallbackCache case, by design.
            // expected IL size: 31 bytes; first IL byte: CEE_LDARG_0 (0x02)
            Assert::AreEqual((size_t)32, capturedBytes.size());
            Assert::AreEqual((uint8_t)0x02, capturedBytes[1]);
        }

        // Pins the Reflection strategy emission for the Agent API path. Reflection is
        // the graceful-degradation path used when core library helper injection fails,
        // so it must stay self-contained: it resolves the target itself and must never
        // call an injected helper.
        TEST_METHOD(api_method_emits_reflection_path_under_reflection_strategy)
        {
            auto function = std::make_shared<MockFunction>();
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            function->_tokenizer = tokenizer;

            ApiFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("C:\\corepath")), false, AgentCallStyle::Strategy::Reflection);
            manipulator.InstrumentApi();

            // Reflection resolves the method itself and dispatches through MethodBase.Invoke.
            Assert::IsTrue(tokenizer->Tokenized(_X("Invoke")), L"Reflection path must dispatch through MethodBase.Invoke");
            // It must not depend on any injected core library helper.
            Assert::IsFalse(tokenizer->Tokenized(_X("InvokeAgentMethodInvokerFunc")), L"Reflection path must not call injected helpers");
            Assert::IsFalse(tokenizer->Tokenized(_X("GetMethodFromAppDomainStorageOrReflectionOrThrow")), L"Reflection path must not call injected helpers");
        }

        // Under AppDomainFallbackCache the API path must dispatch through the injected
        // managed invoker helper, not resolve a MethodInfo and call MethodBase.Invoke.
        // That is what removes the per-call AppDomain.GetData.
        TEST_METHOD(api_method_emits_managed_invoker_under_app_domain_fallback_cache)
        {
            auto function = std::make_shared<MockFunction>();
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            function->_tokenizer = tokenizer;

            ApiFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("C:\\corepath")), false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentApi();

            Assert::IsTrue(tokenizer->Tokenized(_X("InvokeAgentMethodInvokerFunc")), L"API path must call the managed invoker helper");
            Assert::IsFalse(tokenizer->Tokenized(_X("Invoke")), L"API path must no longer dispatch through MethodBase.Invoke");
            Assert::IsFalse(tokenizer->Tokenized(_X("GetMethodFromAppDomainStorageOrReflectionOrThrow")), L"API path must no longer resolve a MethodInfo per call");
        }

        // Under AppDomainFallbackCache the instrumented-method path must dispatch GetTracer
        // through the injected shim delegate helper rather than resolving a MethodInfo and
        // calling MethodBase.Invoke. That is what removes the per-call reflection invoke.
        TEST_METHOD(instrumented_method_emits_shim_delegate_invoker_under_app_domain_fallback_cache)
        {
            auto function = std::make_shared<MockFunction>();
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            function->_tokenizer = tokenizer;

            InstrumentFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("C:\\corepath")), false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentDefault(CreateInstrumentationPointThatMatchesFunction(function));

            Assert::IsTrue(tokenizer->Tokenized(_X("InvokeAgentShimFinishTracerDelegateFunc")), L"hot path must call the shim delegate helper");
            Assert::IsFalse(tokenizer->Tokenized(_X("GetAgentShimMethodFromAppDomainStorageOrReflectionOrThrow")), L"hot path must no longer resolve a MethodInfo per call");
        }

        // Reflection is the graceful-degradation path used when core library helper injection
        // fails, so it must keep resolving the target itself and must never call an injected
        // helper. GetMethod comes from LoadMethodInfoFromType.
        TEST_METHOD(instrumented_method_emits_method_info_invoke_under_reflection)
        {
            auto function = std::make_shared<MockFunction>();
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            function->_tokenizer = tokenizer;

            InstrumentFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("C:\\corepath")), false, AgentCallStyle::Strategy::Reflection);
            manipulator.InstrumentDefault(CreateInstrumentationPointThatMatchesFunction(function));

            Assert::IsTrue(tokenizer->Tokenized(_X("GetMethod")), L"Reflection path must resolve the target itself");
            Assert::IsFalse(tokenizer->Tokenized(_X("InvokeAgentShimFinishTracerDelegateFunc")), L"Reflection path must not call injected helpers");
        }

        // Covers the non-void return path. The invoker needs a real System.Type for the
        // return type, not the null used for void. A mismatch here would make CreateDelegate
        // throw on the managed side and fall back silently to the original method body,
        // so it gets its own test.
        TEST_METHOD(api_method_passes_return_type_for_non_void_methods)
        {
            auto function = std::make_shared<MockFunction>();
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            function->_tokenizer = tokenizer;

            // MockFunction defaults to a void return. Swap in a signature with a string
            // return: default calling convention, 1 parameter, ELEMENT_TYPE_STRING (0x0e)
            // return, ELEMENT_TYPE_CLASS (0x12) parameter with a compressed class token.
            BYTEVECTOR(nonVoidSignature, 0x00, 0x01, 0x0e, 0x12, 0x49);
            function->_signature = std::make_shared<ByteVector>(nonVoidSignature);

            ApiFunctionManipulator manipulator(function, std::make_shared<InstrumentationSettings>(nullptr, _X("C:\\corepath")), false, AgentCallStyle::Strategy::AppDomainFallbackCache);
            manipulator.InstrumentApi();

            Assert::IsTrue(tokenizer->Tokenized(_X("InvokeAgentMethodInvokerFunc")), L"non-void API methods must still use the managed invoker");

            // Type::GetTypeFromHandle is emitted once per System.Type pushed: one for the
            // single parameter, one for the non-void return type.
            auto getTypeFromHandleCount = std::count(tokenizer->_memberRefMethodNames.begin(), tokenizer->_memberRefMethodNames.end(), std::wstring(_X("GetTypeFromHandle")));
            Assert::AreEqual((size_t)2, (size_t)getTypeFromHandleCount, L"expected one GetTypeFromHandle for the parameter and one for the return type");
        }

        // Injected core library helpers are always emitted with a tiny method header, which
        // caps IL at 63 bytes. Over-limit IL is silently truncated into a malformed method:
        // no assert, no exception, just an InvalidProgramException or a crash at runtime.
        // This guards every helper under both strategies so nobody has to remember.
        TEST_METHOD(all_injected_helpers_respect_the_tiny_method_limit)
        {
            const std::vector<xstring_t> helperNames {
                _X("LoadAssemblyOrThrow"),
                _X("GetTypeViaReflectionOrThrow"),
                _X("GetMethodViaReflectionOrThrow"),
                _X("StoreMethodInAppDomainStorageOrThrow"),
                _X("GetAgentShimFinishTracerDelegateFunc"),
                _X("StoreAgentShimFinishTracerDelegateFunc"),
                _X("InvokeAgentShimFinishTracerDelegateFunc"),
                _X("EnsureInitialized"),
                _X("GetAgentMethodInvokerObject"),
                _X("InvokeAgentMethodInvokerFunc"),
                _X("StoreAgentMethodInvokerFunc")
            };

            const std::vector<AgentCallStyle::Strategy> strategies {
                AgentCallStyle::Strategy::AppDomainFallbackCache,
                AgentCallStyle::Strategy::Reflection
            };

            for (auto strategy : strategies)
            {
                for (auto helperName : helperNames)
                {
                    auto function = std::make_shared<MockFunction>();
                    function->_functionName = helperName;

                    ByteVector capturedBytes;
                    function->_writeMethodHandler = [&capturedBytes](const ByteVector& bytes) {
                        capturedBytes = bytes;
                    };

                    HelperFunctionManipulator manipulator(function, false, strategy);
                    manipulator.InstrumentHelper();

                    Assert::IsTrue(capturedBytes.size() > 1, (helperName + _X(": emitted no IL")).c_str());
                    // 1-byte tiny header plus IL body, and tiny IL must be under 64 bytes.
                    Assert::IsTrue(capturedBytes.size() <= 64, (helperName + _X(": IL exceeds the tiny method limit")).c_str());
                }
            }
        }

        // LoadMethodInfoFromType's null-lambda branch is only reached when a caller has no
        // parameters to match against (Type.GetMethod(string) instead of the overload that
        // also takes a Type[]). ApiFunctionManipulator always supplies a non-null lambda, so
        // this branch needs a direct call through the test-only forwarder.
        TEST_METHOD(load_method_info_from_type_resolves_without_argument_types_when_no_parameters)
        {
            auto function = std::make_shared<MockFunction>();
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            function->_tokenizer = tokenizer;

            TestableFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::Reflection);

            manipulator.CallLoadMethodInfoFromType(_X("SomeMethod"), std::function<void()>());

            Assert::IsTrue(tokenizer->Tokenized(_X("GetMethod")), L"a null argument-types lambda should resolve via the single-argument Type.GetMethod(string) overload");
        }

        // No current production caller reaches FunctionManipulator::InvokeMethodInfo directly:
        // ApiFunctionManipulator's reflection path emits its own inline MethodBase.Invoke call
        // rather than delegating to it. Call the base method itself through the forwarder so
        // its own Append call is exercised.
        TEST_METHOD(invoke_method_info_dispatches_through_method_base_invoke)
        {
            auto function = std::make_shared<MockFunction>();
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            function->_tokenizer = tokenizer;

            TestableFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::Reflection);

            manipulator.CallInvokeMethodInfo();

            Assert::IsTrue(tokenizer->Tokenized(_X("Invoke")), L"should dispatch through MethodBase.Invoke(object, object[])");
        }

        // LoadMethodInfo now serves Reflection only, so AppDomainFallbackCache must take the
        // error branch rather than emit an injected cache helper.
        TEST_METHOD(load_method_info_throws_for_app_domain_fallback_cache)
        {
            auto function = std::make_shared<MockFunction>();
            TestableFunctionManipulator manipulator(function, false, AgentCallStyle::Strategy::AppDomainFallbackCache);

            std::function<void(void)> func = [&manipulator]() {
                manipulator.CallLoadMethodInfo(_X("C:\\corepath"), _X("NewRelic.Agent.Core.AgentShim"), _X("SomeMethod"), std::function<void()>());
                };

            Assert::ExpectException<FunctionManipulatorException>(func, L"AppDomainFallbackCache dispatches through cached delegates and must not resolve a MethodInfo.");
        }

        // The remaining strategy value is out of range. Forcing it through the constructor
        // exercises the same error branch from the other direction.
        TEST_METHOD(load_method_info_throws_for_an_unsupported_agent_call_strategy)
        {
            auto function = std::make_shared<MockFunction>();
            TestableFunctionManipulator manipulator(function, false, static_cast<AgentCallStyle::Strategy>(-1));

            std::function<void(void)> func = [&manipulator]() {
                manipulator.CallLoadMethodInfo(_X("C:\\corepath"), _X("NewRelic.Agent.Core.AgentApi"), _X("SomeMethod"), std::function<void()>());
                };

            Assert::ExpectException<FunctionManipulatorException>(func, L"an unsupported AgentCallStyle::Strategy should not be usable to load method info.");
        }

        // ThrowExceptionIfStackItemIsNull unconditionally emits a dup/branch/throw/label
        // sequence at IL-generation time; the CEE_BRTRUE it appends only affects behaviour of
        // the generated IL at CLR runtime, not which C++ statements execute here. A single
        // direct call is enough to exercise the call to ThrowException inside it.
        TEST_METHOD(throw_exception_if_stack_item_is_null_appends_a_conditional_throw)
        {
            auto tokenizer = std::make_shared<RecordingTokenizer>();
            auto instructions = std::make_shared<InstructionSet>(tokenizer, nullptr);

            TestableFunctionManipulator::CallThrowExceptionIfStackItemIsNull(instructions, _X("null check message"), true);

            Assert::IsTrue(tokenizer->Tokenized(_X(".ctor")), L"should append a call to construct and throw a System.Exception when the stack item is null");
        }

    private:
        Configuration::InstrumentationPointPtr CreateInstrumentationPointThatMatchesFunction(IFunctionPtr function)
        {
            Configuration::InstrumentationPointPtr instrumentationPoint(new Configuration::InstrumentationPoint());
            instrumentationPoint->AssemblyName = function->GetAssemblyName();
            instrumentationPoint->ClassName = function->GetTypeName();
            instrumentationPoint->MethodName = function->GetFunctionName();
            return instrumentationPoint;
        }
    };
}}}}
