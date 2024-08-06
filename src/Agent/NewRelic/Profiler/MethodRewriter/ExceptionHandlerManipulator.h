/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include <vector>
#include <stdint.h>
#include "../Common/Macros.h"
#include "Exceptions.h"
#include "../Logging/Logger.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    // object for building and holding exception handling clause information
    struct ExceptionHandlingClause
    {
        ByteVectorPtr _bytes;
        uint32_t _flags;
        uint32_t _tryOffset;
        uint32_t _tryLength;
        uint32_t _handlerOffset;
        uint32_t _handlerLength;
        uint32_t _classToken;
        uint32_t _filterOffset;

        // shifts the offsets
        void ShiftOffsets(uint32_t offset)
        {
            _tryOffset += offset;
            _handlerOffset += offset;
            if (_flags == 0x0001)
            {
                _filterOffset += offset;
            }
        }

        // returns the bytes that make up this exception handling clause
        ByteVectorPtr GetBytes()
        {
            PrepareBytes();
            return _bytes;
        }

        // prepares the exception clause byte vector, called by GetFatBytes automatically
        void PrepareBytes()
        {
            _bytes = std::make_shared<ByteVector>();
            _bytes->reserve(24);
            AppendLittleEndian(_flags, *_bytes);
            AppendLittleEndian(_tryOffset, *_bytes);
            AppendLittleEndian(_tryLength, *_bytes);
            AppendLittleEndian(_handlerOffset, *_bytes);
            AppendLittleEndian(_handlerLength, *_bytes);
            if (_flags == 0x0000)
                AppendLittleEndian(_classToken, *_bytes);
            else if (_flags & 0x0001)
                AppendLittleEndian(_filterOffset, *_bytes);
            else
                AppendLittleEndian(uint32_t(0), *_bytes);
        }

        static void AppendLittleEndian(uint32_t value, ByteVector& bytes)
        {
            bytes.push_back(uint8_t(value & 0xff));
            bytes.push_back(uint8_t((value >> 8) & 0xff));
            bytes.push_back(uint8_t((value >> 16) & 0xff));
            bytes.push_back(uint8_t((value >> 24) & 0xff));
        }

        static void AppendLittleEndian24(uint32_t value, ByteVector& bytes)
        {
            bytes.push_back(uint8_t(value & 0xff));
            bytes.push_back(uint8_t((value >> 8) & 0xff));
            bytes.push_back(uint8_t((value >> 16) & 0xff));
        }

        static void AppendLittleEndian(uint16_t value, ByteVector& bytes)
        {
            bytes.push_back(uint8_t(value & 0xff));
            bytes.push_back(uint8_t((value >> 8) & 0xff));
        }

        static void AppendLittleEndian(uint8_t value, ByteVector& bytes)
        {
            bytes.push_back(value);
        }

        static uint32_t FromLittleEndian32(ByteVector::const_iterator& iterator)
        {
            uint32_t value = 0;
            value |= *(iterator++) << 0;
            value |= *(iterator++) << 8;
            value |= *(iterator++) << 16;
            value |= *(iterator++) << 24;
            return value;
        }

        static uint32_t FromLittleEndian24(ByteVector::const_iterator& iterator)
        {
            uint32_t value = 0;
            value |= *(iterator++) << 0;
            value |= *(iterator++) << 8;
            value |= *(iterator++) << 16;
            return value;
        }

        static uint16_t FromLittleEndian16(ByteVector::const_iterator& iterator)
        {
            uint16_t value = 0;
            value |= *(iterator++) << 0;
            value |= *(iterator++) << 8;
            return value;
        }

        static uint8_t FromLittleEndian8(ByteVector::const_iterator& iterator)
        {
            return *(iterator++);
        }

    protected:
        ExceptionHandlingClause() :
            _flags(0),
            _tryOffset(0),
            _tryLength(0),
            _handlerOffset(0),
            _handlerLength(0),
            _classToken(0),
            _filterOffset(0)
        {}
        ExceptionHandlingClause(uint16_t flags, uint32_t tryStart, uint32_t tryEnd, uint32_t handlerStart, uint32_t handlerEnd, uint32_t classToken, uint32_t filterOffset) :
            _flags(flags),
            _tryOffset(tryStart),
            _tryLength(tryEnd - tryStart),
            _handlerOffset(handlerStart),
            _handlerLength(handlerEnd - handlerStart),
            _classToken(classToken),
            _filterOffset(filterOffset)
        {}
    };
    typedef std::shared_ptr<ExceptionHandlingClause> ExceptionHandlingClausePtr;

    struct FatExceptionHandlingClause : ExceptionHandlingClause
    {
        FatExceptionHandlingClause() {}

        FatExceptionHandlingClause(ByteVector::const_iterator& iterator)
        {
            _flags = FromLittleEndian32(iterator);
            _tryOffset = FromLittleEndian32(iterator);
            _tryLength = FromLittleEndian32(iterator);
            _handlerOffset = FromLittleEndian32(iterator);
            _handlerLength = FromLittleEndian32(iterator);
            // last 4 bytes are either class token or filter offset, depending on flags (COR_ILEXCEPTION_CLAUSE_EXCEPTION)
            if (_flags == 0x0000)
            {
                _filterOffset = 0;
                _classToken = FromLittleEndian32(iterator);
            }
            else if (_flags == 0x0001)
            {
                _classToken = 0;
                _filterOffset = FromLittleEndian32(iterator);
            }
            else
            {
                _classToken = 0;
                _filterOffset = 0;
                iterator += 4;
            }
        }

        FatExceptionHandlingClause(uint16_t flags, uint32_t tryStart, uint32_t tryEnd, uint32_t handlerStart, uint32_t handlerEnd, uint32_t classToken, uint32_t filterOffset) :
            ExceptionHandlingClause(flags, tryStart, tryEnd, handlerStart, handlerEnd, classToken, filterOffset)
        {}
    };
    typedef std::shared_ptr<FatExceptionHandlingClause> FatExceptionHandlingClausePtr;

    struct SmallExceptionHandlingClause : ExceptionHandlingClause
    {
        SmallExceptionHandlingClause(ByteVector::const_iterator& iterator)
        {
            _flags = FromLittleEndian16(iterator);
            _tryOffset = FromLittleEndian16(iterator);
            _tryLength = FromLittleEndian8(iterator);
            _handlerOffset = FromLittleEndian16(iterator);
            _handlerLength = FromLittleEndian8(iterator);
            // last 4 bytes are either class token or filter offset, depending on flags (COR_ILEXCEPTION_CLAUSE_EXCEPTION)
            if (_flags & 0x1)
            {
                _classToken = 0;
                _filterOffset = FromLittleEndian32(iterator);
            }
            else
            {
                _filterOffset = 0;
                _classToken = FromLittleEndian32(iterator);
            }
        }
    };

    // See: ECMA-335 II.25.4.5
    class ExceptionHandlerManipulator
    {
    private:
        std::vector<ExceptionHandlingClausePtr> _exceptionClauses;
        uint32_t _originalExceptionClauseCount;

    public:
        // manipulator for an existing extra section
        ExceptionHandlerManipulator(ByteVector::const_iterator& extraSectionBytes)
        {
            // flags are packed into the first byte, split them up into bools (ECMA-355 II.25.4.5)
            uint8_t methodDataSectionFlags = *(extraSectionBytes++);
            bool isExceptionHandlingTable = (methodDataSectionFlags & 0x1) ? true : false;
            bool sectionIsFat = (methodDataSectionFlags & 0x40) ? true : false;
            bool moreSectionsFollow = (methodDataSectionFlags & 0x80) ? true : false;

            // validate that the flags are things we handle
            ValidateFlags(isExceptionHandlingTable, moreSectionsFollow);

            // get the size of the extra section
            uint32_t extraSectionSize = ExtractExtraSectionSize(extraSectionBytes, sectionIsFat);

            // calculate the number of exception clauses in this extra section
            _originalExceptionClauseCount = CalculateNumberOfExceptionClauses(extraSectionSize, sectionIsFat);

            // build ExceptionHandlingClause structures for each clause in this section
            for (uint32_t i = 0; i < _originalExceptionClauseCount; ++i)
            {
                auto exceptionClause = ExtractExceptionClause(extraSectionBytes, sectionIsFat);
                _exceptionClauses.push_back(exceptionClause);
            }
        }

        // manipulator for a method without any extra sections
        ExceptionHandlerManipulator() : _originalExceptionClauseCount(0) {}

        void AddExceptionHandlingClause(ExceptionHandlingClausePtr clause)
        {
            // push the clause onto the clause vector
            _exceptionClauses.push_back(clause);
        }

        ByteVectorPtr GetExtraSectionBytes(uint32_t userCodeOffset)
        {
            ByteVectorPtr bytes(new ByteVector);
            // set the flags (ECMA-335 II.25.4.5)
            bytes->push_back(0x1 | 0x40);

            // figure out how much space our exception blocks will take
            uint32_t extraSectionSize = ((uint32_t)_exceptionClauses.size()) * 24 + 4;
            if (extraSectionSize > 0xffffff)
            {
                LogError("Exception clauses grew too large with instrumentation.");
                throw ExceptionHandlerManipulatorException(_X("Exception clauses grew too large with instrumentation."));
            }

            // set the size
            ExceptionHandlingClause::AppendLittleEndian24(extraSectionSize, *bytes);

            // shift the original clauses up to the correct
            for (uint32_t i = 0; i < _originalExceptionClauseCount; ++i)
            {
                auto clause = _exceptionClauses[i];
                clause->ShiftOffsets(userCodeOffset);
            }

            // append the clauses
            for (auto clause : _exceptionClauses)
            {
                auto clauseBytes = clause->GetBytes();
                bytes->insert(bytes->end(), clauseBytes->begin(), clauseBytes->end());
            }

            return bytes;
        }

        uint32_t GetOriginalExceptionClauseCount()
        {
            return _originalExceptionClauseCount;
        }

    private:
        static void ValidateFlags(bool isExceptionHandlingTable, bool moreSectionsFollow)
        {
            // as of 2013-04-10 there is only one extra section type (exception handling table) so throw an exception in all other cases
            if (!isExceptionHandlingTable)
            {
                LogError("Attempted to instrument a method with something other than an exception handling clause as a method extra section.");
                throw ExceptionHandlerManipulatorException(_X("Attempted to instrument a method with something other than an exception handling clause as a method extra section."));
            }

            if (moreSectionsFollow)
            {
                LogError("Attempted to instrument a method with multiple extra sections.");
                throw ExceptionHandlerManipulatorException(_X("Attempted to instrument a method with something other than an exception handling clause as a method extra section."));
            }
        }

        static uint32_t ExtractExtraSectionSize(ByteVector::const_iterator& iterator, bool isFat)
        {
            if (isFat)
            {
                return ExceptionHandlingClause::FromLittleEndian24(iterator);
            }
            else
            {
                uint8_t extraSectionSize = *(iterator++);
                iterator += 2;
                return extraSectionSize;
            }
        }

        static uint32_t CalculateNumberOfExceptionClauses(uint32_t size, bool isFat)
        {
            if (isFat)
            {
                return (size - 4) / 24;
            }
            else
            {
                return (size - 4) / 12;
            }
        }

        static ExceptionHandlingClausePtr ExtractExceptionClause(ByteVector::const_iterator& iterator, bool isFat)
        {
            if (isFat)
            {
                return std::make_shared<FatExceptionHandlingClause>(iterator);
            }
            else
            {
                return std::make_shared<SmallExceptionHandlingClause>(iterator);
            }
        }
    };
    typedef std::shared_ptr<ExceptionHandlerManipulator> ExceptionHandlerManipulatorPtr;
}}}
