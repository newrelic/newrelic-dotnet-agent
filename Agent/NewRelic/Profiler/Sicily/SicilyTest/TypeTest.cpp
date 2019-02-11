#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "TestTemplates.h"
#include "../ast/Type.h"
#include "../Exceptions.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace sicily
{
	namespace ast
	{
		namespace Test
		{
			TEST_CLASS(TypeTest)
			{
			public:
				TEST_METHOD(TestArrayGetKind)
				{
					std::unique_ptr<TypeImplementation> arrayType(new TypeImplementation(Type::Kind::kARRAY));
					Assert::AreEqual(Type::Kind::kARRAY, arrayType->GetKind());
				}

				TEST_METHOD(TestClassGetKind)
				{
					std::unique_ptr<TypeImplementation> classType(new TypeImplementation(Type::Kind::kCLASS));
					Assert::AreEqual(Type::Kind::kCLASS, classType->GetKind());
				}

				TEST_METHOD(TestGenericGetKind)
				{
					std::unique_ptr<TypeImplementation> genericType(new TypeImplementation(Type::Kind::kGENERICCLASS));
					Assert::AreEqual(Type::Kind::kGENERICCLASS, genericType->GetKind());
				}

				TEST_METHOD(TestMethodGetKind)
				{
					std::unique_ptr<TypeImplementation> methodType(new TypeImplementation(Type::Kind::kMETHOD));
					Assert::AreEqual(Type::Kind::kMETHOD, methodType->GetKind());
				}

				TEST_METHOD(TestPrimitiveGetKind)
				{
					std::unique_ptr<TypeImplementation> primitiveType(new TypeImplementation(Type::Kind::kPRIMITIVE));
					Assert::AreEqual(Type::Kind::kPRIMITIVE, primitiveType->GetKind());
				}

			private:
				class TypeImplementation : public Type
				{
				public:
					TypeImplementation(Type::Kind kind) : Type(kind) {}
					virtual std::wstring ToString() const override final { throw NotImplementedException(); }
				};
			};
		}
	}
}
