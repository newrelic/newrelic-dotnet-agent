// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once

#define __STDC_WANT_LIB_EXT1__ 1
#include <string.h>
#include <list>
#include <map>

#include "../Logging/Logger.h"
#include "FunctionHeaderInfo.h"
#include "OpCodes.h"

#include <memory.h>

using namespace std;

namespace NewRelic {
    namespace Profiler {
        namespace MethodRewriter {

#define BRANCH_OPERAND_SIZE        (g_ILOperands[CEE_BR])  // Unconditional long form branch.

            // Write the integer numberToWrite for size bytes into dest.
            // Write in little-endian order,
            // namely the LSB of the numberToWrite gets written in smaller indices in dest.
            // We assume that the numberToWrite fits into size bytes.
            static void WriteNumber(BYTE* dest, int numberToWrite, size_t size) {
                for (size_t byte_number = 0; byte_number < size; byte_number++) {
                    dest[byte_number] = (((unsigned)numberToWrite >> (byte_number * 8)) & 0xFF);
                }
            }


            // There's no apparent rhyme or reason to the mapping in CIL single-byte opcodes
            // from long branch to short branch form.  Just gut out a linear search.
            // Data in the tables was derived from http://en.wikipedia.org/wiki/List_of_CIL_Instructions
            // as of 2011-10
            struct CILMap {
                BYTE from;
                BYTE to;
            };

            // Map single byte CIL opcode for a short form branch to a long form branch.
            // Return 0x00 if no match is found.
            static OpCodePtr GetLongFormBranch(OpCodePtr from) {
#undef PAIR
#define PAIR(to, from) {from, to}
                static const CILMap ShortToLongBranchMap[] = {
                    PAIR(0x3B /* beq */,        0x2E /* beq.s */),
                    PAIR(0x3C /* bge */,        0x2F /* bge.s */),
                    PAIR(0x41 /* bge.un */,    0x34 /* bge.un.s */),
                    PAIR(0x3D /* bgt */,        0x30 /* bgt.s */),
                    PAIR(0x42 /* bgt.un */,    0x35 /* bgt.un.s */),
                    PAIR(0x3E /* ble */,        0x31 /* ble.s */),
                    PAIR(0x43 /* ble.un */,    0x36 /* ble.un.s */),
                    PAIR(0x3F /* blt */,        0x32 /* blt.s */),
                    PAIR(0x44 /* blt.un */,    0x37 /* blt.un.s */),
                    PAIR(0x40 /* bne.un */,    0x33 /* bne.un.s */),
                    PAIR(0x38 /* br */,        0x2B /* br.s */),
                    PAIR(0x39 /* brfalse */,    0x2C /* brfalse.s */),
                    PAIR(0x3A /* brinst */,    0x2D /* brinst.s */),
                    PAIR(0x39 /* brnull */,    0x2C /* brnull.s */),
                    PAIR(0x3A /* brtrue */,    0x2D /* brtrue.s */),
                    PAIR(0x39 /* brzero */,    0x2C /* brzero.s */),
                    PAIR(0xDD /* leave */,    0xDE /* brzero.s */),
                };
#undef PAIR
                for (unsigned i = 0; i < sizeof(ShortToLongBranchMap) / sizeof(ShortToLongBranchMap[0]); i++) {
                    if (ShortToLongBranchMap[i].from == from->instruction) {
                        return GetOpCode((ILCODE)ShortToLongBranchMap[i].to);
                    }
                }
                return from;
            }

            // Writes length number of bytes into the given vector.
            static void WritePadding(ByteVectorPtr vector, unsigned length)
            {
                for (unsigned i = 0; i < length; i++)
                {
                    vector->push_back(0xff);
                }
            }

            class Instruction
            {
            public:
                Instruction(OpCodePtr opCode, unsigned offset) :
                    _opcode(opCode),
                    _offset(offset),
                    _valid(true)
                {
                }

                virtual xstring_t ToString()
                {
                    return xstring_t((_valid ? _X("[") : _X("*["))) + to_hex_string(_offset) + _X("] : ") + _opcode->name;
                }

                virtual void ResolveTargets(ByteVectorPtr methodBody, std::shared_ptr<std::map<unsigned, std::shared_ptr<Instruction>>> instructions)
                {
                }

