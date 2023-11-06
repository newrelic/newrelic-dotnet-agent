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
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    Assert::AreEqual(Type::Kind::kPRIMITIVE, primitiveType->GetKind());
                }

                TEST_METHOD(TestCharGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kCHAR, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kCHAR, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestObjectGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kOBJECT, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kOBJECT, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestVoidGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kVOID, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kVOID, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestStringGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kSTRING, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kSTRING, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test1ByteIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kI1, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kI1, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test2ByteIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kI2, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kI2, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test4ByteIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kI4, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kI4, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test8ByteIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kI8, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kI8, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test1ByteUnsignedIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kU1, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kU1, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test2ByteUnsignedIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kU2, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kU2, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test4ByteUnsignedIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kU4, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kU4, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test8ByteUnsignedIntegerGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kU8, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kU8, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test4ByteFloatGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kR4, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kR4, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(Test8ByteFloatGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kR8, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kR8, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestIntPtrGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kINTPTR, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kINTPTR, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestUIntPtrGetPrimitiveKind)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(PrimitiveType::PrimitiveKind::kUINTPTR, false));
                    Assert::AreEqual(PrimitiveType::PrimitiveKind::kUINTPTR, primitiveType->GetPrimitiveKind());
                }

                TEST_METHOD(TestToString)
                {
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, _X("bool"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kCHAR, _X("char"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kI1, _X("int8"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kI2, _X("int16"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kI4, _X("int32"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kI8, _X("int64"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kINTPTR, _X("unknown"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kOBJECT, _X("object"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kR4, _X("float32"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kR8, _X("float64"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kSTRING, _X("string"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kU1, _X("unsigned int8"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kU2, _X("unsigned int16"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kU4, _X("unsigned int32"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kU8, _X("unsigned int64"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kUINTPTR, _X("unknown"));
                    TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind::kVOID, _X("void"));
                }

            private:
                void TestToStringForPrimitiveType(PrimitiveType::PrimitiveKind primitiveKind, const xstring_t& expectedString)
                {
                    PrimitiveTypePtr primitiveType(new PrimitiveType(primitiveKind, false));
                    PrimitiveTypePtr primitiveTypeReference(new PrimitiveType(primitiveKind, true));
                    Assert::AreEqual(expectedString, primitiveType->ToString());
                    Assert::AreEqual(expectedString + _X("&"), primitiveTypeReference->ToString());
                }
            };
        }
    }
}
