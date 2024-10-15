// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <sstream>
#include <memory>
#include <unordered_map>
#include <functional>
#include <CppUnitTest.h>
#include "..\Logging\DefaultFileLogLocation.h"


using namespace Microsoft::VisualStudio::CppUnitTestFramework;

// Microsoft's built-in unit test logging class has the same name as our logging class.
#define MSLogger Microsoft::VisualStudio::CppUnitTestFramework::Logger

namespace NewRelic { namespace Profiler { namespace Logger { namespace Test
{
    using namespace NewRelic::Profiler::Logger;

    struct SystemCalls : IFileDestinationSystemCalls
    {
        std::function<uint32_t()> GetCurrentProcessHandler;
        std::function<std::shared_ptr<std::wostream>(const std::wstring&, std::ios_base::openmode)> OpenFileHandler;
        std::function<void(std::shared_ptr<std::wostream>)> CloseFileHandler;
        std::function<std::unique_ptr<std::wstring>(const std::wstring&)> TryGetEnvironmentVariableHandler;
        std::unordered_map<std::wstring, std::wstring> environmentVariables;
        std::function<bool(const std::wstring&)> DirectoryExistsHandler;
        std::function<void(const std::wstring&)> DirectoryCreateHandler;
        std::function<std::wstring()> GetCommonAppDataFolderPathHandler;

        virtual uint32_t GetCurrentProcessId()
        {
            if (GetCurrentProcessHandler) return GetCurrentProcessHandler();
            else return 1234;
        }

        virtual std::shared_ptr<std::wostream> OpenFile(const std::wstring& fileName, std::ios_base::openmode openMode)
        {
            if (OpenFileHandler) return OpenFileHandler(fileName, openMode);
            else return std::make_shared<std::wostringstream>();
        }

        virtual void CloseFile(std::shared_ptr<std::wostream> fileAsStream)
        {
            if (CloseFileHandler) CloseFileHandler(fileAsStream);
        }

        virtual std::unique_ptr<std::wstring> TryGetEnvironmentVariable(const std::wstring& variableName)
        {
            if (TryGetEnvironmentVariableHandler)
            {
                return TryGetEnvironmentVariableHandler(variableName);
            }
            
            auto valueString = environmentVariables.find(variableName);
            if (valueString == environmentVariables.end()) return nullptr;

            return std::unique_ptr<std::wstring>(new std::wstring(valueString->second));
        }

        virtual bool DirectoryExists(const std::wstring& logFilePath)
        {
            if (DirectoryExistsHandler) return DirectoryExistsHandler(logFilePath);
            else return false;
        }

        virtual void DirectoryCreate(const std::wstring& logFilePath)
        {
            if (DirectoryCreateHandler) DirectoryCreateHandler(logFilePath);
        }

