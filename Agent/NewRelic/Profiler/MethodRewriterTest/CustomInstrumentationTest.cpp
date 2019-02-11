#include <stdint.h>
#include <memory>
#include <exception>
#include <functional>
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include "CppUnitTest.h"
#include "../MethodRewriter/CustomInstrumentation.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
	TEST_CLASS(CustomInstrumentationTest)
	{
	public:
		TEST_METHOD(builder_works)
		{
			CustomInstrumentationBuilder builder;
			builder.AddCustomInstrumentationXml(L"test", L"<extension/>");
			auto instruments = builder.Build();

			Assert::AreEqual((size_t)1, instruments->size());

			auto emptyInstruments = builder.Build();
			Assert::AreEqual((size_t)0, emptyInstruments->size());

			Assert::AreEqual((size_t)1, instruments->size());
		}
	};
}}}}