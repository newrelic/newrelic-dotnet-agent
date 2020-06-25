/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <algorithm>
#include "xplat.h"

namespace NewRelic { namespace Profiler
{
    struct Strings
    {
        // Does a case sensitive string comparison and returns true if the sourceString ends with the suffixString.
        static bool EndsWith(xstring_t sourceString, xstring_t suffixString)
        {
            if (suffixString.length() > sourceString.length())
                return false;

            return std::equal(suffixString.rbegin(), suffixString.rend(), sourceString.rbegin());
        }

        static bool StartsWith(xstring_t sourceString, xstring_t prefixString)
        {
            if (prefixString.length() > sourceString.length())
                return false;

            return sourceString.substr(0, prefixString.length()) == prefixString;
        }

        static bool Contains(xstring_t stringToSearch, xstring_t token)
        {
            if (stringToSearch.length() < token.length())
                return false;

            return stringToSearch.find(token) != std::string::npos;
        }
        
        static bool ContainsCaseInsensitive(xstring_t const& stringToSearch, xstring_t const& token)
        {
            if (stringToSearch.length() < token.length())
                return false;

            auto it = std::search(
                stringToSearch.begin(), stringToSearch.end(),
                token.begin(), token.end(),
                [](const xchar_t& ch1, const xchar_t& ch2) { return ch1 == ch2 || ch1 == (ch2 ^ 32); });

            return (it != stringToSearch.end());
        }

        static bool AreEqualCaseInsensitive(xstring_t const& s1, xstring_t const& s2)
        {
            if (s1.size() != s2.size())
                return false;

            for (xstring_t::size_type i = 0; i < s1.size(); i++)
            {
                if (s1[i] != s2[i] && s1[i] != (s2[i] ^ 32))
                    return false;
            }

            return true;
        }
    };
}}
