// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "../Parser.h"
#include "../Exceptions.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace sicily
{
    namespace Test
    {
        TEST_CLASS(ParserTest)
        {
        private:
            void TestParser(std::wstring testString)
            {
                TestParser(testString, testString);
            }

            void TestParser(const xstring_t& testString, const xstring_t& expectedParsedString)
            {
                Scanner scanner(testString);
                Parser parser;
                ast::TypePtr rootType = parser.Parse(scanner);
                Assert::IsTrue(rootType != nullptr, L"rootType was nullptr");
                Assert::AreEqual(expectedParsedString, rootType->ToString());
            }

        public:
            TEST_METHOD(TestGenericMethod)
            {
                TestParser(L"instance void MyClass::MyMethod<bool>()");
            }

            TEST_METHOD(TestGenericType)
            {
                TestParser(L"instance void class MyClass`1<bool>::MyMethod()");
            }

            TEST_METHOD(TestGenericTypeParam)
            {
                TestParser(L"instance !0 class MyClass`1<bool>::MyMethod()");
            }

            TEST_METHOD(TestGenericMethodParam)
            {
                TestParser(L"!!0 MyClass::MyMethod<bool>()");
            }

            TEST_METHOD(TestGenericTypeParamAsGenericMethodArg)
            {
                TestParser(L"instance void class MyClass`1<bool>::MyMethod<!0>()");
            }

            TEST_METHOD(TestParser1)
            {
                TestParser(L"void");
            }

            TEST_METHOD(TestParser2)
            {
                TestParser(L"string");
            }

            TEST_METHOD(TestParser3)
            {
                TestParser(L"object[]");
            }

            TEST_METHOD(TestParser4)
            {
                TestParser(L"class [MyAssembly]MyClass");
            }

            TEST_METHOD(TestParser5)
            {
                TestParser(L"class [MyAssembly]MyNamespace.MyClass");
            }

            TEST_METHOD(TestParser6)
            {
                TestParser(L"class [MyAssembly]MyNamespace.MyNamespace2.MyClass");
            }

            TEST_METHOD(TestParser7)
            {
                TestParser(L"class MyClass");
            }

            TEST_METHOD(TestParser8)
            {
                TestParser(L"class MyNamespace.MyClass");
            }

            TEST_METHOD(TestParser9)
            {
                TestParser(L"class MyNamespace.MyNamespace2.MyClass");
            }

            TEST_METHOD(TestParser10)
            {
                TestParser(L"void [MyAssembly]MyClass::MyMethod()");
            }

            TEST_METHOD(TestParser11)
            {
                TestParser(L"void [MyAssembly]MyNamespace.MyClass::MyMethod()");
            }

            TEST_METHOD(TestParser12)
            {
                TestParser(L"void [MyAssembly]MyNamespace.MyNamespace2.MyClass::MyMethod()");
            }

            TEST_METHOD(TestParser13)
            {
                TestParser(L"void [MyAssembly]MyNamespace.MyNamespace2.MyClass::MyMethod(bool)");
            }

            TEST_METHOD(TestParser14)
            {
                TestParser(L"void [MyAssembly]MyNamespace.MyNamespace2.MyClass::MyMethod(bool, string)");
            }

            TEST_METHOD(TestParser15)
            {
                TestParser(L"void [MyAssembly]MyNamespace.MyNamespace2.MyClass::MyMethod(bool, object[], string)");
            }

            TEST_METHOD(TestParser16)
            {
                TestParser(L"void [MyAssembly]MyNamespace.MyNamespace2.MyClass::MyMethod(class [MyAssembly2]MyNamespace3.MyNamespace4.MyClass2)");
            }

            TEST_METHOD(TestParser17)
            {
                TestParser(L"void [MyAssembly]MyNamespace.MyNamespace2.MyClass::MyMethod(class [MyAssembly2]MyNamespace3.MyNamespace4.MyClass2[], string)");
            }

            TEST_METHOD(TestParser18)
            {
                TestParser(L"instance void MyClass::MyMethod()");
            }

            TEST_METHOD(TestParser19)
            {
                TestParser(L"instance class MyClass MyClass2::MyMethod()");
            }

            TEST_METHOD(TestParser20)
            {
                TestParser(L"instance class [MyAssembly]MyNamespace.MyNamespace2.MyClass [MyAssembly]MyNamespace3.MyNamespace4.MyClass2::MyMethod()");
            }

            TEST_METHOD(TestParser21)
            {
                TestParser(L"void MyClass::MyMethod<object>()");
            }

            TEST_METHOD(TestParser22)
            {
                TestParser(L"instance class [MyAssembly]MyNamespace.MyClass`2<class [MyAssembly]MyNamespace.MyClass2`1<object[]>, class [MyAssembly]MyNamespace.MyClass2`1<object[]>> [MyAssembly]MyNamespace.MyNamespace2.MyClass::MyMethod<class MyClass`1<object[]>, class MyClass2`1<string[]>>(class MyClass)");
            }

            TEST_METHOD(TestParser23)
            {
                TestParser(L"class MyClass`1<class MyClass2`1<class MyClass3`1<class MyClass4`1<bool>[]>>[]>");
            }

            TEST_METHOD(TestParser24)
            {
                TestParser(L"!0 class MyClass`1<string>::MyMethod<object>(!!0)");
            }

            TEST_METHOD(TestParser25)
            {
                TestParser(L"void class MyClass`1<void>::MyMethod<void>(void)");
            }

            TEST_METHOD(TestParser26)
            {
                TestParser(L"class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)");
            }

            TEST_METHOD(TestParser27)
            {
                TestParser(L"instance !0 class [mscorlib]System.Tuple`2<class [mscorlib]System.Action`1<object[]>, class [mscorlib]System.Action`1<object[]>>::get_Item1()");
            }

            TEST_METHOD(TestField)
            {
                TestParser(L"int32 __NRInitializer__::_isAgentAssemblyLoaded");
            }

            TEST_METHOD(TestVolatileField)
            {
                TestParser(L"object modreq(System.Runtime.CompilerServices.IsVolatile) __NRInitializer__::_myField");
            }

            TEST_METHOD(TestMethodWithRefParams)
            {
                TestParser(L"int32 System.Threading.Interlocked::CompareExchange(int32&, int32, int32)");
            }

            TEST_METHOD(EmptyStringToParseThrowsException)
            {
                Assert::ExpectException<UnexpectedEndTokenException>([this]() {TestParser(_X("")); });
            }

            TEST_METHOD(ParsingIncompleteInstanceMethodThrowsException)
            {
                Assert::ExpectException<UnexpectedEndTokenException>([this]() {TestParser(_X("instance void")); });
            }

            TEST_METHOD(ParsingInvalidMethodTypeThrowsException)
            {
                Assert::ExpectException<UnexpectedTypeKindException>([this]() {TestParser(_X("instance void void")); });
            }

            TEST_METHOD(ParseComplexClassName)
            {
                TestParser(_X("class [assemblyname]Foo1_2Bar.__something"));
            }

            TEST_METHOD(ParseClassNameWithSlashes)
            {
                TestParser(_X("class [assemblyname]Foo/Bar"));
            }

            TEST_METHOD(ParseClassNameWithSpacesBeforeDotss)
            {
                // Foo .Bar -> Foo.Bar
                TestParser(_X("class [assemblyname]Foo .Bar"), _X("class [assemblyname]Foo.Bar"));
            }

            TEST_METHOD(ParsingGenericClassWithIncorrectGenericCountThrowsException)
            {
                Assert::ExpectException<GenericArgumentCountMismatchException>([this]() { TestParser(_X("class [mscorlib]System.Action`1<object[], object>")); });
            }

            TEST_METHOD(ParsingRawClassNameThrowsException)
            {
                Assert::ExpectException<ExpectedTypeDescriptorException>([this]() { TestParser(_X("instance Foo")); });
            }

            TEST_METHOD(ParsingUnexpectedTokenThrowsException)
            {
                Assert::ExpectException<UnhandledTokenException>([this]() { TestParser(_X("instance ,")); });
            }

            TEST_METHOD(ParseMethodWithManyTypes)
            {
                TestParser(_X("void MyClass::MyMethod<object>(bool, uint32, int32, string&, valuetype Foo)"), _X("void MyClass::MyMethod<object>(bool, unsigned int32, int32, string&, valuetype Foo)"));
            }

            TEST_METHOD(ParsingBadAssemblyNameThrowsException)
            {
                Assert::ExpectException<UnexpectedTokenException>([this]() { TestParser(_X("[BadAssembly")); });
            }

            TEST_METHOD(ParsingBadColonPlacementThrowsException)
            {
                Assert::ExpectException<UnexpectedEndOfStreamException>([this]() { TestParser(_X("Foo:")); });
                Assert::ExpectException<UnexpectedCharacterException>([this]() { TestParser(_X("Foo:Bar")); });
            }

            TEST_METHOD(ParsingUnhandledCharacterThrowsException)
            {
                Assert::ExpectException<UnhandledCharacterException>([this]() { TestParser(_X("^")); });
            }

            TEST_METHOD(ParsingTracerFuncType)
            {
                // The expected signature transforms uint32 and uint64 to unsigned int32 and unsigned int64
                TestParser(_X("class [System.Private.CoreLib]System.Func`12<string, uint32, string, string, class [System.Private.CoreLib]System.Type, string, string, string, object, object[], uint64, class [System.Private.CoreLib]System.Action`2<object, class [System.Private.CoreLib]System.Exception>>"),
                    _X("class [System.Private.CoreLib]System.Func`12<string, unsigned int32, string, string, class [System.Private.CoreLib]System.Type, string, string, string, object, object[], unsigned int64, class [System.Private.CoreLib]System.Action`2<object, class [System.Private.CoreLib]System.Exception>>"));
            }
        };
    }
}
