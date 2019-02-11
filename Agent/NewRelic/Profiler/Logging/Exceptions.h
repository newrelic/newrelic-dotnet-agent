#pragma once
#include <exception>
#include "../Common/xplat.h"

namespace NewRelic { namespace Profiler { namespace Logger
{
	struct MessageException : std::exception
	{
		MessageException() { }
		MessageException(xstring_t message) : _message(message) { }
	
		xstring_t _message;
	};

	struct LoggerException : MessageException
	{
		LoggerException() : MessageException(_X("LoggerException")) {}
		LoggerException(xstring_t message) : MessageException(message) {}
	};
}}}
