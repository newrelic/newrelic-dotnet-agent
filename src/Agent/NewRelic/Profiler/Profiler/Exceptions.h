/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <exception>
#include <string>
#include <sstream>
#include <vector>
#include <stdint.h>

namespace NewRelic { namespace Profiler
{
    struct MessageException
    {
        MessageException() : _message(_X("Profiler MessageException")) { }
        MessageException(const xstring_t& message) : _message(message) { }

        xstring_t _message;
    };

    struct NonCriticalException : MessageException
    {
        NonCriticalException() : MessageException(_X("Non Critical Profiler Exception")) {}
        NonCriticalException(const xstring_t& message) : MessageException(message) {}
    };

    struct ProfilerException : MessageException
    {
        ProfilerException() : MessageException(_X("ProfilerException")) {}
        ProfilerException(const xstring_t& message) : MessageException(message) {}
    };

    struct AssemblyNotSupportedException : MessageException
    {
        AssemblyNotSupportedException(const xstring_t& assemblyName) :
            _assemblyName(assemblyName)
        {
            _message = xstring_t(_X("Assembly not supported. Assembly Name: ")) + _assemblyName;
        }
        xstring_t _assemblyName;
    };

    struct UnableToDefineAssemblyRefException : MessageException
    {
        UnableToDefineAssemblyRefException(const xstring_t& assemblyName) :
            _assemblyName(assemblyName)
        {;
            _message = xstring_t(_X("Unable to define AssemblyRef. Assembly name: ")) + assemblyName;
        }
        xstring_t _assemblyName;
    };

    struct ModuleNotFoundException : MessageException
    {
        ModuleNotFoundException(const xstring_t& moduleName, const xstring_t& methodName) :
            _moduleName(moduleName),
            _methodName(methodName)
        {
            _message = xstring_t(_X("Module not found.  Module Name: ")) + _moduleName + _X(";  Method Name: ") + _methodName;
        }
        xstring_t _moduleName;
        xstring_t _methodName;
    };

    // exception containing a failed HRESULT
    struct Win32Exception : MessageException
    {
        Win32Exception(HRESULT result) :
            _result(result)
        {
            //to_hex_string will provide 0x
            _message = xstring_t(_X("Win32 error occurred.  HRESULT: ")) + to_hex_string(result, 0, true);
        }
        
        HRESULT _result;
    };

    struct Win32NullHandleException : MessageException
    {
        Win32NullHandleException() : MessageException(_X("Win32 error occurred.  Null handle returned by function.")) {}
    };

    struct StackSnapshooterException : ProfilerException {};
    struct FailedToOpenThreadException : StackSnapshooterException {};
    struct FailedToSuspendThreadException : StackSnapshooterException {};
    struct FailedToGetThreadContextException : StackSnapshooterException {};
    struct FailedToResumeThreadException : StackSnapshooterException {};
    struct FailedToCloseThreadException : StackSnapshooterException {};

    struct FailedToGetFunctionInformationException : ProfilerException {};
}}
