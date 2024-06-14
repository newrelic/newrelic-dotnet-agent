/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../Common/Macros.h"

namespace NewRelic {
    namespace Profiler {

        // opcode.def requires these definitions
        #define BRANCH                1
        // we treat conditional and unconditional branches the same.
        #define COND_BRANCH            BRANCH

        // http://msdn.microsoft.com/en-us/library/system.reflection.emit.operandtype.aspx
        // Instruction operands / sizes
        #define InlineNone              0
        #define InlineVar               2
        #define ShortInlineVar          1
        #define InlineI                 4
        #define ShortInlineI            1
        #define InlineI8                8
        #define InlineR                 8
        #define ShortInlineR            4
        #define InlineBrTarget          4
        #define ShortInlineBrTarget     1
        #define InlineMethod            4
        #define InlineField             4
        #define InlineType              4
        #define InlineString            4
        #define InlineSig               4
        #define InlineTok               4
        #define InlineSwitch            4
        // end opcode.def requires

        // {
        #undef OPDEF
        #define OPDEF(id, name, pop, push, operand, type, len, OpCode1, OpCode2, cf) operand,

        // https://github.com/dotnet/coreclr/blob/master/src/inc/opcode.def
        // array of IL opcode operands
        const unsigned g_ILOperands[] =
        {
            #include "opcode.def"
        };
        #undef OPDEF
        // }

        // {
        #undef OPDEF
        #define OPDEF(id, name, pop, push, operand, type, len, OpCode1, OpCode2, cf) cf,

        #define NEXT                0
        #define BREAK                0
        #define CALL                0
        #define RETURN                0
        #define THROW                0
        #define META                0

        // array of IL opcode control flows
        const unsigned g_ILControlFlow[] =
        {
            #include "opcode.def"
        };

        #undef NEXT
        #undef BREAK
        #undef CALL
        #undef RETURN
        #undef THROW
        #undef META    
        #undef OPDEF
        // }

        // {
        #undef OPDEF
        #define OPDEF(id, name, pop, push, operand, type, len, OpCode1, OpCode2, cf) _X(name),
        // array of IL opcode operands
        const xchar_t* g_ILOpCodeNames[] =
        {
            #include "opcode.def"
        };
        #undef OPDEF
        // }

        //  List of opcodes
        // http://www.asukaze.net/etc/cil/opcode.html
        class OpCode {
        public:
            uint8_t instruction;
            unsigned instructionSize;
            unsigned arrayOffset;
            unsigned operandSize;
            unsigned totalSize;
            unsigned controlFlow;
            xstring_t name;

            OpCode() :
                instruction(0),
                instructionSize(0),
                arrayOffset(0),
                operandSize(0),
                totalSize(0),
                controlFlow(0)
                {}
                

            void Reset(std::shared_ptr<OpCode> newOpCode)
            {
                instruction = newOpCode->instruction;
                instructionSize = newOpCode->instructionSize;
                arrayOffset = newOpCode->arrayOffset;
                operandSize = newOpCode->operandSize;
                totalSize = newOpCode->totalSize;
                controlFlow = newOpCode->controlFlow;
                name = newOpCode->name;
            }
        };

        typedef std::shared_ptr<OpCode> OpCodePtr;

        // Return information about the 1 or 2 byte opcode at code[offset].
        // May return NULL if the opcode there is unknown.
        OpCodePtr GetOpCode(const BYTE* code, unsigned offset) {
            BYTE instruction = code[offset];
            BYTE originalInstruction = instruction;
            unsigned arrayOffset = 0;
            unsigned totalInstructionSize = 1;
            if (instruction > RESERVED_PREFIX_START) {
                arrayOffset = (unsigned)REFPRE - (unsigned)instruction;
                instruction = code[offset + 1];
                totalInstructionSize++;
            }

            (void)originalInstruction;  // shut up warning message when DEBUG not defined

            unsigned arrIndex = instruction + (arrayOffset * 256);
            if (arrIndex >= CEE_ILLEGAL) {
                return nullptr;
            }
            auto info = std::make_shared<OpCode>();
            info->instruction = instruction;
            info->arrayOffset = arrayOffset;
            info->operandSize = g_ILOperands[arrIndex];
            info->totalSize = totalInstructionSize + info->operandSize;
            info->controlFlow = g_ILControlFlow[arrIndex];
            info->name = g_ILOpCodeNames[arrIndex];
            info->instructionSize = arrayOffset + 1;

            return info;
        }

        // Returns an opcode structure pointer for the given instruction.
        OpCodePtr GetOpCode(ILCODE opcode) {
            return GetOpCode((const BYTE*)&opcode, 0);
        }
    }
}
