#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"

#include "../ast/GenericParamType.h"
#include "TestTemplates.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace Test
{
	TEST_CLASS(GenericParamTypeTest)
	{
	public:
		TEST_METHOD(TestGetKind)
		{
			GenericParamTypePtr genericParamType(new GenericParamType(GenericParamType::GenericParamKind::kTYPE, 2));
			Assert::AreEqual(Type::Kind::kGENERICPARAM, genericParamType->GetKind());
		}

		TEST_METHOD(TestClassGetParamKind)
		{
			GenericParamTypePtr genericParamType(new GenericParamType(GenericParamType::GenericParamKind::kTYPE, 2));
			// type param kind *must* be 0x13.  See ECMA-335 II.23.1.16
			Assert::AreEqual(0x13, int(genericParamType->GetGenericParamKind()));
		}

		TEST_METHOD(TestMethodGetParamKind)
		{
			GenericParamTypePtr genericParamType(new GenericParamType(GenericParamType::GenericParamKind::kMETHOD, 2));
			// method param kind *must* be 0x1e.  See ECMA-335 II.23.1.16
			Assert::AreEqual(0x1e, int(genericParamType->GetGenericParamKind()));
		}

		TEST_METHOD(TestGetNumber)
		{
			GenericParamTypePtr genericParamType(new GenericParamType(GenericParamType::GenericParamKind::kMETHOD, 2));
			Assert::AreEqual(uint32_t(2), genericParamType->GetNumber());
		}
	};
}
