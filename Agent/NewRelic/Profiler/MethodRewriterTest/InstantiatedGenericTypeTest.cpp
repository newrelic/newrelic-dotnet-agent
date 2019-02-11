#include "CppUnitTest.h"
#include "MockFunction.h"
#include "../MethodRewriter/InstantiatedGenericType.h"
#include "MockTokenizer.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
	TEST_CLASS(InstantiatedGenericTypeTest)
	{
	public:
		TEST_METHOD(GetMethodToken_class_bytes_1_class_argument)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = CreateGenericClassInstantiationSignature(1);
			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			
			tokenResolver->_typeGenericArgumentCount = 1;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<ClassType>(0), false));
			
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(), parameters, 1);
			

			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_instantiationSignature);
		}

		TEST_METHOD(GetMethodToken_class_bytes_2_class_arguments)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = CreateGenericClassInstantiationSignature(2);
			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();

			tokenResolver->_typeGenericArgumentCount = 2;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<ClassType>(0), false));
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(), parameters, 1);

			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_instantiationSignature);
		}

		TEST_METHOD(GetMethodToken_class_bytes_3_class_arguments)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = CreateGenericClassInstantiationSignature(3);
			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();

			tokenResolver->_typeGenericArgumentCount = 3;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<ClassType>(0), false));
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(), parameters, 1);

			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_instantiationSignature);
		}

		TEST_METHOD(GetMethodToken_default_method_bytes_0_parameters_and_void_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x20);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x00);	// number of method parameters
			expectedSignatureBytes->push_back(0x01);	// void return type

			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT), std::make_shared<VoidReturnType>(),
				std::make_shared<Parameters>(), 0);

			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}

		TEST_METHOD(GetMethodToken_default_method_bytes_1_generic_parameter_and_void_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x20);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of method parameters
			expectedSignatureBytes->push_back(0x01);	// void return type
			expectedSignatureBytes->push_back(ELEMENT_TYPE_MVAR); // generic type parameter
			expectedSignatureBytes->push_back(0x00);

			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			auto numberGenericClassParameters = 1;
			tokenResolver->_typeGenericArgumentCount = numberGenericClassParameters;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<MvarType>(0), false));
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_DEFAULT), std::make_shared<VoidReturnType>(),
				parameters, numberGenericClassParameters);


			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}

		TEST_METHOD(GetMethodToken_generic_method_bytes_0_parameters_and_void_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x30);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of generic parameters
			expectedSignatureBytes->push_back(0x00);	// number of method parameters
			expectedSignatureBytes->push_back(0x01);	// void return type

			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			auto numberGenericClassParameters = 1;
			tokenResolver->_typeGenericArgumentCount = numberGenericClassParameters;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(),
				std::make_shared<Parameters>(), numberGenericClassParameters);


			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}

		TEST_METHOD(GetMethodToken_generic_method_bytes_1_default_parameter_and_void_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x30);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of method parameters
			expectedSignatureBytes->push_back(0x01);	// void return type
			expectedSignatureBytes->push_back(ELEMENT_TYPE_CLASS); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?

			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			auto numberGenericClassParameters = 1;
			tokenResolver->_typeGenericArgumentCount = numberGenericClassParameters;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<ClassType>(0), false));
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(), parameters, numberGenericClassParameters);


			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}

		TEST_METHOD(GetMethodToken_generic_method_bytes_2_default_parameters_and_void_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x30);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of generic parameters
			expectedSignatureBytes->push_back(0x02);	// number of method parameters
			expectedSignatureBytes->push_back(0x01);	// void return type
			expectedSignatureBytes->push_back(ELEMENT_TYPE_CLASS); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?
			expectedSignatureBytes->push_back(ELEMENT_TYPE_CLASS); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?

			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			auto numberGenericClassParameters = 1;
			tokenResolver->_typeGenericArgumentCount = numberGenericClassParameters;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<ClassType>(Parameter::Kind::TYPED_PARAMETER), false));
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<ClassType>(Parameter::Kind::TYPED_PARAMETER), false));
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(), parameters, numberGenericClassParameters);


			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}

		TEST_METHOD(GetMethodToken_generic_method_bytes_1_generic_parameter_and_void_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x30);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of method parameters
			expectedSignatureBytes->push_back(0x01);	// void return type
			expectedSignatureBytes->push_back(ELEMENT_TYPE_MVAR); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?

			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			auto numberGenericClassParameters = 1;
			tokenResolver->_typeGenericArgumentCount = numberGenericClassParameters;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<MvarType>(0), false));
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(), parameters, numberGenericClassParameters);


			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}

		TEST_METHOD(GetMethodToken_generic_method_bytes_2_generic_parameters_and_void_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x30);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of generic parameters
			expectedSignatureBytes->push_back(0x02);	// number of method parameters
			expectedSignatureBytes->push_back(0x01);	// void return type
			expectedSignatureBytes->push_back(ELEMENT_TYPE_MVAR); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?
			expectedSignatureBytes->push_back(ELEMENT_TYPE_MVAR); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?

			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			auto numberGenericClassParameters = 1;
			tokenResolver->_typeGenericArgumentCount = numberGenericClassParameters;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<MvarType>(0), false));
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<MvarType>(0), false));
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(), parameters, numberGenericClassParameters);

			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}

		TEST_METHOD(GetMethodToken_generic_method_bytes_1_generic_and_1_default_parameter_and_void_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x30);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of generic parameters
			expectedSignatureBytes->push_back(0x02);	// number of method parameters
			expectedSignatureBytes->push_back(0x01);	// void return type
			expectedSignatureBytes->push_back(ELEMENT_TYPE_MVAR); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?
			expectedSignatureBytes->push_back(ELEMENT_TYPE_CLASS); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?


			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			auto numberGenericClassParameters = 1;
			tokenResolver->_typeGenericArgumentCount = numberGenericClassParameters;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<MvarType>(0), false));
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<ClassType>(0), false));
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(), parameters, numberGenericClassParameters);

			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}

		TEST_METHOD(GetMethodToken_generic_method_bytes_0_parameters_and_typed_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x30);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of generic parameters
			expectedSignatureBytes->push_back(0x00);	// number of method parameters
			expectedSignatureBytes->push_back(ELEMENT_TYPE_SZARRAY);	// Single-dimension array return type
			expectedSignatureBytes->push_back(ELEMENT_TYPE_OBJECT);		// It's an array of OBJECT

			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			auto numberGenericClassParameters = 1;
			tokenResolver->_typeGenericArgumentCount = numberGenericClassParameters;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;
			
			auto returnType = std::make_shared<TypedReturnType>(std::make_shared<SingleDimensionArrayType>(std::make_shared<ObjectType>()), false);
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), returnType,
				std::make_shared<Parameters>(), numberGenericClassParameters);

			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}

		TEST_METHOD(GetMethodToken_generic_method_bytes_1_generic_parameter_and_typed_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x30);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of method parameters
			expectedSignatureBytes->push_back(ELEMENT_TYPE_SZARRAY);	// Single-dimension array return type
			expectedSignatureBytes->push_back(ELEMENT_TYPE_OBJECT);		// It's an array of OBJECT
			expectedSignatureBytes->push_back(ELEMENT_TYPE_MVAR); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?

			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			auto numberGenericClassParameters = 1;
			tokenResolver->_typeGenericArgumentCount = numberGenericClassParameters;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<MvarType>(0), false));
			auto returnType = std::make_shared<TypedReturnType>(std::make_shared<SingleDimensionArrayType>(std::make_shared<ObjectType>()), false);
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), returnType, parameters, numberGenericClassParameters);

			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}

		TEST_METHOD(GetMethodToken_generic_method_bytes_1_class_parameter_and_1_method_param_and_void_return)
		{
			//Arrange
			ByteVectorPtr expectedSignatureBytes = std::make_shared<ByteVector>();
			expectedSignatureBytes->push_back(0x30);	// method has one or more generic parameters
			expectedSignatureBytes->push_back(0x01);	// number of generic parameters
			expectedSignatureBytes->push_back(0x02);	// number of method parameters
			expectedSignatureBytes->push_back(0x01);	// void return type
			expectedSignatureBytes->push_back(ELEMENT_TYPE_MVAR); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?
			expectedSignatureBytes->push_back(ELEMENT_TYPE_VAR); // a generic parameter
			expectedSignatureBytes->push_back(0x00);	// parameter index?

			auto tokenizer = std::make_shared<MockTokenizer>();
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			auto numberGenericClassParameters = 1;
			tokenResolver->_typeGenericArgumentCount = numberGenericClassParameters;
			function->_tokenResolver = tokenResolver;
			function->_tokenizer = tokenizer;

			auto parameters = std::make_shared<Parameters>();
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<MvarType>(0), false));
			parameters->push_back(std::make_shared<TypedParameter>(std::make_shared<VarType>(0), false));
			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(), parameters, numberGenericClassParameters);

			//Act
			InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			AssertAreEqual(expectedSignatureBytes, tokenizer->_methodBytes);
		}
		TEST_METHOD(GetMethodToken_ValidateMemberRefOrDefToken)
		{
			// Validates that the MemberRefOrDefToken that is sent to the MockTokenizer is actually retrieved.

			//Arrange
			const uint32_t ExpectedMemberRefOrDefToken = 0x123;
			auto tokenizer = std::make_shared<MockTokenizer>();
			tokenizer->_memberRefOrDefToken = ExpectedMemberRefOrDefToken;
			auto tokenResolver = std::make_shared<MockTokenResolver>();
			auto function = CreateMockFunctionOnGenericClass();
			function->_tokenizer = tokenizer;

			auto methodSignature = std::make_shared<MethodSignature>(true, false, static_cast<uint8_t>(CorCallingConvention::IMAGE_CEE_CS_CALLCONV_GENERIC), std::make_shared<VoidReturnType>(), std::make_shared<Parameters>(), 0);

			//Act
			uint32_t methodToken = InstantiatedGenericType::GetMethodToken(function, methodSignature);

			//Assert
			Assert::AreEqual(ExpectedMemberRefOrDefToken, methodToken);
		}


	private:

		void AssertAreEqual(ByteVectorPtr expected, ByteVectorPtr actual)
		{
			Assert::IsTrue(expected->size() == actual->size(), L"Byte vectors must be of equal size.");
			for (size_t i = 0; i < expected->size(); i++)
			{
				Assert::AreEqual(expected->at(i), actual->at(i));
			}
		}

		ByteVectorPtr CreateGenericClassInstantiationSignature(uint8_t numberOfArguments)
		{
			ByteVectorPtr bytes = std::make_shared<ByteVector>();
			bytes->push_back(0x15);
			bytes->push_back(0x12);
			bytes->push_back(0xc1);
			bytes->push_back(0x84);
			bytes->push_back(0x8d);
			bytes->push_back(0x14);
			if (numberOfArguments == 0)
			{
				bytes->push_back(0x00);
			}
			else
			{
				bytes->push_back(numberOfArguments);
				for (uint8_t i = 0; i < numberOfArguments; i++)
				{
					bytes->push_back(NewRelic::Profiler::CorElementType::ELEMENT_TYPE_VAR);
					bytes->push_back(i);
				}
			}

			return bytes;
		}


		ByteVectorPtr CreateStartOfGenericMethodSignature()
		{
			ByteVectorPtr bytes = std::make_shared<ByteVector>();
			bytes->push_back(0x30); // instance, generic
			//bytes->push_back(numberOfMethodArguments); // # method params (this might be # generic params)
			//bytes->push_back(numberOfMethodArguments); // # method params
			//bytes->push_back(0x01); // void return type
			return bytes;
		}

		ByteVectorPtr CreateMethodSignature(bool isGeneric, uint8_t numberOfMethodArguments)
		{
			ByteVectorPtr bytes = std::make_shared<ByteVector>();

			if (isGeneric)
			{
				bytes->push_back(0x30); // instance, generic
				bytes->push_back(numberOfMethodArguments); // # method params (this might be # generic params)
				bytes->push_back(numberOfMethodArguments); // # method params
				bytes->push_back(0x01); // void return type

				if (numberOfMethodArguments > 0)
				{
					for (uint8_t i = 0; i < numberOfMethodArguments; i++)
					{
						bytes->push_back(ELEMENT_TYPE_CLASS);
						bytes->push_back(0x00);
					}
				}
			}
			else
			{
				bytes->push_back(0x20); // instance
				if (numberOfMethodArguments == 0)
				{
					bytes->push_back(0x00);
				}
				else
				{
					bytes->push_back(numberOfMethodArguments); // # method params
					bytes->push_back(numberOfMethodArguments); // # method params
				}
				bytes->push_back(0x01); // void return type
			}
			return bytes;
		}

		MockFunctionPtr CreateMockFunctionOnGenericClass(std::wstring typeName = L"MyGenericClass`1", std::wstring methodName = L"MyGenericMethod")
		{
			auto function = std::make_shared<MockFunction>(true);
			//function->_isGenericType = true;
			function->_functionName = methodName;
			function->_typeName = typeName;
			return function;
		}
	};
}}}}
