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

namespace NewRelic {
    namespace Profiler
    {
        static std::wstring ReadFile(const std::wstring& filePath) {
            // Open file with wide character path
            std::wifstream file(filePath, std::ios::binary);
            if (!file.is_open()) {
                std::wcerr << L"Failed to open file: " << filePath << std::endl;
                return L"";
            }

            // Configure locale for UTF-8 and handle BOM
            file.imbue(std::locale(file.getloc(), new std::codecvt_utf8_utf16<wchar_t, 0x10ffff, std::consume_header>()));

            // Read file content
            std::wstringstream wss;
            wss << file.rdbuf();

            return wss.str();
        }


    }
};
