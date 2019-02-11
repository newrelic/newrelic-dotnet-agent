#pragma once
#include <exception>
#include <string>
#include "../Common/xplat.h"

namespace NewRelic { namespace Profiler { namespace SignatureParser
{
	struct MessageException : std::exception
	{
		MessageException() { }
		MessageException(xstring_t message) : _message(message) { }
	
		xstring_t _message;
	};

	struct SignatureParserException : MessageException
	{
		SignatureParserException() : MessageException(_X("SignatureParserException")) {}
		SignatureParserException(xstring_t message) : MessageException(message) {}
	};
}}}