                virtual void WriteBranches(ByteVectorPtr instructions, std::shared_ptr<std::map<unsigned, std::shared_ptr<Instruction>>> instructionMap)
                {
                }

                virtual void OnInstructionChange(std::shared_ptr<Instruction> oldInstruction, std::shared_ptr<Instruction> newInstruction)
                {
                }

                // Write this instruction into a vector of bytes.
                void Write(const BYTE* originalBody, ByteVectorPtr instructionSet) 
                {
                    // set the new offset
                    const unsigned newOffset = (unsigned)instructionSet->size();
                    
                    // the first byte of a 2 byte instruction is 0xFE
                    if (_opcode->instructionSize == 2) {
                        instructionSet->push_back(0xFE);
                    }

                    instructionSet->push_back(_opcode->instruction);
                    WriteOperand(originalBody, instructionSet);

                    _offset = newOffset;
                }

                // Write the original bytes following the instruction
                virtual void WriteOperand(const BYTE* originalBody, ByteVectorPtr instructionSet)
                { 
                    auto bytesCount = _opcode->totalSize - _opcode->instructionSize;
                    for (size_t i = 0; i < bytesCount; ++i)
                    {
                        auto index = _offset + _opcode->instructionSize + i;
                        auto byte = *(originalBody + index);
                        instructionSet->push_back(byte);
                    }
                }

                OpCodePtr GetOpCode()
                {
                    return _opcode;
                }
                unsigned GetOffset()
                {
                    return _offset;
                }
                bool IsValid()
                {
                    return _valid;
                }
            protected:
                OpCodePtr _opcode;
                unsigned _offset;
                bool _valid;
            };

            typedef std::shared_ptr<Instruction> InstructionPtr;
            typedef std::map<unsigned, InstructionPtr> OffsetToInstructionMap;
            typedef std::shared_ptr<OffsetToInstructionMap> OffsetToInstructionMapPtr;

            class SwitchInstruction :public Instruction
            {
            public:
                SwitchInstruction(OpCodePtr opCode, unsigned offset, unsigned numberOfArms) : Instruction(opCode, offset)
                {
                    _numberOfArms = numberOfArms;
                    _targets = std::make_shared<std::list<InstructionPtr>>();
                }

                virtual void WriteBranches(ByteVectorPtr instructions, OffsetToInstructionMapPtr instructionMap) override
                {
                    if (_valid)
                    {
                        auto offsetOfInstructionFollowingSwitch = _offset + _opcode->totalSize;
                        auto offsetOfArm = _offset + _opcode->instructionSize + sizeof(DWORD);
                        for (auto target : *_targets) {
                            auto jumpLength = target->GetOffset() - offsetOfInstructionFollowingSwitch;
                            
                            auto armLocation = instructions->data() + offsetOfArm;
                            WriteNumber(armLocation, jumpLength, sizeof(DWORD));
                            
                            // increment arm offset to next arm
                            offsetOfArm += sizeof(DWORD);
                        }
                    }
                }

                virtual void OnInstructionChange(InstructionPtr oldInstruction, InstructionPtr newInstruction) override
                {
                    // a better person would do this in place but the iter pointer stuff confuses me
                    auto newTargetsList = std::make_shared<std::list<InstructionPtr>>();
                    for (auto targetInstruction : *_targets) {
                        if (oldInstruction == targetInstruction)
                        {
                            newTargetsList->push_back(newInstruction);
                        }
                        else {
                            newTargetsList->push_back(targetInstruction);
                        }
                    }
                    _targets = newTargetsList;
                }

                virtual void ResolveTargets(ByteVectorPtr methodBody, OffsetToInstructionMapPtr instructions) override
                {
                    auto startOfArms = _offset + _opcode->instructionSize + sizeof(DWORD);
                    for (unsigned i = 0; i < _numberOfArms; i++) {
                        auto jumpOffset = startOfArms + (i * sizeof(DWORD));
                        auto jumpLength = ReadNumber(methodBody->data() + jumpOffset, sizeof(DWORD));

                        auto bodyOffset = _offset + _opcode->totalSize + jumpLength;

                        auto found = instructions->find(bodyOffset);
                        if (found == instructions->end())
                        {
                            _valid = false;
                        }
                        else {
                            _targets->push_back(found->second);
                        }
                    }
                }
            private:
                unsigned _numberOfArms;
                std::shared_ptr<std::list<InstructionPtr>> _targets;
            };

