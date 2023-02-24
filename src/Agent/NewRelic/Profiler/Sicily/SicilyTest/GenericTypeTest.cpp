// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "TestTemplates.h"
#include "../ast/Types.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace sicily
{
    namespace ast
    {
        namespace Test
        {
            TEST_CLASS(GenericTypeTest)
            {
            public:
        
                TEST_METHOD(TestGetKind)
                {
                    GenericTypePtr genericType = CreateGenericType();
                    Assert::IsTrue(Type::Kind::kGENERICCLASS == genericType->GetKind());
                }

                TEST_METHOD(TestGetAssembly)
                {
                    GenericTypePtr genericType = CreateGenericType();
                    Assert::AreEqual(std::wstring(L"bar"), genericType->GetAssembly());
                }

                TEST_METHOD(TestGetName)
                {
                    GenericTypePtr genericType = CreateGenericType();
                    Assert::AreEqual(std::wstring(L"Foo"), genericType->GetName());
                }

                TEST_METHOD(TestGenericTypeCount)
                {
                    GenericTypePtr genericType = CreateGenericType();
                    TypeListPtr genericTypeList = genericType->GetGenericTypes();
                    Assert::AreEqual(uint16_t(2), genericTypeList->GetSize());
                }

                TEST_METHOD(TestGenericTypes)
                {
                    GenericTypePtr genericType = CreateGenericType();
                    TypeListPtr genericTypeList = genericType->GetGenericTypes();
                    Assert::AreEqual(Type::Kind::kPRIMITIVE, genericTypeList->GetItem(0)->GetKind());
                    Assert::AreEqual(Type::Kind::kCLASS, genericTypeList->GetItem(1)->GetKind());
                }

                TEST_METHOD(TestToString)
                {
                    GenericTypePtr genericType = CreateGenericType();
                    Assert::AreEqual(std::wstring(L"class [bar]Foo`2<bool, class [bar]Faz>"), genericType->ToString());
                }

            private:
                GenericTypePtr CreateGenericType()
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    ClassTypePtr classType(new ClassType(L"Faz", L"bar"));
                    TypeListPtr typeList(new TypeList());
                    typeList->Add(primitiveType);
                    typeList->Add(classType);
                    GenericTypePtr genericType(new GenericType(L"Foo", L"bar", typeList));
                    return genericType;
                }
            };
        }
    }
}
