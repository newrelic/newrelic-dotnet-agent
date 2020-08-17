// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "TestTemplates.h"
#include "../ast/PrimitiveType.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace sicily
{
    namespace ast
    {
        namespace Test
        {
            TEST_CLASS(PrimitiveTypeTest)
            {
            public:
                TEST_METHOD(TestGetKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL));
                    Assert::AreEqual(Type::Kind::kPRIMITIVE, primitiveType->GetKind());
                }

                TEST_METHOD(TestCharGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kCHAR));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kCHAR, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestObjectGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kOBJECT));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kOBJECT, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestVoidGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kVOID));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kVOID, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestStringGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kSTRING));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kSTRING, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test1ByteIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kI1));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kI1, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test2ByteIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kI2));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kI2, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test4ByteIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kI4));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kI4, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test8ByteIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kI8));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kI8, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test1ByteUnsignedIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kU1));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kU1, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test2ByteUnsignedIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kU2));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kU2, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test4ByteUnsignedIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kU4));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kU4, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test8ByteUnsignedIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kU8));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kU8, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test4ByteFloatGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kR4));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kR4, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test8ByteFloatGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kR8));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kR8, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestIntPtrGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kINTPTR));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kINTPTR, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestUIntPtrGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kUINTPTR));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kUINTPTR, primitiveType->GetPrimitiveKind());
                }
            };
        }
    }
}
