// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once


#ifdef PAL_STDCPP_COMPAT

#include <pal_assert.h>
#include <pal.h>
#include <palprivate.h>
#include <palrt.h>
// shut up the compiler
#define HKEY_LOCAL_MACHINE 0x0

#ifndef AtlThrow
#define AtlThrow(a) RaiseException(STATUS_NO_MEMORY,EXCEPTION_NONCONTINUABLE,0,nullptr);
#endif
#ifndef ATLASSERT
#define ATLASSERT(a) _ASSERTE(a)
#endif

#include <atl.h>
//#include <atlwin.h>

#else // PAL_STDCPP_COMPAT (not)

// ATL Header Files:
#include <atlbase.h>
#include <atlcomcli.h>

#endif // PAL_STDCPP_COMPAT


// Profiler Header Files:
#include <corhlpr.h>

// Windows Header Files:
#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0502
#endif
//#include <sdkddkver.h>
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// RapidXML
#include "../RapidXML/rapidxml.hpp"

// STL Header Files:
#include <stdint.h>
#include <exception>
#include <sstream>
#include <vector>
#include <fstream>

#include "../Common/xplat.h"

