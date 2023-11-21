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
            TEST_CLASS(MethodTypeTest)
            {
            public:
                TEST_METHOD(TestGetKind)
                {
                    MethodTypePtr methodType = CreateSimpleMethodType();
                    Assert::AreEqual(Type::Kind::kMETHOD, methodType->GetKind());
                }

                TEST_METHOD(TestGetMethodName)
                {
                    MethodTypePtr methodType = CreateSimpleMethodType();
                    Assert::AreEqual(std::wstring(L"MyMethod"), methodType->GetMethodName());
                }

                TEST_METHOD(TestGetTargetType)
                {
                    MethodTypePtr methodType = CreateSimpleMethodType();
                    Assert::AreEqual(std::wstring(L"class [MyAssembly]MyClass"), methodType->GetTargetType()->ToString());
                }

                TEST_METHOD(TestGetParameterCount)
                {
                    MethodTypePtr methodType = CreateSimpleMethodType();
                    Assert::AreEqual(uint16_t(2), methodType->GetArgTypes()->GetSize());
                }

                TEST_METHOD(TestGetGenericCount)
                {
                    MethodTypePtr methodType = CreateComplexMethodType();
                    Assert::AreEqual(uint16_t(1), methodType->GetGenericTypes()->GetSize());
                }

                TEST_METHOD(TestToString)
                {
                    MethodTypePtr methodType = CreateComplexMethodType();
                    Assert::AreEqual(std::wstring(L"instance class [MyAssembly]MyGenericReturnClass`2<object[], string> class [MyAssembly]MyClass`1<unsigned int64[]>::MyMethod<class [MyAssembly]MyGenericClass`1<class [MyAssembly]MyNestedGenericClass`1<bool[]>>>(object, bool)"), methodType->ToString());
                }

                TEST_METHOD(TestIsInstanceMethod)
                {
                    MethodTypePtr nonInstanceMethodType = CreateSimpleMethodType();
                    Assert::IsFalse(nonInstanceMethodType->IsInstanceMethod());

                    MethodTypePtr instanceMethodType = CreateComplexMethodType();
                    Assert::IsTrue(instanceMethodType->IsInstanceMethod());
                }

            private:
                static MethodTypePtr CreateSimpleMethodType()
                {
                    ClassTypePtr targetType(new ClassType(L"MyClass", L"MyAssembly"));
                    TypePtr returnType(new PrimitiveType(PrimitiveType::PrimitiveKind::kSTRING, false));
                    TypePtr parameterType1(new PrimitiveType(PrimitiveType::PrimitiveKind::kOBJECT, false));
                    TypePtr parameterType2(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    TypeListPtr parameterTypes(new TypeList());
                    parameterTypes->Add(parameterType1);
                    parameterTypes->Add(parameterType2);
                    MethodTypePtr methodType(new MethodType(targetType, L"MyMethod", returnType, false, parameterTypes));
                    return methodType;
                }

                static MethodTypePtr CreateComplexMethodType()
                {
                    PrimitiveTypePtr u8Type(new PrimitiveType(PrimitiveType::PrimitiveKind::kU8, false));
                    ArrayTypePtr u8ArrayType(new ArrayType(u8Type));
                    TypeListPtr targetTypeGenericArguments(new TypeList());
                    targetTypeGenericArguments->Add(u8ArrayType);
                    GenericTypePtr targetType(new GenericType(L"MyClass", L"MyAssembly", targetTypeGenericArguments));
                    
                    PrimitiveTypePtr stringType(new PrimitiveType(PrimitiveType::PrimitiveKind::kSTRING, false));
                    PrimitiveTypePtr objectType(new PrimitiveType(PrimitiveType::PrimitiveKind::kOBJECT, false));
                    ArrayTypePtr objectArrayType(new ArrayType(objectType));
                    TypeListPtr returnTypeGenericArguments(new TypeList());
                    returnTypeGenericArguments->Add(objectArrayType);
                    returnTypeGenericArguments->Add(stringType);
                    GenericTypePtr returnType(new GenericType(L"MyGenericReturnClass", L"MyAssembly", returnTypeGenericArguments));
                    
                    TypePtr parameterType1(new PrimitiveType(PrimitiveType::PrimitiveKind::kOBJECT, false));
                    TypePtr parameterType2(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    TypeListPtr parameterTypes(new TypeList());
                    parameterTypes->Add(parameterType1);
                    parameterTypes->Add(parameterType2);

                    PrimitiveTypePtr boolType(new PrimitiveType(PrimitiveType::PrimitiveKind::kBOOL, false));
                    ArrayTypePtr boolArrayType(new ArrayType(boolType));
                    TypeListPtr nestedGenericArguments(new TypeList());
                    nestedGenericArguments->Add(boolArrayType);
                    GenericTypePtr nestedGenericType(new GenericType(L"MyNestedGenericClass", L"MyAssembly", nestedGenericArguments));
                    TypeListPtr genericArguments(new TypeList());
                    genericArguments->Add(nestedGenericType);
                    GenericTypePtr genericType(new GenericType(L"MyGenericClass", L"MyAssembly", genericArguments));
                    TypeListPtr methodGenericArguments(new TypeList());
                    methodGenericArguments->Add(genericType);
                    
                    MethodTypePtr methodType(new MethodType(targetType, L"MyMethod", returnType, true, parameterTypes, methodGenericArguments));
                    return methodType;
                }
            };
        }
    }
}