        virtual std::wstring GetCommonAppDataFolderPath() override
        {
            if (GetCommonAppDataFolderPathHandler) return GetCommonAppDataFolderPathHandler();
            else return L"C:\\Common\\AppData\\FolderPath";
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicHomePath() override
        {
            return TryGetEnvironmentVariable(L"NEW_RELIC_HOME");
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicProfilerLogDirectory() override
        {
            return TryGetEnvironmentVariable(L"NEW_RELIC_PROFILER_LOG_DIRECTORY");
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicLogDirectory() override
        {
            return TryGetEnvironmentVariable(L"NEW_RELIC_LOG_DIRECTORY");
        }

        virtual std::unique_ptr<xstring_t> GetNewRelicLogLevel() override
        {
            return TryGetEnvironmentVariable(L"NEW_RELIC_LOG_LEVEL");
        }
    };

    TEST_CLASS(DefaultFileLogLocationTestt)
    {
    public:
        TEST_METHOD(azure_websites_environment_logs_to_c_home_logfiles_newrelic)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"HOME"] = L"C:\\Foo";
            systemCalls->environmentVariables[L"HOME_EXPANDED"] = L"C:\\DWASFiles\\Sites\\MySite";

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Foo\\LogFiles\\NewRelic\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(azure_websites_environment_logs_to_default_when_no_home_expanded_env_var)
        {
            auto systemCalls = std::make_shared<SystemCalls>();
            systemCalls->environmentVariables[L"HOME"] = L"C:\\Home";

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Common\\AppData\\FolderPath\\New Relic\\.NET Agent\\Logs\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(profiler_environment_variable_with_no_backslash_suffix)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"NEW_RELIC_PROFILER_LOG_DIRECTORY"] = L"C:\\Foo";

            systemCalls->OpenFileHandler = [](std::wstring fileName, std::ios_base::openmode)
            {
                Assert::AreEqual(std::wstring(L"C:\\Foo\\NewRelic.Profiler.1234.log"), fileName);
                return std::make_shared<std::wostringstream>();
            };

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Foo\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(profiler_environment_variable_with_backslash_suffix)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"NEW_RELIC_PROFILER_LOG_DIRECTORY"] = L"C:\\Foo\\";

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Foo\\\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(profiler_log_directory_environment_variable_over_all)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"NEW_RELIC_HOME"] = L"C:\\Foo\\Home";
            systemCalls->environmentVariables[L"NEW_RELIC_PROFILER_LOG_DIRECTORY"] = L"C:\\Foo\\Profiler";
            systemCalls->environmentVariables[L"NEW_RELIC_LOG_DIRECTORY"] = L"C:\\Foo\\General";

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Foo\\Profiler\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(general_log_directory_environment_variable_over_home)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"NEW_RELIC_HOME"] = L"C:\\Foo\\Home";
            systemCalls->environmentVariables[L"NEW_RELIC_LOG_DIRECTORY"] = L"C:\\Foo\\General";

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Foo\\General\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(common_app_data_fallback)
        {
            auto systemCalls = std::make_shared<SystemCalls>();
            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Common\\AppData\\FolderPath\\New Relic\\.NET Agent\\Logs\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(profiler_log_variable_trumps_all)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"ALLUSERSPROFILE"] = L"D:\\Foo\\ProgramData\\Bar";
            systemCalls->environmentVariables[L"HOME"] = L"C:\\DWASFiles\\Sites\\MySite\\";
            systemCalls->environmentVariables[L"NEW_RELIC_PROFILER_LOG_DIRECTORY"] = L"C:\\Foo";;

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Foo\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(azure_variable_trumps_allusers)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"ALLUSERSPROFILE"] = L"D:\\Foo\\ProgramData\\Bar";
            systemCalls->environmentVariables[L"HOME_EXPANDED"] = L"C:\\DWASFiles\\Sites\\MySite";
            systemCalls->environmentVariables[L"HOME"] = L"C:\\Home";

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Home\\LogFiles\\NewRelic\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(azure_variable_d_drive)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"ALLUSERSPROFILE"] = L"C:\\Foo\\ProgramData\\Bar";
            systemCalls->environmentVariables[L"HOME_EXPANDED"] = L"D:\\DWASFiles\\Sites\\MySite";
            systemCalls->environmentVariables[L"HOME"] = L"C:\\Home";

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Home\\LogFiles\\NewRelic\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(when_directory_exists_do_not_create_directory)
        {
            auto systemCalls = std::make_shared<SystemCalls>();
            systemCalls->environmentVariables[L"HOME"] = L"C:\\DWASFiles\\Sites\\MySite\\";

            systemCalls->DirectoryExistsHandler = [](const std::wstring&)
            {
                return true;
            };

            systemCalls->DirectoryCreateHandler = [](const std::wstring&)
            {
                Assert::Fail(L"When the directory exists then the DirectoryCreate function should not be called");
            };

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            fileName;
        }