            class BranchInstruction :public Instruction
            {
            public:
                BranchInstruction(OpCodePtr opCode, unsigned offset) : Instruction(opCode, offset)
                {
                }

                BranchInstruction(OpCodePtr opCode, unsigned offset, InstructionPtr target) : Instruction(opCode, offset)
                {
                    _targetInstruction = target;
                }

                virtual xstring_t ToString() override
                {
                    return Instruction::ToString() + _X(" branch to ") + to_hex_string(_targetOffset);
                }

                virtual void OnInstructionChange(InstructionPtr oldInstruction, InstructionPtr newInstruction) override
                {
                    if (oldInstruction == _targetInstruction)
                    {
                        _targetInstruction = newInstruction;
                    }
                }

                virtual void WriteBranches(ByteVectorPtr instructions, OffsetToInstructionMapPtr instructionMap) override
                {
                    if (_valid)
                    {
                        signed int jump = (signed int)_targetInstruction->GetOffset() - (_offset + _opcode->totalSize);
                        if (_opcode->operandSize == 1 && (jump < -127 || jump > 127))
                        {
                            _valid = false;
                            return;
                        }

                        auto operandLocation = instructions->data() + _offset + _opcode->instructionSize;
                        auto sanity = *(operandLocation);
                        if (sanity != 0xff)
                        {
                            LogTrace(L"Branch sanity check failed at offset ", _offset);
                            _valid = false;
                        }
                        else {
                            WriteNumber(operandLocation, jump, _opcode->operandSize);
                        }
                    }
                }

                virtual void WriteOperand(const BYTE* originalBody, ByteVectorPtr instructionSet) override
                {
                    (void)originalBody;  // shut up warning
                    // write a temp operand that we'll overwrite later
                    WritePadding(instructionSet, _opcode->operandSize);
                }

                virtual void ResolveTargets(ByteVectorPtr methodBody, OffsetToInstructionMapPtr instructions) override
                {
                    auto branchLength = ReadNumber(methodBody->data() + _offset + _opcode->instructionSize, _opcode->operandSize);
                    _targetOffset = _offset + _opcode->totalSize + branchLength;
                    if (_targetOffset < 0)
                    {
                        _valid = false;
                    }
                    else {
                        auto found = instructions->find(_targetOffset);
                        if (found == instructions->end())
                        {
                            _valid = false;
                        }
                        else {
                            _targetInstruction = found->second;
                        }
                    }
                }
            private:
                InstructionPtr _targetInstruction;
                int _targetOffset;
            };

            // This processes methods so that their method bodies are ready to be wrapped.
            // For methods with a single RET, that final return is changed to a NOP.
            // For methods with multiple RETs, the final return is changed to a NOP,
            // and all middle returns are changed to branches to the final NOP.
            class FunctionPreprocessor
            {
            public:
                FunctionPreprocessor(std::shared_ptr<NewRelic::Profiler::MethodRewriter::IFunctionHeaderInfo> headerInfo, ByteVectorPtr methodBytes) :
                    _headerInfo(headerInfo)
                {
                    _methodBytes = methodBytes;
                }

                ByteVectorPtr Process() {
                    auto returnCount = _headerInfo->GetReturnCount();
                    if (returnCount == 0)
                    {
                        // no returns, just wrap it
                        return _methodBytes;
                    }
                    if (_headerInfo->GetCode() == nullptr)
                    {
                        return nullptr;
                    }

                    uint8_t* codeEnd = _headerInfo->GetCode() + _headerInfo->GetMethodBodySize();
                    if (CEE_RET != *(codeEnd - 1)) 
                    {
                        LogError(L"Last instruction was not RET, aborting");
                        return nullptr;
                    }
                    if (returnCount == 1) {
                        // overwrite the CEE_RET instruction with a CEE_NOP - we want to store the return value and 
                        // finish the tracer before returning
                        *(codeEnd - 1) = CEE_NOP;
                        return _methodBytes;
                    }

                    return RewriteMultiReturnMethod();
                }
            private:
                ByteVectorPtr _methodBytes;

