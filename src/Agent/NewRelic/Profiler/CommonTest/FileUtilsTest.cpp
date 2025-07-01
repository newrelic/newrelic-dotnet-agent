// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include "stdafx.h"

#include <codecvt>
#include <fstream>

#include "CppUnitTest.h"
#include "../Common/Strings.h"
#include "../Common/FileUtils.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic {
    namespace Profiler {
        namespace Common
        {
            TEST_CLASS(FileUtilsTest)
            {
            public:
                TEST_METHOD(ReadFile_WithJapaneseCharacters)
                {
                    // create a temporary file with Japanese characters
                    std::wstring tempFilePath = L"temp_japanese.txt";
                    std::wstring japaneseContent = L"こんにちは、世界！"; // "Hello, World!" in Japanese
                    {
                        std::wofstream outFile(tempFilePath, std::ios::binary);
                        outFile.imbue(std::locale(outFile.getloc(), new std::codecvt_utf8_utf16<wchar_t, 0x10ffff, std::generate_header>()));
                        outFile << japaneseContent;
                    }
                    // read the file using ReadFile function
                    std::wstring readContent = ReadFile(tempFilePath);
                    // verify the content matches
                    Assert::AreEqual(japaneseContent, readContent);
                }

                TEST_METHOD(ReadFile_WithBOM)
                {
                    // create a temporary file with BOM
                    std::wstring tempFilePath = L"temp_bom.txt";
                    std::wstring contentWithBOM = L"This file starts with a BOM and has some Japanese characters. こんにちは、世界！";
                    {
                        std::wofstream outFile(tempFilePath, std::ios::binary);
                        outFile.imbue(std::locale(outFile.getloc(), new std::codecvt_utf8_utf16<wchar_t, 0x10ffff, std::generate_header>()));
                        outFile << contentWithBOM;
                    }
                    // read the file using ReadFile function
                    std::wstring actual = ReadFile(tempFilePath);

                    // verify the content matches (BOM should be handled)
                    auto expected = contentWithBOM;
                    Assert::AreEqual(expected, actual);
                }

                TEST_METHOD(ReadFile_EnglishCharactersOnly_NoBOM)
                {
                    // create a temporary file with English characters only
                    std::wstring tempFilePath = L"temp_english.txt";
                    std::wstring englishContent = L"This is a test file with English characters only.";
                    {
                        std::wofstream outFile(tempFilePath, std::ios::binary);
                        outFile.imbue(std::locale(outFile.getloc(), new std::codecvt_utf8_utf16<wchar_t, 0x10ffff, std::consume_header>()));
                        outFile << englishContent;
                    }
                    // read the file using ReadFile function
                    std::wstring readContent = ReadFile(tempFilePath);
                    // verify the content matches
                    Assert::AreEqual(englishContent, readContent);
                }

                TEST_METHOD(ReadFile_EnglishCharactersOnly_WithBOM)
                {
                    // create a temporary file with English characters only and BOM
                    std::wstring tempFilePath = L"temp_english_bom.txt";
                    std::wstring englishContentWithBOM = L"This is a test file with English characters only and BOM.";
                    {
                        std::wofstream outFile(tempFilePath, std::ios::binary);
                        outFile.imbue(std::locale(outFile.getloc(), new std::codecvt_utf8_utf16<wchar_t, 0x10ffff, std::generate_header>()));
                        outFile << englishContentWithBOM;
                    }
                    // read the file using ReadFile function
                    std::wstring actual = ReadFile(tempFilePath);

                    // verify the content matches (BOM should be handled)
                    auto expected = englishContentWithBOM;
                    Assert::AreEqual(expected, actual); 
                }

                TEST_METHOD(ReadFile_JapaneseFileName_JapaneseAndEnglishCharacters_NoBOM)
                {
                    // create a temporary file with Japanese characters in the name and content
                    std::wstring tempFilePath = L"テストファイル.txt"; // "Test file" in Japanese
                    std::wstring content = L"This file has a Japanese filename and contains both English and Japanese characters. こんにちは、世界！";
                    {
                        std::wofstream outFile(tempFilePath, std::ios::binary);
                        outFile.imbue(std::locale(outFile.getloc(), new std::codecvt_utf8_utf16<wchar_t, 0x10ffff, std::consume_header>()));
                        outFile << content;
                    }
                    // read the file using ReadFile function
                    std::wstring readContent = ReadFile(tempFilePath);
                    // verify the content matches
                    Assert::AreEqual(content, readContent);
                }

                TEST_METHOD(ReadFile_JapaneseFileName_JapaneseAndEnglishCharacters_WithBOM)
                {
                    // create a temporary file with Japanese characters in the name and content with BOM
                    std::wstring tempFilePath = L"テストファイル_bom.txt"; // "Test file" in Japanese
                    std::wstring contentWithBOM = L"This file has a Japanese filename and contains both English and Japanese characters with BOM. こんにちは、世界！";
                    {
                        std::wofstream outFile(tempFilePath, std::ios::binary);
                        outFile.imbue(std::locale(outFile.getloc(), new std::codecvt_utf8_utf16<wchar_t, 0x10ffff, std::generate_header>()));
                        outFile << contentWithBOM;
                    }
                    // read the file using ReadFile function
                    std::wstring actual = ReadFile(tempFilePath);
                    // verify the content matches (BOM should be handled)
                    auto expected = contentWithBOM;
                    Assert::AreEqual(expected, actual);
                }

                TEST_METHOD(ReadFile_FileDoesNotExist_ThrowsException)
                {
                    std::wstring nonExistentFilePath = L"file_does_not_exist.txt";
                    auto func = [&]() { ReadFile(nonExistentFilePath); };
                    Assert::ExpectException<std::exception>(func);
                }

            };
        }
    }
}