        TEST_METHOD(when_directory_does_not_exist_create_directory)
        {
            auto systemCalls = std::make_shared<SystemCalls>();
            bool createDirCalled = false;

            systemCalls->environmentVariables[L"HOME"] = L"C:\\DWASFiles\\Sites\\MySite\\";

            systemCalls->DirectoryExistsHandler = [](const std::wstring&)
            {
                return false;
            };

            systemCalls->DirectoryCreateHandler = [&](const std::wstring&)
            {
                createDirCalled = true;
            };

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            fileName;

            Assert::IsTrue(createDirCalled);
        }

        TEST_METHOD(newrelic_home_environment_variable)
        {
            auto systemCalls = std::make_shared<SystemCalls>();
            systemCalls->environmentVariables[L"NEW_RELIC_HOME"] = L"C:\\Foo";

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Foo\\Logs\\NewRelic.Profiler.1234.log"), fileName);
        }

        TEST_METHOD(newrelic_home_trumps_common_app_data)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"NEW_RELIC_HOME"] = L"C:\\Foo";

            auto fileName = DefaultFileLogLocation(systemCalls).GetPathAndFileName();
            Assert::AreEqual(std::wstring(L"C:\\Foo\\Logs\\NewRelic.Profiler.1234.log"), fileName);
        }

    };

