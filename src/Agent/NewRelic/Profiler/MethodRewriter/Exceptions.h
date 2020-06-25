/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <exception>
#include <string>
#include <stdint.h>
#include "../Common/Macros.h"
#include "../Common/xplat.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    struct MessageException : std::exception
    {
        MessageException() { }
        MessageException(xstring_t message) : _message(message) { }
    
        xstring_t _message;
    };

    struct NotImplementedException : MessageException
    {
        NotImplementedException() : MessageException(_X("NotImplementedException")) { }
    };

    struct MethodRewriterException : MessageException
    {
        MethodRewriterException() : MessageException(_X("MethodRewriterException")) { }
        MethodRewriterException(xstring_t message) : MessageException(message) { }
    };

    struct FunctionManipulatorException : MethodRewriterException
    {
        FunctionManipulatorException() : MethodRewriterException(_X("MethodRewriter FunctionManipulatorException")) { }
        FunctionManipulatorException(xstring_t message) : MethodRewriterException(message) { }
    };

    struct FatalFunctionManipulatorException : FunctionManipulatorException
    {
        FatalFunctionManipulatorException(xstring_t message) : FunctionManipulatorException(message) {}
        FatalFunctionManipulatorException(MessageException exception) : FunctionManipulatorException(exception._message), _innerException(exception) {}

        std::exception _innerException;
    };

    struct TypeParserException : FunctionManipulatorException
    {
        TypeParserException() : FunctionManipulatorException(_X("MethodRewriter TypeParserException")) { }
        TypeParserException(xstring_t message) : FunctionManipulatorException(message) { }
    };

    struct UnhandledTypeByteException : TypeParserException
    {
        UnhandledTypeByteException(uint8_t type) :
            TypeParserException(_X("Unhandled type byte found when attempting to parse signature. Type: ") + to_xstring((unsigned)type)),
            _type(type)
        { }

        uint8_t _type;
    };

    struct InstructionSetException : MessageException
    {
        InstructionSetException() : MessageException(_X("MethodRewriter InstructionSetException")) { }
        InstructionSetException(xstring_t message) : MessageException(message) { }
    };

    struct InstructionNotSupportedException : InstructionSetException
    {
        InstructionNotSupportedException(xstring_t label) :
            InstructionSetException(_X("MethodRewriter InstructionNotSupportedException.  Label: ") + label),
            label(label)
        { }
        xstring_t label;
    };

    struct IncorrectlySizedJumpException : InstructionSetException
    {
        IncorrectlySizedJumpException() : InstructionSetException(_X("MethodRewriter IncorrectlySizedJumpException")) { }
        IncorrectlySizedJumpException(xstring_t message) : InstructionSetException(message) { }
    };

    struct InvalidJumpLabelException : InstructionSetException
    {
        InvalidJumpLabelException(xstring_t label) :
            InstructionSetException(_X("MethodRewriter InvalidJumpLabelException") + label),
            label(label)
        { }
        xstring_t label;
    };

    // exception thrown when IUnknown::QueryInterface is unable to get an interface 
    struct InterfaceNotFoundException : MessageException
    {
        InterfaceNotFoundException() : MessageException(_X("MethodRewriter InterfaceNotFoundException.")) { }
        InterfaceNotFoundException(xstring_t message) : MessageException(message) { }
    };

    struct TypeNotSupportedException : MessageException
    {
        TypeNotSupportedException(xstring_t message) : MessageException(message) {}
    };

    struct ExceptionHandlerManipulatorException : MessageException
    {
        ExceptionHandlerManipulatorException(xstring_t message) : MessageException(message) {}
    };

    struct NoAgentConfiguration : MessageException
    {
        NoAgentConfiguration(xstring_t message) : MessageException(message) {}
    };
}}}
