/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once

#include <string>
#include <vector>
#include "../Common/Strings.h"

namespace NewRelic { namespace Profiler { namespace Configuration
{
    class Strings
    {
    public:
        static std::vector<xstring_t> Split( const xstring_t& text, const xstring_t& delimiter )
        {
            std::vector<xstring_t> result;

            xstring_t::size_type start = 0;
            xstring_t::size_type end   = text.find( delimiter, start );

            while( end != xstring_t::npos )
            {
                xstring_t token = text.substr( start, end - start );

                result.push_back( token );

                start = end + delimiter.length();
                end   = text.find( delimiter, start );
            }

            result.push_back( text.substr( start ) );

            return result;
        }

        static bool AreEqualCaseInsensitive(xstring_t const& s1, xstring_t const& s2)
        {
            return NewRelic::Profiler::Strings::AreEqualCaseInsensitive(s1, s2);
        }

        static bool EndsWith(xstring_t stringToSearch, xstring_t token) 
        {
            bool result = false;
            size_t tokenLength = token.length();

            if (stringToSearch.length() >= tokenLength) 
            {
                xstring_t endChars = stringToSearch.substr(
                    stringToSearch.length() - tokenLength,
                    tokenLength);
                result = AreEqualCaseInsensitive(endChars, token);
            } 

            return result;
        }
    };
}}}