#ifdef _NEVER_
    TEST_CLASS(FileDestinationTest)
    {
    public:
        TEST_METHOD(azure_websites_environment_logs_to_c_home_logfiles_newrelic)
        {
            auto systemCalls = std::make_shared<SystemCalls>();
            
            systemCalls->environmentVariables[L"HOME"] = L"C:\\Foo";
            systemCalls->environmentVariables[L"HOME_EXPANDED"] = L"C:\\DWASFiles\\Sites\\MySite";

            systemCalls->OpenFileHandler = [](std::wstring fileName, std::ios_base::openmode)
            {
                Assert::AreEqual(std::wstring(L"C:\\Foo\\LogFiles\\NewRelic\\NewRelic.Profiler.1234.log"), fileName);
                return std::make_shared<std::wostringstream>();
            };

            FileDestination FileDestination(systemCalls);
        }

        TEST_METHOD(azure_websites_environment_logs_to_default_when_no_home_expanded_env_var)
        {
            auto systemCalls = std::make_shared<SystemCalls>();
            systemCalls->environmentVariables[L"HOME"] = L"C:\\Home";
            
            systemCalls->OpenFileHandler = [](std::wstring fileName, std::ios_base::openmode)
            {
                Assert::AreEqual(std::wstring(L"C:\\Common\\AppData\\FolderPath\\New Relic\\.NET Agent\\Logs\\NewRelic.Profiler.1234.log"), fileName);
                return std::make_shared<std::wostringstream>();
            };

            FileDestination FileDestination(systemCalls);
        }

        TEST_METHOD(profiler_environment_variable_with_no_backslash_suffix)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"NEW_RELIC_PROFILER_LOG_DIRECTORY"] = L"C:\\Foo";

            systemCalls->OpenFileHandler = [](std::wstring fileName, std::ios_base::openmode)
            {
                Assert::AreEqual(std::wstring(L"C:\\Foo\\NewRelic.Profiler.1234.log"), fileName);
                return std::make_shared<std::wostringstream>();
            };

            FileDestination FileDestination(systemCalls);
        }

        TEST_METHOD(profiler_environment_variable_with_backslash_suffix)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"NEW_RELIC_PROFILER_LOG_DIRECTORY"] = L"C:\\Foo\\";

            systemCalls->OpenFileHandler = [](std::wstring fileName, std::ios_base::openmode)
            {
                Assert::AreEqual(std::wstring(L"C:\\Foo\\\\NewRelic.Profiler.1234.log"), fileName);
                return std::make_shared<std::wostringstream>();
            };

            FileDestination FileDestination(systemCalls);
        }

        TEST_METHOD(common_app_data_fallback)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->OpenFileHandler = [](std::wstring fileName, std::ios_base::openmode)
            {
                Assert::AreEqual(std::wstring(L"C:\\Common\\AppData\\FolderPath\\New Relic\\.NET Agent\\Logs\\NewRelic.Profiler.1234.log"), fileName);
                return std::make_shared<std::wostringstream>();
            };

            FileDestination FileDestination(systemCalls);
        }

        TEST_METHOD(profiler_log_variable_trumps_all)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"ALLUSERSPROFILE"] = L"D:\\Foo\\ProgramData\\Bar";
            systemCalls->environmentVariables[L"HOME"] = L"C:\\DWASFiles\\Sites\\MySite\\";
            systemCalls->environmentVariables[L"NEW_RELIC_PROFILER_LOG_DIRECTORY"] = L"C:\\Foo";;

            systemCalls->OpenFileHandler = [](std::wstring fileName, std::ios_base::openmode)
            {
                Assert::AreEqual(std::wstring(L"C:\\Foo\\NewRelic.Profiler.1234.log"), fileName);
                return std::make_shared<std::wostringstream>();
            };

            FileDestination FileDestination(systemCalls);
        }

        TEST_METHOD(azure_variable_trumps_allusers)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"ALLUSERSPROFILE"] = L"D:\\Foo\\ProgramData\\Bar";
            systemCalls->environmentVariables[L"HOME_EXPANDED"] = L"C:\\DWASFiles\\Sites\\MySite";
            systemCalls->environmentVariables[L"HOME"] = L"C:\\Home";

            systemCalls->OpenFileHandler = [](std::wstring fileName, std::ios_base::openmode)
            {
                Assert::AreEqual(std::wstring(L"C:\\Home\\LogFiles\\NewRelic\\NewRelic.Profiler.1234.log"), fileName);
                return std::make_shared<std::wostringstream>();
            };

            FileDestination FileDestination(systemCalls);
        }

        TEST_METHOD(when_directory_exists_do_not_create_directory)
        {
            auto systemCalls = std::make_shared<SystemCalls>();
            systemCalls->environmentVariables[L"HOME"] = L"C:\\DWASFiles\\Sites\\MySite\\";

            systemCalls->DirectoryExistsHandler = [](const std::wstring&)
            {
                return true;
            };

            systemCalls->DirectoryCreateHandler = [](const std::wstring&)
            {
                Assert::Fail(L"When the directory exists then the DirectoryCreate function should not be called");
            };

            FileDestination FileDestination(systemCalls);
        }

        TEST_METHOD(when_directory_does_not_exist_create_directory)
        {
            auto systemCalls = std::make_shared<SystemCalls>();
            bool createDirCalled = false;

            systemCalls->environmentVariables[L"HOME"] = L"C:\\DWASFiles\\Sites\\MySite\\";

            systemCalls->DirectoryExistsHandler = [](const std::wstring&)
            {
                return false;
            };

            systemCalls->DirectoryCreateHandler = [&](const std::wstring&)
            {
                createDirCalled = true;
            };

            FileDestination FileDestination(systemCalls);

            Assert::IsTrue(createDirCalled);
        }

        TEST_METHOD(newrelic_home_environment_variable)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"NEW_RELIC_HOME"] = L"C:\\Foo";

            systemCalls->OpenFileHandler = [](std::wstring fileName, std::ios_base::openmode)
            {
                Assert::AreEqual(std::wstring(L"C:\\Foo\\Logs\\NewRelic.Profiler.1234.log"), fileName);
                return std::make_shared<std::wostringstream>();
            };

            FileDestination FileDestination(systemCalls);
        }

        TEST_METHOD(newrelic_home_trumps_common_app_data)
        {
            auto systemCalls = std::make_shared<SystemCalls>();

            systemCalls->environmentVariables[L"NEW_RELIC_HOME"] = L"C:\\Foo";

            systemCalls->OpenFileHandler = [](std::wstring fileName, std::ios_base::openmode)
            {
                Assert::AreEqual(std::wstring(L"C:\\Foo\\Logs\\NewRelic.Profiler.1234.log"), fileName);
                return std::make_shared<std::wostringstream>();
            };

            FileDestination FileDestination(systemCalls);
        }
    };
#endif
}}}}
