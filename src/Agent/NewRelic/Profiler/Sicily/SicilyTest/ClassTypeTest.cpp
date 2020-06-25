/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "../ast/ClassType.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace sicily
{
    namespace ast
    {
        namespace Test
        {
            TEST_CLASS(ClassTypeTest)
            {
            public:
        
                TEST_METHOD(TestGetKind)
                {
                    ClassTypePtr classType(new ClassType(L"Foo", L"bar"));
                    Assert::IsTrue(classType->GetKind() == Type::Kind::kCLASS);
                }

                TEST_METHOD(TestGetAssembly)
                {
                    ClassTypePtr classType(new ClassType(L"Foo", L"bar"));
                    Assert::AreEqual(std::wstring(L"bar"), classType->GetAssembly());
                }

                TEST_METHOD(TestGetName)
                {
                    ClassTypePtr classType(new ClassType(L"Foo", L"bar"));
                    Assert::AreEqual(std::wstring(L"Foo"), classType->GetName());
                }

                TEST_METHOD(TestToString)
                {
                    ClassTypePtr classType(new ClassType(L"Foo", L"bar"));
                    Assert::AreEqual(std::wstring(L"class [bar]Foo"), classType->ToString());
                }
            };
        }
    }
}
