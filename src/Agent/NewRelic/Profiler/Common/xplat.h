/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <string>
#include <iomanip>
#include <sstream>

// Cross-platform definitions.  The big issue we tackle is that Win APIs use lots of WCHAR* definitions, and 
// WCHAR is defined as wchar_t on windows and char16_t on Linux (wchar_t is 2 bytes on windows and 4 on Unix).
// To address this, most of our code references the xchar_t and xstring_t typedefs.

// We use _X for most string literals.

// From palclr.h:
// This macro is used to standardize the wide character string literals between UNIX and Windows.
// Unix L"" is UTF32, and on windows it's UTF16.  Because of built-in assumptions on the size
// of string literals, it's important to match behaviour between Unix and Windows.  Unix will be defined
// as u"" (char16_t)
#ifdef PLATFORM_UNIX
#define W(str) u##str
#else // PLATFORM_UNIX
#define W(str) L##str
#endif // PLATFORM_UNIX

#ifdef PAL_STDCPP_COMPAT

#include <codecvt>
#include <locale>

#if defined(__llvm__)
namespace std{
template <class T, class... Args>
typename std::enable_if<!std::is_array<T>::value, std::unique_ptr<T>>::type
make_unique(Args &&... args) {
    return std::unique_ptr<T>(new T(std::forward<Args>(args)...));
}

/// \brief Constructs a `new T[n]` with the given args and returns a                           
///        `unique_ptr<T[]>` which owns the object.                                            
///                                                                                            
/// \param n size of the new array.                                                            
///                                                                                            
/// Example:                                                                                   
///                                                                                            
///     auto p = make_unique<int[]>(2); // value-initializes the array with 0's.               
template <class T>
typename std::enable_if<std::is_array<T>::value && std::extent<T>::value == 0,
    std::unique_ptr<T>>::type
    make_unique(size_t n) {
    return std::unique_ptr<T>(new typename std::remove_extent<T>::type[n]());
}

/// This function isn't used and is only here to provide better compile errors.                
template <class T, class... Args>
typename std::enable_if<std::extent<T>::value != 0>::type
make_unique(Args &&...) = delete;
}
#endif

// Per http://en.cppreference.com/w/c/string/byte/memcpy, define this to uses memcpy_s
#define __STDC_WANT_LIB_EXT1__ 1

typedef std::u16string::value_type xchar_t;
typedef std::u16string xstring_t;
typedef std::basic_ifstream<xchar_t> xifstream;
typedef std::basic_stringstream<xchar_t> xstringstream;

// The basic_ifstream api expects a UTF-8 path, so we need to convert the xstring_t to a UTF-8 string.
inline std::string to_pathstring(const xstring_t& str)
{
    return std::wstring_convert <std::codecvt_utf8 <xchar_t>, xchar_t>().to_bytes(str);
}

inline xstring_t to_xstring(unsigned short val)
{
    auto str = std::to_string(val);
    return xstring_t(str.begin(), str.end());
}
inline xstring_t to_xstring(unsigned int val) 
{ 
    auto str = std::to_string(val);
    return xstring_t(str.begin(), str.end());
}
inline xstring_t to_xstring(unsigned long val) 
{ 
    auto str = std::to_string(val);
    return xstring_t(str.begin(), str.end());
}
inline int xstoi(xstring_t str)
{
    return std::stoi(std::string(str.begin(), str.end()));
}

#define _X(s) u ## s

#define PATH_SEPARATOR _X("/")

inline xstring_t ToWideString(const char* const buf)
{
    auto str = std::string(buf);
    return xstring_t(str.begin(), str.end());
}

// implementations of windows apis
inline int wcscmp(const xstring_t& s1, const xstring_t& s2)
{
    return s1.compare(s2) == 0;
}

inline int gmtime_s(struct tm* _tm, const time_t* time)
{
    return gmtime_r(time, _tm) == nullptr ? EINVAL : 0;
}

#else
// Windows

typedef wchar_t xchar_t;
typedef std::basic_string<xchar_t> xstring_t;
typedef std::wifstream xifstream;
typedef std::wstringstream xstringstream;

// The wifstream api expects a wide character path, which we already have so we can just return the string as is.
inline xstring_t to_pathstring(const xstring_t& str) { return str; }

inline xstring_t to_xstring(unsigned short val) { return std::to_wstring(val); }
inline xstring_t to_xstring(unsigned int val) { return std::to_wstring(val); }
inline xstring_t to_xstring(unsigned long val) { return std::to_wstring(val); }
inline int xstoi(xstring_t str)
{
    return std::stoi(str);
}

#define _X(s) L ## s

#define PATH_SEPARATOR _X("\\")

inline xstring_t ToWideString(const char* const buf)
{
    if (buf != nullptr && *buf != 0)
    {
        return xstring_t(buf, buf+strlen(buf));
    }
    return xstring_t();
}

#endif

typedef std::basic_ostringstream<xchar_t> xostringstream;
typedef std::basic_ostream<xchar_t> xostream;
typedef std::basic_ifstream<xchar_t> xifstream;

template <typename _T>
inline xstring_t to_hex_string(_T value, int width = 0, bool showbase=false)
{
    std::wostringstream oss;
    oss << std::hex;
    if (showbase)
        oss << std::showbase;
    if (width > 0) {
        oss << std::setw(width) << std::setfill(L'0');
    }
    oss << value;
    const auto str = oss.str();
    return xstring_t(str.cbegin(), str.cend());
}
