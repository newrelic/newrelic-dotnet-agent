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
                Scanner scanner(testString);
                Parser parser;
                ast::TypePtr rootType = parser.Parse(scanner);
                Assert::IsTrue(rootType != nullptr, L"rootType was nullptr");
                Assert::AreEqual(std::wstring(testString), rootType->ToString());
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
        };
    }
}