                // The header information, for the old function.
                std::shared_ptr<NewRelic::Profiler::MethodRewriter::IFunctionHeaderInfo> _headerInfo;

                // Rewrite the method so that all middle RETs jump to the final RET
                // which is changed to a NOP.  The header will be converted to a FAT
                // header if it is TINY.
                ByteVectorPtr RewriteMultiReturnMethod() {
                    LogDebug(L"Rewriting multi-return method");
                    const BYTE* oldCodeBytes = _headerInfo->GetCode();
                    const unsigned oldCodeSize = _headerInfo->GetMethodBodySize();

                    // the index of the last instruction
                    auto finalInstructionIndex = oldCodeSize - 1;
                    // Check assertion of the location of the final return instruction.
                    if (oldCodeBytes[finalInstructionIndex] != CEE_RET) {
                        LogTrace(L"Last instruction of method is not a RET");
                        return nullptr;
                    }

                    // we're at the very least adding an extra byte to every RET to turn it into a br.s
                    auto smallestBodySize = oldCodeSize + _headerInfo->GetReturnCount();
                    // if it looks like there could be branches that require the long form, expand all branches 
                    // to the long form
                    auto expandBranches = smallestBodySize >= 127;

                    auto instructions = ReadInstructions(NewVector(oldCodeBytes, oldCodeSize));
                    if (instructions == nullptr)
                    {
                        return nullptr;
                    }

                    // sanity check the final instruction.  If it isn't a RET, we likely mucked up the instruction parsing
                    auto lastInstruction = instructions->at(finalInstructionIndex);
                    if (lastInstruction->GetOpCode()->instruction != CEE_RET) {
                        LogTrace(L"Expected RET as final instruction but found ", lastInstruction->GetOpCode()->instruction);
                        return nullptr;
                    }

                    // change the return instruction into a NOP.  we change it in place
                    // because other instructions point to it and we're only changing the opcode,
                    // we're not changing its behavior as we do when we turn RETs into BRs.
                    lastInstruction->GetOpCode()->Reset(GetOpCode(CEE_NOP));

                    auto branches = std::make_shared<std::list<InstructionPtr>>();
                    for (auto instruction : *instructions.get()) 
                    {
                        if (instruction.second->GetOpCode()->instruction == CEE_RET)
                        {
                            // re-write RET instructions as branch instructions to the last instruction
                            auto branchInstruction = expandBranches ? CEE_BR : CEE_BR_S;
                            OpCodePtr branch = GetOpCode(branchInstruction);
                            auto newInst = std::make_shared<BranchInstruction>(branch, instruction.first, lastInstruction);

                            (*instructions.get())[instruction.second->GetOffset()] = newInst;
                            // branch instructions may point to the old RET instruction, so re-point them 
                            // to the new BR instruction
                            NotifyOfInstructionChange(instructions, instruction.second, newInst);
                            branches->push_back(newInst);
                        }
                        else if (instruction.second->GetOpCode()->controlFlow == BRANCH)
                        {
                            // if this is a short branch instruction and we're expanding branches, do it now
                            if (expandBranches && instruction.second->GetOpCode()->operandSize == 1)
                            {
                                auto longBranch = GetLongFormBranch(instruction.second->GetOpCode());
                                LogTrace(L"Expand instruction ", instruction.second->GetOpCode()->name, " to ", longBranch->name);
                                // convert branches to long form
                                instruction.second->GetOpCode()->Reset(longBranch);
                            }
                            branches->push_back(instruction.second);
                        }
                    }

                    // get an estimate of the new function size and reserve the space for that
                    const unsigned newFunctionSize = GetNewFunctionSize();
                    ByteVectorPtr newByteCode = std::make_shared<ByteVector>();
                    newByteCode->reserve(newFunctionSize);

                    // write the instructions into our bytecode vector.  that'll reset the offsets 
                    // of the instructions so that we can recompute the branch jumps.
                    for (auto instruction : *instructions.get()) 
                    {
                        instruction.second->Write(oldCodeBytes, newByteCode);
                    }

                    // now all instructions contain their final offset, so we can
                    // write the branch instruction offsets
                    for (auto branch : *branches) {
                        branch->WriteBranches(newByteCode, instructions);
                    }

                    // check that everything is still valid
                    if (!AllValid(instructions)) return nullptr;

                    // record our new body size
                    const DWORD newBodySize = DWORD(newByteCode->size());
                    WriteHeader(newByteCode, newBodySize);
                    if (!WriteSEH(newByteCode, instructions))
                    {
                        return nullptr;
                    }

                    newByteCode->shrink_to_fit();

                    try {
                        if (!PassesCheck(newByteCode, instructions))
                        {
                            return nullptr;
                        }
                    }
                    catch (...)
                    {
                        LogError(L"Failed to valid rewrite of method with multiple returns");
                        return nullptr;
                    }
                    instructions->clear();

                    return newByteCode;
                }

