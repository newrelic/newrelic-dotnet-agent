// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "../Common/Macros.h"
#include "MockFunction.h"
#include "../MethodRewriter/RuntimeAsyncReturnType.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    // A .NET 11 runtime-async method returns the *unwrapped* type its task wraps rather than
    // the task type its metadata signature declares (runtime-async.md, I.8.4.5). These tests
    // pin down the mapping the IL rewriter depends on:
    //
    //   Task, ValueTask          -> void  (nothing is pushed before ret)
    //   Task<T>, ValueTask<T>    -> T
    //   anything else            -> nullptr, meaning "shape we do not understand, do not rewrite"
    //
    // The nullptr cases matter as much as the positive ones. The Async impl flag can be set but
    // inert -- the spec says it "only has effect" on Task/ValueTask returns -- and rewriting the
    // return handling of a method whose flag is inert would *introduce* an InvalidProgramException
    // into a method the profiler handles correctly today. See NR-610232.
    TEST_CLASS(RuntimeAsyncReturnTypeTest)
    {
    private:
        static const wchar_t* const TaskName;
        static const wchar_t* const ValueTaskName;
        static const wchar_t* const GenericTaskName;
        static const wchar_t* const GenericValueTaskName;

        // MockTokenResolver answers every token with the same string, which is all these tests
        // need: the only token whose name decides the outcome is the outer Task/ValueTask. A type
        // argument is either a primitive (which never consults the resolver) or -- in the nested
        // case -- a task itself, where the shared answer is the correct one anyway.
        static SignatureParser::ITokenResolverPtr MakeResolver(const wchar_t* typeString)
        {
            auto resolver = std::make_shared<MockTokenResolver>();
            resolver->_typeString = typeString;
            return resolver;
        }

        static SignatureParser::ReturnTypePtr Returns(SignatureParser::TypePtr type, bool isByRef = false)
        {
            return std::make_shared<SignatureParser::TypedReturnType>(type, isByRef);
        }

        // ValueTask and ValueTask`1 are structs, so they arrive as ELEMENT_TYPE_VALUETYPE rather
        // than ELEMENT_TYPE_CLASS. Recognition must not care which.
        static SignatureParser::TypePtr Class(uint32_t token = 0x01000001)
        {
            return std::make_shared<SignatureParser::ClassType>(token);
        }

        static SignatureParser::TypePtr ValueType(uint32_t token = 0x01000002)
        {
            return std::make_shared<SignatureParser::ValueTypeType>(token);
        }

        static SignatureParser::TypePtr GenericOf(SignatureParser::TypePtr outer, SignatureParser::TypePtr argument)
        {
            auto arguments = std::make_shared<SignatureParser::Types>();
            arguments->push_back(argument);
            return std::make_shared<SignatureParser::GenericType>(outer, arguments);
        }

        static void AssertIsVoid(SignatureParser::ReturnTypePtr actual, const wchar_t* message)
        {
            Assert::IsNotNull(actual.get(), message);
            Assert::IsTrue(actual->_kind == SignatureParser::ReturnType::Kind::VOID_RETURN_TYPE, message);
        }

        // The effective return type feeds AppendToLocalsSignature via ToBytes(), so byte equality
        // with the type argument -- not just kind equality -- is what actually has to hold.
        static void AssertSameBytes(SignatureParser::ReturnTypePtr actual, SignatureParser::TypePtr expected, const wchar_t* message)
        {
            Assert::IsNotNull(actual.get(), message);
            auto actualBytes = actual->ToBytes();
            auto expectedBytes = expected->ToBytes();
            Assert::AreEqual(expectedBytes->size(), actualBytes->size(), message);
            for (size_t i = 0; i < expectedBytes->size(); ++i)
            {
                Assert::AreEqual(uint32_t(expectedBytes->at(i)), uint32_t(actualBytes->at(i)), message);
            }
        }

    public:
        TEST_METHOD(task_is_effectively_void)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(Class()), MakeResolver(TaskName));
            AssertIsVoid(actual, L"async Task pushes nothing before ret, so its effective return type is void");
        }

        TEST_METHOD(value_task_is_effectively_void)
        {
            // ValueTask is a struct; this is the ELEMENT_TYPE_VALUETYPE path
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(ValueType()), MakeResolver(ValueTaskName));
            AssertIsVoid(actual, L"async ValueTask pushes nothing before ret, so its effective return type is void");
        }

        TEST_METHOD(generic_task_is_effectively_its_type_argument)
        {
            auto argument = std::make_shared<SignatureParser::Int32Type>();
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(GenericOf(Class(), argument)), MakeResolver(GenericTaskName));
            AssertSameBytes(actual, argument, L"async Task<int> pushes an int before ret");
        }

        TEST_METHOD(generic_value_task_is_effectively_its_type_argument)
        {
            auto argument = std::make_shared<SignatureParser::StringType>();
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(GenericOf(ValueType(), argument)), MakeResolver(GenericValueTaskName));
            AssertSameBytes(actual, argument, L"async ValueTask<string> pushes a string before ret");
        }

        TEST_METHOD(generic_task_of_a_generic_parameter_is_effectively_that_parameter)
        {
            // async Task<T> on a generic method -- the type argument is !!0, not a concrete type
            auto argument = std::make_shared<SignatureParser::MvarType>(0);
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(GenericOf(Class(), argument)), MakeResolver(GenericTaskName));
            AssertSameBytes(actual, argument, L"async Task<!!0> pushes the method generic parameter before ret");
        }

        TEST_METHOD(generic_task_of_a_type_generic_parameter_is_effectively_that_parameter)
        {
            auto argument = std::make_shared<SignatureParser::VarType>(0);
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(GenericOf(Class(), argument)), MakeResolver(GenericTaskName));
            AssertSameBytes(actual, argument, L"async Task<!0> pushes the type generic parameter before ret");
        }

        TEST_METHOD(nested_generic_task_unwraps_exactly_one_level)
        {
            // async Task<Task<int>> genuinely returns a Task<int>; unwrapping twice would be wrong
            auto inner = GenericOf(Class(), std::make_shared<SignatureParser::Int32Type>());
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(GenericOf(Class(), inner)), MakeResolver(GenericTaskName));
            AssertSameBytes(actual, inner, L"Task<Task<int>> unwraps to Task<int>, not to int");
        }

        TEST_METHOD(non_task_class_return_type_is_unrecognized)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(Class()), MakeResolver(L"MyNamespace.MyClass"));
            Assert::IsNull(actual.get(), L"an inert Async flag on a non-task return must not be rewritten");
        }

        TEST_METHOD(primitive_return_type_is_unrecognized)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(std::make_shared<SignatureParser::Int32Type>()), MakeResolver(TaskName));
            Assert::IsNull(actual.get(), L"an int-returning method cannot be runtime-async");
        }

        TEST_METHOD(void_return_type_is_unrecognized)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(std::make_shared<SignatureParser::VoidReturnType>(), MakeResolver(TaskName));
            Assert::IsNull(actual.get(), L"async void is not expressible under runtime-async");
        }

        TEST_METHOD(typed_by_ref_return_type_is_unrecognized)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(std::make_shared<SignatureParser::TypedByRefReturnType>(), MakeResolver(TaskName));
            Assert::IsNull(actual.get(), L"a TypedReference return cannot be a task");
        }

        TEST_METHOD(by_ref_task_return_type_is_unrecognized)
        {
            // "ref Task" is not "async Task" -- the byref makes it a different convention
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(Class(), true), MakeResolver(TaskName));
            Assert::IsNull(actual.get(), L"a byref task return must not be treated as runtime-async");
        }

        TEST_METHOD(non_task_generic_return_type_is_unrecognized)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(
                Returns(GenericOf(Class(), std::make_shared<SignatureParser::Int32Type>())),
                MakeResolver(L"System.Collections.Generic.List`1"));
            Assert::IsNull(actual.get(), L"List<int> is generic but is not a task");
        }

        TEST_METHOD(generic_task_name_with_two_type_arguments_is_unrecognized)
        {
            // defends the arity assumption rather than trusting the name alone
            auto arguments = std::make_shared<SignatureParser::Types>();
            arguments->push_back(std::make_shared<SignatureParser::Int32Type>());
            arguments->push_back(std::make_shared<SignatureParser::Int32Type>());
            auto twoArgGeneric = std::make_shared<SignatureParser::GenericType>(Class(), arguments);
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(twoArgGeneric), MakeResolver(GenericTaskName));
            Assert::IsNull(actual.get(), L"a task type has exactly one type argument");
        }

        TEST_METHOD(non_generic_task_name_used_generically_is_unrecognized)
        {
            // the arity suffix is part of the metadata name, so "...Task" as a generic outer type
            // is a shape we have not seen and should decline rather than guess at
            auto actual = RuntimeAsync::GetEffectiveReturnType(
                Returns(GenericOf(Class(), std::make_shared<SignatureParser::Int32Type>())),
                MakeResolver(TaskName));
            Assert::IsNull(actual.get(), L"generic Task must carry its `1 arity suffix");
        }

        TEST_METHOD(generic_task_name_used_non_generically_is_unrecognized)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(Class()), MakeResolver(GenericTaskName));
            Assert::IsNull(actual.get(), L"non-generic Task must not carry an arity suffix");
        }

        TEST_METHOD(null_return_type_is_unrecognized)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(nullptr, MakeResolver(TaskName));
            Assert::IsNull(actual.get(), L"a missing return type must not crash the rewriter");
        }

        TEST_METHOD(null_resolver_is_unrecognized)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(Class()), nullptr);
            Assert::IsNull(actual.get(), L"a missing token resolver must not crash the rewriter");
        }

        // CorTokenResolver throws for TypeSpec tokens and unhandled token types. Name resolution
        // happens on the JIT path, so a throw must degrade to "unrecognized" -- which the caller
        // turns into a skip -- rather than escaping into the profiler callback.
        struct ThrowingTokenResolver : public SignatureParser::ITokenResolver
        {
            virtual std::wstring GetTypeStringsFromTypeDefOrRefOrSpecToken(uint32_t) override
            {
                throw std::runtime_error("token resolution failed");
            }

            virtual uint32_t GetTypeGenericArgumentCount(uint32_t) override
            {
                return 0;
            }
        };

        TEST_METHOD(resolver_that_throws_is_unrecognized)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(Returns(Class()), std::make_shared<ThrowingTokenResolver>());
            Assert::IsNull(actual.get(), L"a failed token lookup must degrade to a skip, not propagate");
        }

        TEST_METHOD(resolver_that_throws_on_a_generic_return_type_is_unrecognized)
        {
            auto actual = RuntimeAsync::GetEffectiveReturnType(
                Returns(GenericOf(Class(), std::make_shared<SignatureParser::Int32Type>())),
                std::make_shared<ThrowingTokenResolver>());
            Assert::IsNull(actual.get(), L"a failed token lookup on the outer task type must degrade to a skip");
        }
    };

    const wchar_t* const RuntimeAsyncReturnTypeTest::TaskName = L"System.Threading.Tasks.Task";
    const wchar_t* const RuntimeAsyncReturnTypeTest::ValueTaskName = L"System.Threading.Tasks.ValueTask";
    const wchar_t* const RuntimeAsyncReturnTypeTest::GenericTaskName = L"System.Threading.Tasks.Task`1";
    const wchar_t* const RuntimeAsyncReturnTypeTest::GenericValueTaskName = L"System.Threading.Tasks.ValueTask`1";
}}}}
