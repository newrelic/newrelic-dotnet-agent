// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "TestTemplates.h"
#include "../ast/Types.h"
#include "../Exceptions.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace sicily
{
    namespace ast
    {
        namespace Test
        {
            TEST_CLASS(ArrayTypeTest)
            {
            public:
                TEST_METHOD(TestGetKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    std::unique_ptr<ArrayType> arrayType(new ArrayType(primitiveType));
                    Assert::AreEqual(Type::Kind::kARRAY, arrayType->GetKind());
                }

                TEST_METHOD(TestGetElementKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    std::unique_ptr<ArrayType> arrayType(new ArrayType(primitiveType));
                    Assert::AreEqual(Type::Kind::kPRIMITIVE, arrayType->GetElementType()->GetKind());
                }

                TEST_METHOD(TestGetElement)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    std::unique_ptr<ArrayType> arrayType(new ArrayType(primitiveType));
                    PrimitiveTypePtr elementType = std::static_pointer_cast<PrimitiveType, Type>(arrayType->GetElementType());
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kBOOL, elementType->GetPrimitiveKind());
                }

                TEST_METHOD(TestPrimitiveArrayToString)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    std::unique_ptr<ArrayType> arrayType(new ArrayType(primitiveType));
                    Assert::AreEqual(std::wstring(L"bool[]"), arrayType->ToString());
                }

                TEST_METHOD(TestClassArrayToString)
                {
                    ClassTypePtr classType(new ClassType(L"Foo", L"bar"));
                    std::unique_ptr<ArrayType> arrayType(new ArrayType(classType));
                    Assert::AreEqual(std::wstring(L"class [bar]Foo[]"), arrayType->ToString());
                }

                TEST_METHOD(TestGenericArrayToString)
                {
                    TypeListPtr typeList(new TypeList());
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    ClassTypePtr classType(new ClassType(L"Baz", L"bar"));
                    typeList->Add(primitiveType);
                    typeList->Add(classType);
                    GenericTypePtr genericType(new GenericType(L"Foo", L"bar", typeList));
                    ArrayTypePtr arrayType(new ArrayType(genericType));
                    Assert::AreEqual(std::wstring(L"class [bar]Foo`2<bool, class [bar]Baz>[]"), arrayType->ToString());
                }
            };
        }
    }
}
