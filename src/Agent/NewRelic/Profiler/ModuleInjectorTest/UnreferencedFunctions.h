// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once
#include <CppUnitTest.h>

// fix unreferenced local functions warning in CppUnitTest.h
static inline void UseUnreferenced()
{
    std::wstring (*boolFunc)(const bool&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)boolFunc;
    std::wstring (*intFunc)(const int&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)intFunc;
    std::wstring (*longFunc)(const long&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)longFunc;
    std::wstring (*shortFunc)(const short&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)shortFunc;
    std::wstring (*charFunc)(const char&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)charFunc;
    std::wstring (*wchar_tFunc)(const wchar_t&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)wchar_tFunc;
    std::wstring (*signedCharFunc)(const signed char&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)signedCharFunc;
    std::wstring (*unsignedIntFunc)(const unsigned int&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedIntFunc;
    std::wstring (*unsignedLongFunc)(const unsigned long&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedLongFunc;
    std::wstring (*unsignedLongLongFunc)(const unsigned long long&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedLongLongFunc;
    std::wstring (*unsignedCharFunc)(const unsigned char&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedCharFunc;
    std::wstring (*stringFunc)(const std::string&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)stringFunc;
    std::wstring (*wstringFunc)(const std::wstring&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)wstringFunc;
    std::wstring (*doubleFunc)(const double&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)doubleFunc;
    std::wstring (*floatFunc)(const float&) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)floatFunc;

    std::wstring (*boolFuncConstPtr)(const bool*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)boolFuncConstPtr;
    std::wstring (*intFuncConstPtr)(const int*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)intFuncConstPtr;
    std::wstring (*longFuncConstPtr)(const long*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)longFuncConstPtr;
    std::wstring (*shortFuncConstPtr)(const short*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)shortFuncConstPtr;
    std::wstring (*signedCharFuncConstPtr)(const signed char*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)signedCharFuncConstPtr;
    std::wstring (*unsignedIntFuncConstPtr)(const unsigned int*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedIntFuncConstPtr;
    std::wstring (*unsignedLongFuncConstPtr)(const unsigned long*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedLongFuncConstPtr;
    std::wstring (*unsignedLongLongFuncConstPtr)(const unsigned long long*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedLongLongFuncConstPtr;
    std::wstring (*unsignedCharFuncConstPtr)(const unsigned char*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedCharFuncConstPtr;
    std::wstring (*charFuncConstPtr)(const char*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)charFuncConstPtr;
    std::wstring (*wchar_tFuncConstPtr)(const wchar_t*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)wchar_tFuncConstPtr;
    std::wstring (*doubleFuncConstPtr)(const double*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)doubleFuncConstPtr;
    std::wstring (*floatFuncConstPtr)(const float*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)floatFuncConstPtr;
    std::wstring (*voidFuncConstPtr)(const void*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)voidFuncConstPtr;

    std::wstring (*boolFuncPtr)(bool*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)boolFuncPtr;
    std::wstring (*intFuncPtr)(int*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)intFuncPtr;
    std::wstring (*longFuncPtr)(long*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)longFuncPtr;
    std::wstring (*shortFuncPtr)(short*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)shortFuncPtr;
    std::wstring (*signedCharFuncPtr)(signed char*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)signedCharFuncPtr;
    std::wstring (*unsignedIntFuncPtr)(unsigned int*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedIntFuncPtr;
    std::wstring (*unsignedLongFuncPtr)(unsigned long*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedLongFuncPtr;
    std::wstring (*unsignedLongLongFuncPtr)(unsigned long long*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedLongLongFuncPtr;
    std::wstring (*unsignedCharFuncPtr)(unsigned char*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)unsignedCharFuncPtr;
    std::wstring (*charFuncPtr)(char*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)charFuncPtr;
    std::wstring (*wchar_tFuncPtr)(wchar_t*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)wchar_tFuncPtr;
    std::wstring (*doubleFuncPtr)(double*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)doubleFuncPtr;
    std::wstring (*floatFuncPtr)(float*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)floatFuncPtr;
    std::wstring (*voidFuncPtr)(void*) = &Microsoft::VisualStudio::CppUnitTestFramework::ToString;
    (void)voidFuncPtr;
}
