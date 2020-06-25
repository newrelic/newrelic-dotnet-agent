/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once

// windows
//#include <sdkddkver.h>
#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
#include <windows.h>

// stl
#include <string>
#include <memory>
#include <sstream>
#include <fstream>
#include <ctime>
#include <mutex>
#include <stdint.h>
#include <list>
#include <time.h>
#include <locale>
//#include <codecvt> deprecated in c++17