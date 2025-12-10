/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <algorithm>
#include <codecvt>
#include <fstream>
#include <iostream>

#include "xplat.h"
#include "../Logging/Logger.h"

namespace NewRelic {
    namespace Profiler
    {
        /// <summary>
        /// Reads the contents of a file specified by a wide string path and returns it as a wide string.
        /// Handles UTF-8 encoding and BOM if present.
        /// If the file cannot be opened, logs an error and throws an exception.
        /// </summary>
        static xstring_t ReadFile(const xstring_t& filePath) {
            // Open file with wide character path
            xifstream file(to_pathstring(filePath), std::ios::binary);
            if (!file.is_open()) {
                LogError(L"Unable to open file. File path: ", filePath);
                throw std::exception();
            }

            // Configure locale for UTF-8 and handle BOM
            file.imbue(std::locale(file.getloc(), new std::codecvt_utf8_utf16<xchar_t, 0x10ffff, std::consume_header>())); // xchar_t is wchar_t on Windows, char16_t on Linux

            // Read file content
            xstringstream ss;
            ss << file.rdbuf();

            return ss.str();
        }
    }
};
