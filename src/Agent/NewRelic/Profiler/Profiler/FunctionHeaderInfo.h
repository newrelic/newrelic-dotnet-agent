// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include "../Common/Macros.h"
#include "OpCodes.h"
#include "../MethodRewriter/IFunctionHeaderInfo.h"

namespace NewRelic {
    namespace Profiler {

        // Read and return a signed integer of length size from bytes.
        // This code assumes that sizeof(int) == 4 (eg, 32  bits) and that size is 1, 2 of 4.
        static int ReadNumber(const uint8_t* bytes, size_t size) {
            int num = 0;  // Assemble this piecemeal as a signed number
            for (size_t byte_number = 0; byte_number < size; byte_number++) {
                num |= ((bytes[byte_number] & 0xFF) << (byte_number * 8));
            }
            // Sign extend as necessary
            if (size == 1) {
                if (num & (1 << 7)) {
                    num |= 0xffffff00;
                }
            }
            if (size == 2) {
                if (num & (1 << 15)) {
                    num |= 0xffff0000;
                }
            }
            return num;
        }

        class FunctionHeaderInfo : public NewRelic::Profiler::MethodRewriter::IFunctionHeaderInfo
        {
        public:

            virtual uint16_t GetReturnCount() override 
            {
                return (uint16_t)GetInstructionTypeCounts().returnCount;
            }

            // Returns true if this header contains a exception handling block.
            virtual bool HasSEH() override
            {
                return false;
            }


            // Return the number of return instructions.
            // We assume that the last instruction is always a return instruction.
            virtual NewRelic::Profiler::MethodRewriter::InstructionTypeCounts GetInstructionTypeCounts() override
            {
                NewRelic::Profiler::MethodRewriter::InstructionTypeCounts counts;

                uint8_t* bodyBytes = GetCode();
                unsigned size = GetMethodBodySize();

                for (unsigned pos = 0; pos < size; /*VOID*/) {
                    OpCodePtr info = GetOpCode(bodyBytes, pos);
                    if (info == NULL) {
                        return counts; // error - we should probably throw instead
                    }

                    if (info->instruction == CEE_RET) {
                        counts.returnCount += 1;
                        pos += info->totalSize;
                    }
                    else if (info->instruction == CEE_SWITCH) {
                        counts.switchCount += 1;
                        const unsigned numberArms = ReadNumber(&bodyBytes[pos + 1], 4) & 0xFFFFFFFF;
                        const unsigned numberBytesTable = (1 + numberArms) * sizeof(DWORD);
                        const unsigned totalBytesInstruction = 1 + numberBytesTable;
                        pos += totalBytesInstruction;
                    }
                    else if (info->controlFlow == BRANCH) {
                        if (info->operandSize == 1) {
                            counts.shortBranchCount += 1;
                        }
                        else if (info->operandSize == 4) {
                            counts.longBranchCount += 1;
                        }
                        pos += info->totalSize;
                    }
                    else {
                        pos += info->totalSize;
                    }
                }

                return counts;
            }

        protected:
            uint8_t* _functionBytes;
            FunctionHeaderInfo(uint8_t* functionBytes) {
                _functionBytes = functionBytes;
            }
        };

        class FatFunctionHeaderInfo :
            public FunctionHeaderInfo
        {
        public:
            FatFunctionHeaderInfo(uint8_t* functionBytes) : FunctionHeaderInfo(functionBytes) {
            }

            ~FatFunctionHeaderInfo(void) {
            }

            unsigned GetHeaderSize() {
                return sizeof(COR_ILMETHOD_FAT);
            }

            bool IsTinyHeader() {
                return false;
            }

            COR_ILMETHOD_FAT *GetFatInfo()
            {
                return (COR_ILMETHOD_FAT*)_functionBytes;
            }

            unsigned GetMaxStack() {
                return GetFatInfo()->GetMaxStack();
            }

            unsigned GetMethodBodySize()
            {
                return GetFatInfo()->GetCodeSize();
            }

            unsigned GetFunctionBodyAlignment(unsigned methodBodyEnd) {
                unsigned methodBodyAlignment = methodBodyEnd % sizeof(DWORD);
                return (methodBodyAlignment == 0) ? 0 : (sizeof(DWORD) - methodBodyAlignment);
            }

            unsigned GetTotalSize() {
                unsigned size = GetHeaderSize() + GetMethodBodySize();
                if (GetFatInfo()->More()) {
                    //size = (BYTE*)GetFatInfo()->GetSect();
                    size += GetFatInfo()->GetSect()->DataSize() + GetFunctionBodyAlignment(size);
                }
                return size;
            }

            bool HasSEH()
            {
                return (GetFatInfo()->GetFlags() & CorILMethod_MoreSects) ? true : false;
            }

            uint8_t* GetCode() {
                return GetFatInfo()->GetCode();
            }


        };

        class TinyFunctionHeaderInfo :
            public FunctionHeaderInfo
        {
        public:
            TinyFunctionHeaderInfo(uint8_t* functionBytes) : FunctionHeaderInfo(functionBytes) {
            }

            bool IsTinyHeader() {
                return true;
            }

            unsigned GetHeaderSize() {
                return sizeof(COR_ILMETHOD_TINY);
            }

            COR_ILMETHOD_TINY *GetTinyInfo()
            {
                return (COR_ILMETHOD_TINY*)_functionBytes;
            }

            unsigned GetMaxStack() {
                return GetTinyInfo()->GetMaxStack();
            }

            uint8_t* GetCode() {
                return GetTinyInfo()->GetCode();
            }

            unsigned GetTotalSize() {
                return GetHeaderSize() + GetMethodBodySize();
            }

            unsigned GetMethodBodySize()
            {
                return GetTinyInfo()->GetCodeSize();
            }
        };

        // returns the appropriate FunctionHeaderInfo type based on whether the function header is tiny or fat.
        static NewRelic::Profiler::MethodRewriter::FunctionHeaderInfoPtr CreateFunctionHeaderInfo(ByteVectorPtr functionBytesPtr) {
            uint8_t* functionBytes = functionBytesPtr->data();
            if (((COR_ILMETHOD_TINY*)functionBytes)->IsTiny()) 
            {
                return std::make_shared<TinyFunctionHeaderInfo>(functionBytes);
            }
            else if (((COR_ILMETHOD_FAT*)functionBytes)->IsFat()) 
            {
                return std::make_shared<FatFunctionHeaderInfo>(functionBytes);
            }
            else
            {
                return nullptr;
            }
        }
    }
}