                static ByteVectorPtr NewVector(const BYTE* bytes, unsigned int size)
                {
                    auto vector = std::make_shared<ByteVector>();
                    vector->assign(bytes, bytes + size);
                    return vector;
                }

                static void NotifyOfInstructionChange(OffsetToInstructionMapPtr instructions, InstructionPtr oldInstruction, InstructionPtr newInstruction)
                {
                    for (auto iter : *instructions)
                    {
                        iter.second->OnInstructionChange(oldInstruction, newInstruction);
                    }
                }

                static bool AllValid(OffsetToInstructionMapPtr instructions)
                {
                    for (auto instruction : *instructions.get())
                    {
                        if (!instruction.second->IsValid()) {
                            return false;
                        }
                    }
                    return true;
                }

                static bool PassesCheck(ByteVectorPtr functionBytes, OffsetToInstructionMapPtr firstPassInstructions)
                {
                    const unsigned headerSize = sizeof(COR_ILMETHOD_FAT);
                    COR_ILMETHOD_FAT* header = (COR_ILMETHOD_FAT*)functionBytes->data();
                    if (functionBytes->size() < headerSize) {
                        LogError(L"Failed header check after method rewrite");
                        return false;
                    }
                    if (functionBytes->size() < headerSize + header->GetCodeSize()) {
                        LogError(L"Failed total size check after method rewrite.  Total Size: ", functionBytes->size(), ", CodeSize: ", header->GetCodeSize());
                        return false;
                    }
                    auto instructions = ReadInstructions(NewVector(header->GetCode(), header->GetCodeSize()));
                    if (instructions == nullptr)
                    {
                        LogError(L"Failed to parse instructions after method rewrite");
                        return false;
                    }
                    if (!AllValid(instructions)) {
                        LogError(L"Failed to validate instructions after method rewrite");
                        PrintInstructions(instructions);
                        return false;
                    }
                    return true;
                }

                static void PrintInstructions(OffsetToInstructionMapPtr instructions)
                {
#ifdef DEBUG
                    for (auto iter : *instructions.get())
                    {
                        LogInfo(iter.second->ToString());
                    }
#endif
                }

                bool WriteSEH(ByteVectorPtr newByteCode, OffsetToInstructionMapPtr instructions) {
                    if (_headerInfo->HasSEH()) {
                        COR_ILMETHOD_DECODER method((const COR_ILMETHOD*)_methodBytes.get()->data());
                        COR_ILMETHOD_SECT_EH* currentEHSection = (COR_ILMETHOD_SECT_EH*)method.EH;

                        if (currentEHSection != NULL) {
                            auto alignmentPadding = GetFunctionBodyAlignment((unsigned)newByteCode->size());
                            auto startOfExtra = newByteCode->size() + alignmentPadding;

                            unsigned sehClauseCount = currentEHSection->EHCount();
                            auto sehSectionSize = COR_ILMETHOD_SECT_EH::Size(sehClauseCount);
                            auto sehClausesBytes = std::unique_ptr<BYTE, void(*)(void*)>{
                                reinterpret_cast<BYTE*>(malloc(sehSectionSize)),
                                free
                            };
                            COR_ILMETHOD_SECT_EH_CLAUSE_FAT* sehClauses = (COR_ILMETHOD_SECT_EH_CLAUSE_FAT*)sehClausesBytes.get();
                            CopyOldEHSections(currentEHSection, sehClauses);

                            // write padding for DWORD alignment and extra section
                            WritePadding(newByteCode, alignmentPadding + sehSectionSize);

                            UpdateSEHSections(sehClauseCount, sehClauses, instructions);

                            // Copy the SEH clauses.
                            auto actualExtraSize = COR_ILMETHOD_SECT_EH::Emit(sehSectionSize,
                                sehClauseCount, sehClauses,
                                false, newByteCode->data() + startOfExtra);

                            if (actualExtraSize != sehSectionSize)
                            {
                                LogTrace(L"Extra section actual size ", actualExtraSize);
                            }
                        }
                    }
                    return true;
                }

