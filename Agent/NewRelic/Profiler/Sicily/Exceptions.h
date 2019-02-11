#pragma once
#include "../Common/xplat.h"
#include <exception>
#include <string>
#include <sstream>

#ifdef _MSC_VER
/* MSVC++ doesn't support `noexcept` */
#define __NOEXCEPT
#else
#define __NOEXCEPT noexcept(true)
#endif

namespace sicily
{
    struct MessageException : std::exception
    {
        MessageException() { }
        MessageException(xstring_t message) : _message(message) { }
        xstring_t _message;
    };

    struct NotImplementedException : MessageException
    {
        NotImplementedException() : MessageException(_X("not implemented exception")) { }
        NotImplementedException(xstring_t message) : MessageException(message) { }
    };

    struct SicilyException : MessageException
    {
        SicilyException() : MessageException(_X("sicily SicilyException")) { }
        SicilyException(xstring_t message) : MessageException(message) { }
    };

    struct ParserException : SicilyException
    {
        ParserException() : SicilyException(_X("sicily ParserException")) { }
        ParserException(xstring_t message) : SicilyException(message) { }
    };

    struct ScannerException : SicilyException
    {
        ScannerException() : SicilyException(_X("sicily ScannerException")) { }
        ScannerException(xstring_t message) : SicilyException(message) { }
    };

    struct AstException : SicilyException
    {
        AstException() : SicilyException(_X("sicily AstException")) { }
        AstException(xstring_t message) : SicilyException(message) { }
    };

    struct BytecodeGeneratorException : SicilyException
    {
        BytecodeGeneratorException() : SicilyException(_X("sicily BytecodeGeneratorException")) { }
        BytecodeGeneratorException(xstring_t message) : SicilyException(message) { }
    };
}
