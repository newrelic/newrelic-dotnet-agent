/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <unordered_map>
#include <unordered_set>
#include <stack>
#include <algorithm>
#include <string>
#include <stdint.h>
#include "../Common/Macros.h"
#include "Exceptions.h"
#include "../Logging/Logger.h"
#include "../Sicily/Sicily.h"
#include "../SignatureParser/Types.h"
#include "ExceptionHandlerManipulator.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    class InstructionSet
    {
    public:
        InstructionSet(sicily::codegen::ITokenizerPtr tokenizer, ExceptionHandlerManipulatorPtr exceptionHandlerManipulator) :
            _tokenizer(tokenizer),
            _exceptionHandlerManipulator(exceptionHandlerManipulator),
            _userCodeOffset(0),
            _labelCounter(0)
        {
            // we need a little over 400 bytes to store the bytes we inject so lets allocate at least that many up front, if we need more the vector will re-allocate
            _bytes.reserve(500);
        }

        virtual ~InstructionSet(void) { }

        // append an instruction
        void Append(ILCODE instruction)
        {
            if (instruction <= 0xff)
            {
                _bytes.push_back(uint8_t(instruction));
            }
            else
            {
                _bytes.push_back(uint8_t(0xfe));
                _bytes.push_back(uint8_t(instruction & 0xff));
            }
        }

        // append an instruction and its operand
        void Append(ILCODE instruction, uint8_t operand)
        {
            Append(instruction);
            AppendOperand(operand);
        }

        // append an instruction and its operand
        void Append(ILCODE instruction, uint16_t operand)
        {
            Append(instruction);
            AppendOperand(operand);
        }

        // append an instruction and its operand
        void Append(ILCODE instruction, uint32_t operand)
        {
            Append(instruction);
            AppendOperand(operand);
        }

        // append an instruction and its operand
        void Append(ILCODE instruction, uint64_t operand)
        {
            Append(instruction);
            AppendOperand(operand);
        }

        // append an instruction parsed from a string
        void Append(const xstring_t& string)
        {
            // split the instruction and its details
            auto firstSpace = string.find_first_of(_X(" "));
            auto instruction = string.substr(0, firstSpace);
            xstring_t details(_X(""));
            if (firstSpace != string.npos)
            {
                details = string.substr(firstSpace, string.npos);
                details.erase(0, details.find_first_not_of(_X(" ")));
            }

            // handle the various instructions we support
            if (instruction == _X("call"))
            {
                Append(CEE_CALL, details);
            }
            else if (instruction == _X("callvirt"))
            {
                Append(CEE_CALLVIRT, details);
            }
            else if (instruction == _X("ldstr"))
            {
                auto token = _tokenizer->GetStringToken(details);
                Append(CEE_LDSTR, token);
            }
            else if (instruction == _X("castclass"))
            {
                Append(CEE_CASTCLASS, details);
            }
            else if (instruction == _X("ldtoken"))
            {
                Append(CEE_LDTOKEN, details);
            }
            else if (instruction == _X("pop"))
            {
                Append(CEE_POP);
            }
            else if (instruction == _X("calli"))
            {
                Append(CEE_CALLI, details);
            }
            else if (instruction == _X("ldc.i4"))
            {
                Append(CEE_LDC_I4, uint32_t(xstoi(details)));
            }
            else if (instruction == _X("ldc.i4.s"))
            {
                Append(CEE_LDC_I4_S, uint8_t(xstoi(details)));
            }
            else if (instruction == _X("ldc.i4.0"))
            {
                Append(CEE_LDC_I4_0);
            }
            else if (instruction == _X("ldc.i4.1"))
            {
                Append(CEE_LDC_I4_1);
            }
            else if (instruction == _X("ldc.i4.2"))
            {
                Append(CEE_LDC_I4_2);
            }
            else if (instruction == _X("ldc.i4.3"))
            {
                Append(CEE_LDC_I4_3);
            }
            else if (instruction == _X("ldc.i4.4"))
            {
                Append(CEE_LDC_I4_4);
            }
            else if (instruction == _X("ldc.i4.5"))
            {
                Append(CEE_LDC_I4_5);
            }
            else if (instruction == _X("ldc.i4.6"))
            {
                Append(CEE_LDC_I4_6);
            }
            else if (instruction == _X("ldc.i4.7"))
            {
                Append(CEE_LDC_I4_7);
            }
            else if (instruction == _X("ldc.i4.8"))
            {
                Append(CEE_LDC_I4_8);
            }
            else if (instruction == _X("newarr"))
            {
                Append(CEE_NEWARR);
                ParseTokenizeAndAppendSimpleType(details);
            }
            else if (instruction == _X("nop"))
            {
                Append(CEE_NOP);
            }
            else if (instruction == _X("ret"))
            {
                Append(CEE_RET);
            }
            else if (instruction == _X("ldarg"))
            {
                Append(CEE_LDARG, uint16_t(xstoi(details)));
            }
            else if (instruction == _X("dup"))
            {
                Append(CEE_DUP);
            }
            else if (instruction == _X("box"))
            {
                Append(CEE_BOX);
                ParseTokenizeAndAppendSimpleType(details);
            }
            else if (instruction == _X("unbox.any"))
            {
                Append(CEE_UNBOX_ANY);
                ParseTokenizeAndAppendSimpleType(details);
            }
            else if (instruction == _X("ceq"))
            {
                Append(CEE_CEQ);
            }
            else if (instruction == _X("ldnull"))
            {
                Append(CEE_LDNULL);
            }
            else if (instruction == _X("stelem.ref"))
            {
                Append(CEE_STELEM_REF);
            }
            else if (instruction == _X("ldelem.ref"))
            {
                Append(CEE_LDELEM_REF);
            }
            else if (instruction == _X("throw"))
            {
                Append(CEE_THROW);
            }
            else if (instruction == _X("rethrow"))
            {
                Append(CEE_RETHROW);
            }
            else
            {
                LogError(L"Encountered unsupported instruction while attempting to generate byte code. Instruction: ", instruction);
                throw InstructionNotSupportedException(instruction);
            }
        }

        // append an instruction and an operand that is parsed from a string
        void Append(ILCODE instruction, const xstring_t& string)
        {
            Append(instruction);
            ParseTokenizeAndAppend(string);
        }

        // append bytes between two iterators
        void Append(const ByteVector::const_iterator& begin, const ByteVector::const_iterator& end)
        {
            _bytes.insert(_bytes.end(), begin, end);
        }

        // append a byte array to the instruction list
        void Append(const uint8_t* newBytes, size_t size)
        {
            for (size_t i = 0; i < size; ++i)
            {
                _bytes.push_back(newBytes[i]);
            }
        }

        // append the instruction to load the provided string onto the stack
        void AppendString(const xstring_t& string)
        {
            auto token = _tokenizer->GetStringToken(string);
            Append(CEE_LDSTR);
            AppendOperand(token);
        }

        void AppendJump(const uint8_t& instruction, const xstring_t& label)
        {
            AppendJump(label, instruction);
        }

        // append a long jump instruction that jumps to a label (defined later). will be a noop if the matching label is never defined.
        void AppendJump(const xstring_t& label, uint8_t instruction)
        {
            _bytes.push_back(instruction);
            // store the offset of the jump distance in our jumps map so we can updated it with a distance later
            _jumps.emplace(label, _bytes.size());
            _bytes.push_back(0x00);
            _bytes.push_back(0x00);
            _bytes.push_back(0x00);
            _bytes.push_back(0x00);
        }

        // append a long jump instruction that jumps to the label returned.  Will be a noop if the label is never appended with AppendLabel.
        xstring_t AppendJump(const uint8_t& instruction)
        {
            auto label = to_xstring(++_labelCounter) + _X("unique_jump_label");
            AppendJump(label, instruction);
            return label;
        }

        // append a label that can be jumped to.
        void AppendLabel(const xstring_t& label)
        {
            auto jumpLocations = _jumps.equal_range(label);
            std::for_each(jumpLocations.first, jumpLocations.second, [this](std::pair<xstring_t, size_t> jumpLocationPair)
            {
                auto jumpLocation = jumpLocationPair.second;
                auto distance = _bytes.size() - jumpLocation - 1;
                // because the jump instruction takes 3 extra bytes, we need to reduce the distance by 3
                distance -= 3;
                _bytes[jumpLocation + 0] = uint8_t(distance & 0xFF);
                _bytes[jumpLocation + 1] = uint8_t((distance >> 8) & 0xFF);
                _bytes[jumpLocation + 2] = uint8_t((distance >> 16) & 0xFF);
                _bytes[jumpLocation + 3] = uint8_t((distance >> 24) & 0xFF);
            });
        }

        // append a box instruction if necessary and a load argument instruction
        void AppendLoadArgumentAndBox(uint16_t argumentIndex, SignatureParser::ParameterPtr parameter)
        {
            try
            {
                // we can't box parameters passed by reference, put nulls in their place
                if (IsTypePassedByReference(parameter))
                {
                    Append(CEE_LDNULL);
                }
                // skip sentinels and box the rest
                else
                {
                    auto typeToken = GetTypeTokenForParameter(parameter);
                    // sentinel (token 0) is ignored
                    if (typeToken != 0)
                    {
                        AppendLoadArgument(argumentIndex);
                        Append(CEE_BOX, typeToken);
                    }
                }
            }
            // exceptions thrown here are non-critical, just load a null onto the stack instead
            catch (...)
            {
                Append(CEE_LDNULL);
            }
        }

        // append a box instruction if necessary and a load local instruction
        void AppendLoadLocalAndBox(uint16_t localIndex, SignatureParser::ReturnTypePtr returnType)
        {
            auto typeToken = GetTypeTokenForReturn(returnType);
            AppendLoadLocal(localIndex);
            Append(CEE_BOX, typeToken);
        }

        // append the instructions necessary to push a System.Type onto the stack for the given parameter
        void AppendTypeOfArgument(SignatureParser::ParameterPtr parameter)
        {
            auto typeToken = GetTypeTokenForParameter(parameter);
            // sentinel (token 0) is ignored
            if (typeToken != 0)
            {
                Append(CEE_LDTOKEN, typeToken);
                Append(_X("call class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)"));
            }
        }

        // append a load argument instruction
        void AppendLoadArgument(uint16_t argumentIndex)
        {
            if (argumentIndex < 4) {
                auto instruction = CEE_LDARG_0 + argumentIndex;
                Append((ILCODE)instruction);
            }
            else if (argumentIndex < 255) {
                Append(CEE_LDARG_S, uint8_t(argumentIndex));
            }
            else {
                Append(CEE_LDARG, uint16_t(argumentIndex));
            }
        }

        // append a load local instruction
        void AppendLoadLocal(uint16_t localIndex)
        {
            if (localIndex < 4) {
                auto instruction = CEE_LDLOC_0 + localIndex;
                Append((ILCODE)instruction);
            }
            else if (localIndex < 255) {
                // short form
                Append(CEE_LDLOC_S);
                AppendOperand(uint8_t(localIndex));
            }
            else {
                Append(CEE_LDLOC, localIndex);
            }
        }

        // append a store local instruction
        void AppendStoreLocal(uint16_t localIndex)
        {
            if (localIndex < 4) {
                auto instruction = CEE_STLOC_0 + localIndex;
                Append((ILCODE)instruction);
            }
            else if (localIndex < 255) {
                // short form
                Append(CEE_STLOC_S);
                AppendOperand(uint8_t(localIndex));
            }
            else {
                Append(CEE_STLOC, localIndex);
            }
        }

        void AppendTryStart()
        {
            FatExceptionHandlingClausePtr exceptionClause(new FatExceptionHandlingClause());
            exceptionClause->_tryOffset = uint32_t(_bytes.size());
            _exceptionStack.push(exceptionClause);
        }

        void AppendTryEnd()
        {
            auto exception = _exceptionStack.top();
            if (exception->_tryLength != 0)
            {
                LogError(L"Attempted to set try close on the same exception twice.");
                throw InstructionSetException();
            }
            exception->_tryLength = uint32_t(_bytes.size() - exception->_tryOffset);
        }

        void AppendCatchStart(uint32_t typeToken)
        {
            auto exception = _exceptionStack.top();
            if (exception->_handlerOffset != 0)
            {
                LogError(L"Attempted to set catch start on the same exception twice.");
                throw InstructionSetException();
            }
            exception->_handlerOffset = uint32_t(_bytes.size());
            exception->_flags = 0;
            exception->_classToken = typeToken;
        }

        void AppendCatchStart(xstring_t fullyQualifiedClassName)
        {
            auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), fullyQualifiedClassName);
            AppendCatchStart(token);
        }

        void AppendCatchStart()
        {
            auto exceptionToken = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.Exception"));
            AppendCatchStart(exceptionToken);
        }

        void AppendCatchEnd()
        {
            auto exception = _exceptionStack.top();
            if (exception->_handlerLength != 0)
            {
                LogError(L"Attempted to set catch end on the same exception twice.");
                throw InstructionSetException();
            }
            exception->_handlerLength = uint32_t(_bytes.size() - exception->_handlerOffset);

            // this exception handler is finished, add it to the set of exception handlers
            _exceptionHandlerManipulator->AddExceptionHandlingClause(exception);

            // pop this exception off of our stack
            _exceptionStack.pop();
        }

        void AppendUserCode(const ByteVector& userCode)
        {
            AppendUserCodeMarker();
            Append(userCode);
        }

        // returns the byte array for this set of instructions
        const ByteVector GetBytes() const
        {
            return _bytes;
        }

        // returns the offset to the user's original code (needed to offset the exception handling clauses)
        uint32_t GetUserCodeOffset() const
        {
            return _userCodeOffset;
        }

    private:
        void Append(const ByteVector& newBytes)
        {
            Append(newBytes.begin(), newBytes.end());
        }

        // throws an exception for unsupported types and returns 0 for sentinel
        uint32_t GetTypeTokenForReturn(SignatureParser::ReturnTypePtr returnType)
        {
            switch (returnType->_kind)
            {
                case SignatureParser::ReturnType::Kind::VOID_RETURN_TYPE:
                {
                    LogError(L"Void return to token not supported.");
                    throw InstructionSetException();
                }
                case SignatureParser::ReturnType::Kind::TYPED_BY_REF_RETURN_TYPE:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.TypedReference"));
                    return token;
                }
                case SignatureParser::ReturnType::Kind::TYPED_RETURN_TYPE:
                {
                    auto typedReturnType = std::static_pointer_cast<SignatureParser::TypedReturnType>(returnType);
                    if (typedReturnType->_isByRef)
                    {
                        LogError(L"By ref tokenization not supported.");
                        throw InstructionSetException();
                    }
                    return GetTypeTokenForType(typedReturnType->_type);
                }
                default:
                {
                    LogError(L"Attempted to load and box a return type of an unknown kind. Kind: ", returnType->_kind);
                    throw MethodRewriterException();
                }
            }
        }

        // throws an exception for unsupported types and returns 0 for sentinel
        uint32_t GetTypeTokenForParameter(SignatureParser::ParameterPtr parameter)
        {
            switch (parameter->_kind)
            {
                case SignatureParser::Parameter::Kind::SENTINEL_PARAMETER:
                {
                    return 0;
                }
                case SignatureParser::Parameter::Kind::TYPED_BY_REF_PARAMETER:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.TypedReference"));
                    return token;
                }
                case SignatureParser::Parameter::Kind::TYPED_PARAMETER:
                {
                    auto typedParameter = std::static_pointer_cast<SignatureParser::TypedParameter>(parameter);
                    return GetTypeTokenForType(typedParameter->_type);
                }
                default:
                {
                    LogError(L"Unhandled parameter type encountered in InstructionSet::GetTypeTokeNForParameter. Kind: ", std::hex, std::showbase, parameter->_kind, std::resetiosflags(std::ios_base::basefield|std::ios_base::showbase));
                    throw InstructionSetException();
                }
            }
        }

        uint32_t GetTypeTokenForType(SignatureParser::TypePtr type)
        {
            switch (type->_kind)
            {
                case SignatureParser::Type::Kind::BOOLEAN:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.Boolean"));
                    return token;
                }
                case SignatureParser::Type::Kind::CHAR:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.Char"));
                    return token;
                }
                case SignatureParser::Type::Kind::SBYTE:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.SByte"));
                    return token;
                }
                case SignatureParser::Type::Kind::BYTE:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.Byte"));
                    return token;
                }
                case SignatureParser::Type::Kind::INT16:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.Int16"));
                    return token;
                }
                case SignatureParser::Type::Kind::UINT16:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.UInt16"));
                    return token;
                }
                case SignatureParser::Type::Kind::INT32:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.Int32"));
                    return token;
                }
                case SignatureParser::Type::Kind::UINT32:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.UInt32"));
                    return token;
                }
                case SignatureParser::Type::Kind::INT64:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.Int64"));
                    return token;
                }
                case SignatureParser::Type::Kind::UINT64:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.UInt64"));
                    return token;
                }
                case SignatureParser::Type::Kind::SINGLE:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.Single"));
                    return token;
                }
                case SignatureParser::Type::Kind::DOUBLE:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.Double"));
                    return token;
                }
                case SignatureParser::Type::Kind::INTPTR:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.IntPtr"));
                    return token;
                }
                case SignatureParser::Type::Kind::UINTPTR:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.UIntPtr"));
                    return token;
                }
                case SignatureParser::Type::Kind::OBJECT:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.Object"));
                    return token;
                }
                case SignatureParser::Type::Kind::STRING:
                {
                    auto token = _tokenizer->GetTypeRefToken(_X("mscorlib"), _X("System.String"));
                    return token;
                }
                case SignatureParser::Type::Kind::ARRAY:
                {
                    auto signature = type->ToBytes();
                    auto token = _tokenizer->GetTypeSpecToken(*signature);
                    return token;
                }
                case SignatureParser::Type::Kind::CLASS:
                {
                    auto classType = std::static_pointer_cast<SignatureParser::ClassType>(type);
                    return classType->_typeToken;
                }
                case SignatureParser::Type::Kind::VALUETYPE:
                {
                    auto valueType = std::static_pointer_cast<SignatureParser::ValueTypeType>(type);
                    return valueType->_typeToken;
                }
                case SignatureParser::Type::Kind::GENERIC:
                {
                    auto signature = type->ToBytes();
                    auto token = _tokenizer->GetTypeSpecToken(*signature);
                    return token;
                }
                case SignatureParser::Type::Kind::MVAR:
                {
                    auto signature = type->ToBytes();
                    auto token = _tokenizer->GetTypeSpecToken(*signature);
                    return token;
                }
                case SignatureParser::Type::Kind::VAR:
                {
                    auto signature = type->ToBytes();
                    auto token = _tokenizer->GetTypeSpecToken(*signature);
                    return token;
                }
                case SignatureParser::Type::Kind::SINGLEDIMENSIONARRAY:
                {
                    auto signature = type->ToBytes();
                    auto token = _tokenizer->GetTypeSpecToken(*signature);
                    return token;
                }
                // pointer types throw an exception because I don't know how to handle them. to figure out how they work something like the following needs to be run through ILDasm:
                // System.Type typeOfClassPointer = typeof(MyClass*);
                // System.Type typeOfVoidPointer = typeof(void*);
                // System.Type typeOfFunctionPointer = typeof((void*)(void));
                // Unfortunately, I don't actually know how to write that in C# (the above is pseudocode)
                case SignatureParser::Type::Kind::FUNCTIONPOINTER:
                {
                    LogWarn("Function pointer tokenization not supported.");
                    throw InstructionSetException();
                }
                case SignatureParser::Type::Kind::POINTER:
                {
                    LogWarn("Pointer tokenization not supported.");
                    throw InstructionSetException();
                }
                case SignatureParser::Type::Kind::VOIDPOINTER:
                {
                    LogWarn("Void pointer tokenization not supported.");
                    throw InstructionSetException();
                }
                default:
                {
                    LogError(L"Unhandled type kind encountered.  Kind: ", std::hex, std::showbase, type->_kind, std::resetiosflags(std::ios_base::basefield|std::ios_base::showbase));
                    throw InstructionSetException();
                }
            }
        }

        bool IsTypePassedByReference(SignatureParser::ParameterPtr parameter)
        {
            if (parameter->_kind != SignatureParser::Parameter::Kind::TYPED_PARAMETER) return false;
            
            auto typedParameter = std::static_pointer_cast<SignatureParser::TypedParameter>(parameter);
            return typedParameter->_isByRef;
        }

        void AppendUserCodeMarker()
        {
            _userCodeOffset = (uint32_t)(_bytes.size());
        }

        // Parse a string using sicily, turn it into bytecode using sicily, then append it onto the instruction set
        void ParseTokenizeAndAppend(xstring_t details)
        {
            // parse
            try
            {
                sicily::Scanner scanner(details);
                sicily::Parser parser;
                sicily::ast::TypePtr type = parser.Parse(scanner);

                // tokenize
                sicily::codegen::ByteCodeGenerator generator(_tokenizer);
                auto token = generator.TypeToToken(type);

                // append
                AppendOperand(token);
            }
            catch (const sicily::MessageException& exception)
            {
                LogError(L"Failed to parse or tokenize CIL string: ", details);
                LogError("Exception details: ", exception._message);
                throw;
            }
        }

        void ParseTokenizeAndAppendSimpleType(xstring_t details)
        {
            // parse
            auto openBracePosition = details.find_first_of('[');
            auto closeBracePosition = details.find_first_of(']');
            auto assemblyName = details.substr(openBracePosition + 1, closeBracePosition - openBracePosition - 1);
            auto fullyQualifiedClassName = details.substr(closeBracePosition + 1, details.npos);
                
            // tokenize
            auto token = _tokenizer->GetTypeRefToken(assemblyName, fullyQualifiedClassName);

            // append
            AppendOperand(token);
        }

        void AppendOperand(uint8_t operand)
        {
            _bytes.push_back(operand);
        }

        void AppendOperand(uint16_t operand)
        {
            _bytes.push_back(uint8_t(operand & 0xff));
            _bytes.push_back(uint8_t((operand >> 8) & 0xff));
        }

        void AppendOperand(uint32_t operand)
        {
            _bytes.push_back(uint8_t(operand & 0xff));
            for (int i = 1; i < 4; i++)
            {
                _bytes.push_back(uint8_t((operand >> (8 * i)) & 0xff));
            }
        }

        void AppendOperand(uint64_t operand)
        {
            _bytes.push_back(uint8_t(operand & 0xff));
            for (int i = 1; i < 8; i++)
            {
                _bytes.push_back(uint8_t((operand >> (8 * i)) & 0xff));
            }
        }

    private:
        // our vector of bytes that make up this method
        ByteVector _bytes;
        // a map of jump labels to the source of the jump
        std::unordered_multimap<xstring_t, ByteVector::size_type> _jumps;
        // the tokenizer we should use for tokenizing things
        sicily::codegen::ITokenizerPtr _tokenizer;
        // exception handler manipulator
        ExceptionHandlerManipulatorPtr _exceptionHandlerManipulator;
        // exception handling clauses currently being built
        std::stack<ExceptionHandlingClausePtr> _exceptionStack;
        // the offset to the original user code (used to build exception handler clauses)
        uint32_t _userCodeOffset;
        // counter for generating unique jump labels
        uint32_t _labelCounter;
    };

    typedef std::shared_ptr<InstructionSet> InstructionSetPtr;
}}}