                void WriteHeader(ByteVectorPtr newByteCode, DWORD newBodySize)
                {
                    // insert 12 bytes into the front of our byte code for the header
                    for (unsigned i = 0; i < sizeof(COR_ILMETHOD_FAT); i++)
                    {
                        newByteCode->insert(newByteCode->begin(), 0x00);
                    }

                    // Copy the old header, 1 byte for small headers and 12 for fat headers.
#ifdef __STDC_LIB_EXT1__
                    memcpy_s(newByteCode->data(), newByteCode->size(), _methodBytes.get()->data(), _headerInfo->GetHeaderSize());
#else
                    memcpy(newByteCode->data(), _methodBytes.get()->data(), _headerInfo->GetHeaderSize());
#endif
                    COR_ILMETHOD_FAT* newHeaderStruct = (COR_ILMETHOD_FAT*)newByteCode->data();
                    if (_headerInfo->IsTinyHeader())
                    {
                        newHeaderStruct->SetSize(3); // 3 Dwords for the header
                        newHeaderStruct->SetMaxStack(8);
                        newHeaderStruct->SetFlags(CorILMethod_FatFormat);
                    }

                    // write the method size
                    newHeaderStruct->SetCodeSize(newBodySize);
                }

                // Update the SEH sections based on the new instruction offsets.
                static void UpdateSEHSections(unsigned sehClauseCount, COR_ILMETHOD_SECT_EH_CLAUSE_FAT* sehClauses, OffsetToInstructionMapPtr instructions) 
                {
                    for (unsigned c = 0; c < sehClauseCount; c++) {
                        COR_ILMETHOD_SECT_EH_CLAUSE_FAT* clause = &sehClauses[c];
                        
                        auto tryInstruction = instructions->at(clause->TryOffset);
                        //unsigned newTryLength = _oldPositionToNew[clause->TryOffset + clause->TryLength] - _oldPositionToNew[clause->TryOffset];
                        auto tryEndInstruction = instructions->at(clause->TryOffset + clause->TryLength);

                        auto tryLength = tryEndInstruction->GetOffset() - tryInstruction->GetOffset();
                        clause->SetTryLength(tryLength);
                        clause->SetTryOffset(tryInstruction->GetOffset());
                        
                        auto handlerInstruction = instructions->at(clause->HandlerOffset);
                        auto handlerEndInstruction = instructions->at(clause->HandlerOffset + clause->HandlerLength);
                        
                        auto handlerLength = handlerEndInstruction->GetOffset() - handlerInstruction->GetOffset();
                        clause->SetHandlerLength(handlerLength);
                        clause->SetHandlerOffset(handlerInstruction->GetOffset());
                        
                        if (clause->GetFlags() == static_cast<uint16_t>(COR_ILEXCEPTION_CLAUSE_FILTER)) {
                            auto filterInstruction = instructions->at(clause->FilterOffset);
                            clause->SetFilterOffset(filterInstruction->GetOffset());
                            
                            // There's no FilterLength to adjust.
                        }
                    }
                }

