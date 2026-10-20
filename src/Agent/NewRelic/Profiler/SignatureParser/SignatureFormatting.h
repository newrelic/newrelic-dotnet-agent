// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
#pragma once
#include "SignatureParser.h"
#include "ITokenResolver.h"

namespace NewRelic { namespace Profiler { namespace SignatureParser
{
    // Render a parsed method signature's parameters as an OTel-shaped list: "(T1, T2)", or "()" when there
    // are no parameters. Each parameter's type is rendered by its own ToString (System.Int32, System.String[],
    // Base`N[Arg,...], !N/!!N for generic variables). Parameters are joined with ", " to match the
    // OpenTelemetry .NET profiler's frame format so continuous-profiling frames read identically.
    inline xstring_t FormatParameterList(MethodSignaturePtr signature, ITokenResolverPtr resolver)
    {
        xstring_t result(_X("("));
        bool first = true;
        for (auto& parameter : *signature->_parameters)
        {
            if (first) first = false;
            else result += _X(", ");
            result += parameter->ToString(resolver);
        }
        result.push_back(_X(')'));
        return result;
    }
}}}
