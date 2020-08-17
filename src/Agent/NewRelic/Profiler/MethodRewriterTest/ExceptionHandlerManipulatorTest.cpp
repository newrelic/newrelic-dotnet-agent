// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <CppUnitTest.h>
#include "TestTemplates.h"
#include "UnreferencedFunctions.h"
#include "../Common/Macros.h"
#include "../MethodRewriter/ExceptionHandlerManipulator.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace MethodRewriter { namespace Test
{
    TEST_CLASS(ExceptionHandlerManipulatorTest)
    {
    public:
        
        TEST_METHOD(FromLittleEndian8_0)
        {
            BYTEVECTOR(bytes, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian8(iterator);
            Assert::AreEqual(uint8_t(0), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian8_UINT8MAX)
        {
            BYTEVECTOR(bytes, 0xff);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian8(iterator);
            Assert::AreEqual(uint8_t(UINT8_MAX), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian16_0)
        {
            BYTEVECTOR(bytes, 0x00, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian16(iterator);
            Assert::AreEqual(uint16_t(0), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian16_UINT8MAX)
        {
            BYTEVECTOR(bytes, 0xff, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian16(iterator);
            Assert::AreEqual(uint16_t(UINT8_MAX), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian16_UINT8MAXplus1)
        {
            BYTEVECTOR(bytes, 0x00, 0x01);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian16(iterator);
            Assert::AreEqual(uint16_t(UINT8_MAX + 1), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian16_UINT16MAX)
        {
            BYTEVECTOR(bytes, 0xff, 0xff);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian16(iterator);
            Assert::AreEqual(uint16_t(UINT16_MAX), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian24_0)
        {
            BYTEVECTOR(bytes, 0x00, 0x00, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian24(iterator);
            Assert::AreEqual(uint32_t(0), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian24_UINT8MAX)
        {
            BYTEVECTOR(bytes, 0xff, 0x00, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian24(iterator);
            Assert::AreEqual(uint32_t(UINT8_MAX), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian24_UINT8MAXplus1)
        {
            BYTEVECTOR(bytes, 0x00, 0x01, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian24(iterator);
            Assert::AreEqual(uint32_t(UINT8_MAX + 1), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian24_UINT16MAX)
        {
            BYTEVECTOR(bytes, 0xff, 0xff, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian24(iterator);
            Assert::AreEqual(uint32_t(UINT16_MAX), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian24_UINT16MAXplus1)
        {
            BYTEVECTOR(bytes, 0x00, 0x00, 0x01);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian24(iterator);
            Assert::AreEqual(uint32_t(UINT16_MAX + 1), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian24_UINT24MAX)
        {
            BYTEVECTOR(bytes, 0xff, 0xff, 0xff);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian24(iterator);
            Assert::AreEqual(uint32_t(0xffffff), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian32_0)
        {
            BYTEVECTOR(bytes, 0x00, 0x00, 0x00, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian32(iterator);
            Assert::AreEqual(uint32_t(0), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian32_UINT8MAX)
        {
            BYTEVECTOR(bytes, 0xff, 0x00, 0x00, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian32(iterator);
            Assert::AreEqual(uint32_t(UINT8_MAX), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian32_UINT8MAXplus1)
        {
            BYTEVECTOR(bytes, 0x00, 0x01, 0x00, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian32(iterator);
            Assert::AreEqual(uint32_t(UINT8_MAX + 1), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian32_UINT16MAX)
        {
            BYTEVECTOR(bytes, 0xff, 0xff, 0x00, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian32(iterator);
            Assert::AreEqual(uint32_t(UINT16_MAX), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian32_UINT16MAXplus1)
        {
            BYTEVECTOR(bytes, 0x00, 0x00, 0x01, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian32(iterator);
            Assert::AreEqual(uint32_t(UINT16_MAX + 1), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian32_UINT24MAX)
        {
            BYTEVECTOR(bytes, 0xff, 0xff, 0xff, 0x00);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian32(iterator);
            Assert::AreEqual(uint32_t(0xffffff), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian32_UINT24MAXplus1)
        {
            BYTEVECTOR(bytes, 0x00, 0x00, 0x00, 0x01);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian32(iterator);
            Assert::AreEqual(uint32_t(0x1000000), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(FromLittleEndian32_UINT32MAX)
        {
            BYTEVECTOR(bytes, 0xff, 0xff, 0xff, 0xff);
            auto iterator = bytes.begin();
            auto result = ExceptionHandlingClause::FromLittleEndian32(iterator);
            Assert::AreEqual(uint32_t(UINT32_MAX), result);
            Assert::IsTrue(bytes.end() == iterator, L"Iterator did not make it to the end of the vector or iterated too far.");
        }

        TEST_METHOD(AppendLittleEndian_uint8_0)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint8_t(0), actualBytes);
            BYTEVECTOR(expectedBytes, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint8_UINT8MAX)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint8_t(UINT8_MAX), actualBytes);
            BYTEVECTOR(expectedBytes, 0xff);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint16_0)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint16_t(0), actualBytes);
            BYTEVECTOR(expectedBytes, 0x00, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint16_UINT8MAX)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint16_t(UINT8_MAX), actualBytes);
            BYTEVECTOR(expectedBytes, 0xff, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint16_UINT8MAXplus1)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint16_t(UINT8_MAX + 1), actualBytes);
            BYTEVECTOR(expectedBytes, 0x00, 0x01);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint16_UINT16MAX)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint16_t(UINT16_MAX), actualBytes);
            BYTEVECTOR(expectedBytes, 0xff, 0xff);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint24_0)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian24(uint32_t(0), actualBytes);
            BYTEVECTOR(expectedBytes, 0x00, 0x00, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint24_UINT8MAX)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian24(uint32_t(UINT8_MAX), actualBytes);
            BYTEVECTOR(expectedBytes, 0xff, 0x00, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint24_UINT8MAXplus1)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian24(uint32_t(UINT8_MAX + 1), actualBytes);
            BYTEVECTOR(expectedBytes, 0x00, 0x01, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint24_UINT16MAX)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian24(uint32_t(UINT16_MAX), actualBytes);
            BYTEVECTOR(expectedBytes, 0xff, 0xff, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint24_UINT16MAXplus1)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian24(uint32_t(UINT16_MAX + 1), actualBytes);
            BYTEVECTOR(expectedBytes, 0x00, 0x00, 0x01);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint24_UINT24MAX)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian24(uint32_t(0xffffff), actualBytes);
            BYTEVECTOR(expectedBytes, 0xff, 0xff, 0xff);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint32_0)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint32_t(0), actualBytes);
            BYTEVECTOR(expectedBytes, 0x00, 0x00, 0x00, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint32_UINT8MAX)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint32_t(UINT8_MAX), actualBytes);
            BYTEVECTOR(expectedBytes, 0xff, 0x00, 0x00, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint32_UINT8MAXplus1)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint32_t(UINT8_MAX + 1), actualBytes);
            BYTEVECTOR(expectedBytes, 0x00, 0x01, 0x00, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint32_UINT16MAX)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint32_t(UINT16_MAX), actualBytes);
            BYTEVECTOR(expectedBytes, 0xff, 0xff, 0x00, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint32_UINT16MAXplus1)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint32_t(UINT16_MAX + 1), actualBytes);
            BYTEVECTOR(expectedBytes, 0x00, 0x00, 0x01, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint32_UINT24MAX)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint32_t(0xffffff), actualBytes);
            BYTEVECTOR(expectedBytes, 0xff, 0xff, 0xff, 0x00);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint32_UINT24MAXplus1)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint32_t(0x1000000), actualBytes);
            BYTEVECTOR(expectedBytes, 0x00, 0x00, 0x00, 0x01);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(AppendLittleEndian_uint32_UINT32MAX)
        {
            ByteVector actualBytes;
            ExceptionHandlingClause::AppendLittleEndian(uint32_t(UINT32_MAX), actualBytes);
            BYTEVECTOR(expectedBytes, 0xff, 0xff, 0xff, 0xff);
            Assert::AreEqual(expectedBytes, actualBytes);
        }

        TEST_METHOD(no_small_exception_clauses)
        {
            BYTEVECTOR(extraSectionBytes, 0x01, 0x04, 0x00, 0x00);
            auto iterator = extraSectionBytes.begin();
            ExceptionHandlerManipulator manipulator(iterator);
            auto actualBytes = manipulator.GetExtraSectionBytes(0);
            BYTEVECTOR(expectedBytes, (0x1 | 0x40), 0x04, 0x00, 0x00);
            Assert::IsFalse(iterator < extraSectionBytes.end(), L"Iterator didn't read the entire section.");
            Assert::IsFalse(iterator > extraSectionBytes.end(), L"Iterator read more bytes than were in the section.");
            Assert::AreEqual(expectedBytes, *actualBytes);
        }

        TEST_METHOD(no_fat_exception_clauses)
        {
            BYTEVECTOR(extraSectionBytes, (0x01 | 0x40), 0x04, 0x00, 0x00);
            auto iterator = extraSectionBytes.begin();
            ExceptionHandlerManipulator manipulator(iterator);
            auto actualBytes = manipulator.GetExtraSectionBytes(0);
            BYTEVECTOR(expectedBytes, (0x1 | 0x40), 0x04, 0x00, 0x00);
            Assert::IsFalse(iterator < extraSectionBytes.end(), L"Iterator didn't read the entire section.");
            Assert::IsFalse(iterator > extraSectionBytes.end(), L"Iterator read more bytes than were in the section.");
            Assert::AreEqual(expectedBytes, *actualBytes);
        }

        TEST_METHOD(constructor_throws_on_more_sections)
        {
            try
            {
                BYTEVECTOR(extraSectionBytes, 0x80, 0x04, 0x00);
                auto iterator = extraSectionBytes.begin();
                ExceptionHandlerManipulator manipulator(iterator);
                Assert::Fail(L"Expected ExceptionHandlerManipulatorException to be thrown.");
            }
            catch (const ExceptionHandlerManipulatorException&) {}
            catch (...) { Assert::Fail(L"Expected ExceptionHandlerManipulatorException but caught something else."); }
        }

        TEST_METHOD(constructor_throws_on_non_exception_handling_table)
        {
            try
            {
                BYTEVECTOR(extraSectionBytes, 0x00, 0x04, 0x00);
                auto iterator = extraSectionBytes.begin();
                ExceptionHandlerManipulator manipulator(iterator);
                Assert::Fail(L"Expected ExceptionHandlerManipulatorException to be thrown.");
            }
            catch (const ExceptionHandlerManipulatorException&) {}
            catch (...) { Assert::Fail(L"Expected ExceptionHandlerManipulatorException but caught something else."); }
        }

        TEST_METHOD(one_small_exception_clause)
        {
            BYTEVECTOR(extraSectionBytes,
                0x01, // Kind
                0x10, // DataSize
                0x00, 0x00, // Reserved
                0x00, 0x00, // Flags
                0x00, 0x00, // TryOffset
                0x01, // TryLength
                0x02, 0x00, // HandlerOffset
                0x01, // HandlerLength
                0x00, 0x00, 0x00, 0x00 // classToken
                );
            auto iterator = extraSectionBytes.begin();
            ExceptionHandlerManipulator manipulator(iterator);
            auto actualBytes = manipulator.GetExtraSectionBytes(0);
            BYTEVECTOR(expectedBytes,
                0x01 | 0x40, // Kind
                0x1c, 0x00, 0x00, // DataSize
                0x00, 0x00, 0x00, 0x00, // Flags
                0x00, 0x00, 0x00, 0x00, // TryOffset
                0x01, 0x00, 0x00, 0x00, // TryLength
                0x02, 0x00, 0x00, 0x00, // HandlerOffset
                0x01, 0x00, 0x00, 0x00, // HandlerLength
                0x00, 0x00, 0x00, 0x00 // classToken
                );
            Assert::IsFalse(iterator < extraSectionBytes.end(), L"Iterator didn't read the entire section.");
            Assert::IsFalse(iterator > extraSectionBytes.end(), L"Iterator read more bytes than were in the section.");
            Assert::AreEqual(expectedBytes, *actualBytes);
        }

    };
}}}}