                // Returns a map of offets (starting at 0) to Instructions by parsing through the
                // original function body.
                static OffsetToInstructionMapPtr ReadInstructions(const ByteVectorPtr methodBody)
                {
                    OffsetToInstructionMapPtr instructions = std::make_shared<OffsetToInstructionMap>();
                    auto branches = std::make_shared<std::list<InstructionPtr>>();
                    for (unsigned oldBodyPosition = 0; oldBodyPosition < methodBody->size(); ) 
                    {
                        auto opCode = NewRelic::Profiler::GetOpCode(methodBody->data(), oldBodyPosition);
                        if (opCode == nullptr)
                        {
                            LogTrace(L"Unable to parse op code at line ", oldBodyPosition);
                            return nullptr;
                        }

                        if (opCode->instruction == CEE_SWITCH)
                        {
                            // switches are a special case - they have multiple targets
                            const unsigned numberArms = ReadNumber(methodBody->data() + oldBodyPosition + opCode->instructionSize, sizeof(DWORD));
                            const unsigned numberBytesTable = /* arm size */ sizeof(DWORD) + /* size of all arm instructions */(numberArms * sizeof(DWORD));
                            const unsigned totalBytesInstruction = opCode->instructionSize + numberBytesTable;

                            auto newInstruction = std::make_shared<SwitchInstruction>(opCode, oldBodyPosition, numberArms);
                            newInstruction->GetOpCode()->totalSize = totalBytesInstruction;

                            instructions->emplace(oldBodyPosition, newInstruction);
                            branches->push_back(newInstruction);
                        } else if (opCode->controlFlow == BRANCH)
                        {
                            auto newInstruction = std::make_shared<BranchInstruction>(opCode, oldBodyPosition);

                            instructions->emplace(oldBodyPosition, newInstruction);
                            branches->push_back(newInstruction);
                        }
                        else {
                            instructions->emplace(oldBodyPosition, std::make_shared<Instruction>(opCode, oldBodyPosition));
                        }
                        oldBodyPosition += opCode->totalSize;
                    }

                    // resolve the target instruction(s) of all branches
                    for (auto instruction : *branches)
                    {
                        instruction->ResolveTargets(methodBody, instructions);
                    }

                    return instructions;
                }

                // we have to DWORD align the SEH section following the method body
                static unsigned GetFunctionBodyAlignment(unsigned methodBodyEnd) {
                    unsigned methodBodyAlignment = methodBodyEnd % sizeof(DWORD);
                    return (methodBodyAlignment == 0) ? 0 : (sizeof(DWORD) - methodBodyAlignment);
                }

                // estimate the size of the new function
                unsigned GetNewFunctionSize() {
                    return (unsigned)_methodBytes->size() + (_headerInfo->GetReturnCount() - 1 * sizeof(DWORD)) + (_headerInfo->GetInstructionTypeCounts().shortBranchCount * sizeof(DWORD));
                }

                static HRESULT CopyOldEHSections(COR_ILMETHOD_SECT_EH* currentEHSection, COR_ILMETHOD_SECT_EH_CLAUSE_FAT* clauses) {
                    // This code references variable-sized structs, where the last element in the struct
                    // is declared as an array with size [1].  This seems to confuse the flow analysis
                    // in Visual Studio regarding out of bounds indexing, error C6385.
#pragma warning (push)
#pragma warning (disable : 6385)
                    if (NULL == currentEHSection) {
                        return S_FALSE;
                    }
                    do
                    {
                        const unsigned count = currentEHSection->EHCount();
                        for (unsigned i = 0; i < count; ++i)
                        {
                            if (currentEHSection->IsFat())
                            {
                                clauses[i].SetFlags(currentEHSection->Fat.Clauses[i].Flags);
                                clauses[i].SetClassToken(currentEHSection->Fat.Clauses[i].ClassToken);
                                clauses[i].SetTryOffset(currentEHSection->Fat.Clauses[i].TryOffset);
                                clauses[i].SetTryLength(currentEHSection->Fat.Clauses[i].TryLength);
                                clauses[i].SetHandlerOffset(currentEHSection->Fat.Clauses[i].HandlerOffset);
                                clauses[i].SetHandlerLength(currentEHSection->Fat.Clauses[i].HandlerLength);
                                clauses[i].SetFilterOffset(currentEHSection->Fat.Clauses[i].FilterOffset);
                            }
                            else
                            {
                                SectEH_EHClause(currentEHSection, i, &clauses[i]);
                            }
                        }

                        if (currentEHSection != NULL) {
                            do
                            {
                                currentEHSection = (COR_ILMETHOD_SECT_EH*)currentEHSection->Next();
                            } while (currentEHSection != NULL && (currentEHSection->Kind() & CorILMethod_Sect_KindMask) != CorILMethod_Sect_EHTable);
                        }
                    } while (currentEHSection != NULL);

                    return S_OK;
#pragma warning (pop)
                }
            };
        }
    }
}
