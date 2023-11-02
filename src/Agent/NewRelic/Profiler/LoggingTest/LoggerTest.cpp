// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <CppUnitTest.h>
#include "UnreferencedFunctions.h"
#define LOGGER_DEFINE_STDLOG
#define LOGGER_STDLOG_USE_MEMORYLOGGER
#include "../Logging/Logger.h"
#include "LoggingTestTemplates.h"
#include "../Common/Strings.h"
#include "../RapidXML/rapidxml.hpp"
#include "../MethodRewriter/Exceptions.h"
#include "../MethodRewriter/IFunction.h"
#include "../MethodRewriterTest/MockFunction.h"
#include  "../Configuration/InstrumentationPoint.h"
#include "../Profiler/OpCodes.h"
#include <regex>
#include <list>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

#define TIMESTAMP_REGEX LR"X( \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d )X"
#define ERROR_PREFIX_REGEX LR"X(\[Error\])X" TIMESTAMP_REGEX
#define  WARN_PREFIX_REGEX LR"X(\[Warn \])X" TIMESTAMP_REGEX
#define TRACE_PREFIX_REGEX LR"X(\[Trace\])X" TIMESTAMP_REGEX
#define DEBUG_PREFIX_REGEX LR"X(\[Debug\])X" TIMESTAMP_REGEX
#define  INFO_PREFIX_REGEX LR"X(\[Info \])X" TIMESTAMP_REGEX

// Microsoft's built-in unit test logging class has the same name as our logging class.
#define MSLogger Microsoft::VisualStudio::CppUnitTestFramework::Logger

namespace NewRelic {
    namespace Profiler {
        namespace Logger {
            
            namespace Test
            {
                using namespace NewRelic::Profiler::Logger;

                TEST_CLASS(LoggerTest)
                {
                    typedef std::list<std::wstring> MessageList;

                public:
                    TEST_METHOD_INITIALIZE(MethodSetup)
                    {
                        StdLog.SetLevel(Level::LEVEL_INFO);
                        StdLog.SetConsoleLogging(false);
                        StdLog.SetEnabled(true);
                        StdLog.SetInitalized();
                    }

                    TEST_METHOD(logger_verify_string_representation_of_levels)
                    {
                        Assert::AreEqual(L"Trace", GetLevelString(Level::LEVEL_TRACE));
                        Assert::AreEqual(L"Info ", GetLevelString(Level::LEVEL_INFO));
                        Assert::AreEqual(L"Debug", GetLevelString(Level::LEVEL_DEBUG));
                        Assert::AreEqual(L"Warn ", GetLevelString(Level::LEVEL_WARN));
                        Assert::AreEqual(L"Error", GetLevelString(Level::LEVEL_ERROR));
                    }

                    TEST_METHOD(logger_verify_Info_is_default_Level)
                    {
                        Assert::AreEqual(Level::LEVEL_INFO, StdLog.GetLevel());
                    }

                    TEST_METHOD(logger_verify_one_info_message_is_logged_at_info_level)
                    {
                        ResetStdLog();
                        LogInfo(L"This is an info level logging statement");
                        AssertMessageCount(1);
                    }

                    TEST_METHOD(logger_verify_five_info_messages_are_logged_at_info_level)
                    {
                        ResetStdLog();
                        LogInfo(L"message number one");
                        LogInfo(L"message number two");
                        LogInfo(L"message number three");
                        LogInfo(L"message number four");
                        LogInfo(L"message number five");
                        AssertMessageCount(5);
                    }

                    TEST_METHOD(logger_verify_one_info_message_logged_change_to_debug_then_log_two_debug_messages)
                    {
                        ResetStdLog();
                        LogInfo(L"info message here");
                        StdLog.SetLevel(Level::LEVEL_DEBUG);
                        LogDebug(L"debug message one");
                        LogDebug(L"debug message two");
                        AssertMessageCount(3);
                    }

                    TEST_METHOD(logger_verify_exact_messages_are_actually_logged_for_info)
                    {
                        ResetStdLog();
                        MessageList messages;
                        messages.push_back(L"this is information level message number one");
                        messages.push_back(L"this is information level message number two");
                        messages.push_back(L"this is information level message number three");
                        messages.push_back(L"this is information level message number four");
                        messages.push_back(L"this is information level message number five");
                        AssertMessagesActuallyLogged(Level::LEVEL_INFO, messages);
                    }

                    TEST_METHOD(logger_verify_debug_message_not_logged_at_info_level)
                    {
                        ResetStdLog();
                        LogDebug(L"This is a debug level logging statement");
                        AssertMessageCount(0);
                    }

                    TEST_METHOD(logger_verify_trace_message_not_logged_at_info_level)
                    {
                        ResetStdLog();
                        LogTrace(L"This is a trace level logging statement");
                        AssertMessageCount(0);
                    }

                    TEST_METHOD(logger_verify_warning_message_logged_at_info_level)
                    {
                        ResetStdLog();
                        LogWarn(L"This is a warn level logging statement.");
                        AssertMessageCount(1);
                    }

                    TEST_METHOD(logger_verify_error_message_logged_at_info_level)
                    {
                        ResetStdLog();
                        LogError(L"This is an error level logging statement.");
                        AssertMessageCount(1);
                    }

                    TEST_METHOD(logger_verify_can_change_level_to_debug)
                    {
                        AssertLevelChange(Level::LEVEL_DEBUG);
                    }

                    TEST_METHOD(logger_verify_can_change_level_to_trace)
                    {
                        AssertLevelChange(Level::LEVEL_TRACE);
                    }

                    TEST_METHOD(logger_verify_prior_level_returned_from_set_level)
                    {
                        Level priorLevel = StdLog.GetLevel();
                        StdLog.SetLevel(Level::LEVEL_DEBUG);
                        Assert::AreEqual(Level::LEVEL_INFO, priorLevel);
                    }

                    TEST_METHOD(logger_test_stream_logger)
                    {
                        ResetStdLog();
                        StdLog.ostr() << L"Float: " << 1.234 << L" Int: " << 1234 << L" Hex: " << std::hex << 1234;
                        Assert::AreEqual(std::wstring(L"Float: 1.234 Int: 1234 Hex: 4d2"), GetMessages().front());
                    }


                    TEST_METHOD(logger_test_log_scope_enter_leave)
                    {
                        ResetStdLog();
                        {
                            StdLog.SetLevel(Level::LEVEL_TRACE);

                            LogScopeEnterLeave(Level::LEVEL_TRACE);
                        }
                        //  012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789
                        //L"[Trace] 2017-09-21 16:01:14 Enter: NewRelic::Profiler::Logger::Test::LoggerTest::logger_test_log_scope_enter_leave on c:\\work\\newrelic.profiler\\loggingtest\\loggertest.cpp(167)"
                        auto msgs = GetMessages();
                        constexpr size_t expected_num_msgs = 2;
                        Assert::AreEqual(expected_num_msgs, msgs.size());
                        auto itr = std::begin(msgs);
                        Assert::IsTrue(std::regex_match(*itr++, std::wregex(TRACE_PREFIX_REGEX L"Enter\\: NewRelic\\:\\:Profiler\\:\\:Logger\\:\\:Test\\:\\:LoggerTest\\:\\:logger_test_log_scope_enter_leave on .+\\\\LoggerTest.cpp\\(\\d+\\)")));
                        Assert::IsTrue(std::regex_match(*itr++, std::wregex(TRACE_PREFIX_REGEX L"Leave\\: NewRelic\\:\\:Profiler\\:\\:Logger\\:\\:Test\\:\\:LoggerTest\\:\\:logger_test_log_scope_enter_leave on .+\\\\LoggerTest.cpp\\(\\d+\\)")));
                    }

                    TEST_METHOD(logger_test_example_uses_from_solution)
                    {
                        StdLog.SetLevel(Level::LEVEL_TRACE);
                        ResetStdLog();

                        {
                            uint32_t dataToCompress = 0xdeadbeef;
                            LogError(L"Data too large to compress. ", std::hex, std::showbase, dataToCompress, std::resetiosflags(std::ios_base::basefield | std::ios_base::showbase));
                            AssertRegex(ERROR_PREFIX_REGEX L"Data too large to compress\\. 0xdeadbeef\n");
                        }
                        {
                            //wchar_t NBTS
                            LogTrace(L"This is a small message.");
                            AssertRegex(TRACE_PREFIX_REGEX L"This is a small message\\.\n");

                            LogInfo(L"This is a small message.");
                            AssertRegex(INFO_PREFIX_REGEX L"This is a small message\\.\n");

                            LogDebug(L"This is a small message.");
                            AssertRegex(DEBUG_PREFIX_REGEX L"This is a small message\\.\n");

                            LogWarn(L"This is a small message.");
                            AssertRegex(WARN_PREFIX_REGEX L"This is a small message\\.\n");

                            LogError(L"This is a small message.");
                            AssertRegex(ERROR_PREFIX_REGEX L"This is a small message\\.\n");

                            //char NBTS
                            LogTrace("This is a small message.");
                            AssertRegex(TRACE_PREFIX_REGEX L"This is a small message\\.\n");

                            LogInfo("This is a small message.");
                            AssertRegex(INFO_PREFIX_REGEX L"This is a small message\\.\n");

                            LogDebug("This is a small message.");
                            AssertRegex(DEBUG_PREFIX_REGEX L"This is a small message\\.\n");

                            LogWarn("This is a small message.");
                            AssertRegex(WARN_PREFIX_REGEX L"This is a small message\\.\n");

                            LogError("This is a small message.");
                            AssertRegex(ERROR_PREFIX_REGEX L"This is a small message\\.\n");

                            //xchar_t NBTS
                            LogTrace(_X("This is a small message."));
                            AssertRegex(TRACE_PREFIX_REGEX L"This is a small message\\.\n");

                            LogInfo(_X("This is a small message."));
                            AssertRegex(INFO_PREFIX_REGEX L"This is a small message\\.\n");

                            LogDebug(_X("This is a small message."));
                            AssertRegex(DEBUG_PREFIX_REGEX L"This is a small message\\.\n");

                            LogWarn(_X("This is a small message."));
                            AssertRegex(WARN_PREFIX_REGEX L"This is a small message\\.\n");

                            LogError(_X("This is a small message."));
                            AssertRegex(ERROR_PREFIX_REGEX L"This is a small message\\.\n");
                        }
                        {
                            uint8_t token = 0x27;
                            LogError("Unhandled token encountered while parsing the type.  Token: ", std::hex, std::showbase, token, std::resetiosflags(std::ios_base::basefield | std::ios_base::showbase));
                            AssertRegex(ERROR_PREFIX_REGEX L"Unhandled token encountered while parsing the type\\.  Token: 0x27\n");
                        }
                        {
                            using ModuleID = UINT_PTR;
                            ModuleID moduleId = 0xdeadbeef;
                            class IModule
                            {
                            public:
                                std::wstring GetModuleName() { return std::wstring(L"ModuleName"); }
                            };
                            typedef std::shared_ptr<IModule> IModulePtr;
                            const IModulePtr& module = std::make_shared<IModule>();
                            LogDebug(L"Injecting references to helper methods into ", module->GetModuleName());
                            AssertRegex(DEBUG_PREFIX_REGEX L"Injecting references to helper methods into ModuleName\n");

                            LogTrace("Module Injection Finished. ", moduleId, " : ", module->GetModuleName());
                            AssertRegex(TRACE_PREFIX_REGEX L"Module Injection Finished\\. 3735928559 : ModuleName\n");
                        }

                        {
                            const std::wstring& signature(L"Signature");
                            LogError(L"Failed to tokenize method signature: ", signature);
                            AssertRegex(ERROR_PREFIX_REGEX L"Failed to tokenize method signature: Signature\n");
                        }
                        {

                            wchar_t* source = L"<xml name:=foo>";
                            const rapidxml::parse_error exception("what string", source + 9);
                            LogError(L"Exception thrown while attempting to parse configuration file. ", exception.what(), L" at ", exception.where<wchar_t>());
                            AssertRegex(ERROR_PREFIX_REGEX L"Exception thrown while attempting to parse configuration file\\. what string at :=foo>\n");
                        }
                        {
                            NewRelic::Profiler::MethodRewriter::MessageException exception(_X("xstring exception _message"));
                            LogError(exception._message);
                            AssertRegex(ERROR_PREFIX_REGEX L"xstring exception _message\n");
                            LogError("Exception details: ", exception._message);
                            AssertRegex(ERROR_PREFIX_REGEX L"Exception details: xstring exception _message\n");
                        }
                        {
                            const xstring_t& processName(_X("processname"));
                            LogInfo(L"Enabling instrumentation for this process due to existence of NewRelic.AgentEnabled=true in ", processName, L".config.");
                            AssertRegex(INFO_PREFIX_REGEX L"Enabling instrumentation for this process due to existence of NewRelic\\.AgentEnabled=true in processname\\.config\\.\n");
                            LogInfo(L"Disabling instrumentation for this process due to the existence of NewRelic.AgentEnabled in ", processName, L".config which is set to a value other than 'true'.");
                            AssertRegex(INFO_PREFIX_REGEX L"Disabling instrumentation for this process due to the existence of NewRelic\\.AgentEnabled in processname\\.config which is set to a value other than 'true'\\.\n");
                            LogInfo(L"Enabling instrumentation for this process (", processName, ") due to existence of application node in newrelic.config.");
                            AssertRegex(INFO_PREFIX_REGEX L"Enabling instrumentation for this process \\(processname\\) due to existence of application node in newrelic\\.config\\.\n");
                            LogInfo(L"Enabling instrumentation for this process (", processName, L") due to it being in a predefined set of processes to be instrumented.");
                            AssertRegex(INFO_PREFIX_REGEX L"Enabling instrumentation for this process \\(processname\\) due to it being in a predefined set of processes to be instrumented\\.\n");
                            LogInfo(L"This process (", processName, ") is not configured to be instrumented.");
                            AssertRegex(INFO_PREFIX_REGEX L"This process \\(processname\\) is not configured to be instrumented\\.\n");
                        }
                        {
                            const xstring_t& appPoolId(_X("appPoolId"));

                            LogInfo(_X("This application pool (") + appPoolId + _X(") is explicitly configured to NOT be instrumented."));
                            AssertRegex(INFO_PREFIX_REGEX L"This application pool \\(appPoolId\\) is explicitly configured to NOT be instrumented\\.\n");
                        }
                        {
                            using namespace NewRelic::Profiler::MethodRewriter;
                            using namespace NewRelic::Profiler::MethodRewriter::Test;
                            IFunctionPtr _function = std::make_shared<MockFunction>();
                            IFunctionPtr function = std::make_shared<MockFunction>();

                            LogTrace(_function->ToString(), L": Generating API bytecode instrumentation.");
                            AssertRegex(TRACE_PREFIX_REGEX LR"X(\[MyAssembly\]MyNamespace\.MyClass\.MyMethod)X" L": Generating API bytecode instrumentation\\.\n");

                            LogError(L"Unexpected instruction in method ", _function->ToString());
                            AssertRegex(ERROR_PREFIX_REGEX LR"X(Unexpected instruction in method \[MyAssembly\]MyNamespace\.MyClass\.MyMethod)X" L"\n");

                            LogTrace(_function->ToString() + _X(": Generating locals for default instrumentation."));
                            AssertRegex(TRACE_PREFIX_REGEX LR"X(\[MyAssembly\]MyNamespace\.MyClass\.MyMethod)X" L": Generating locals for default instrumentation\\.\n");
                        }
                        {
                            uint32_t localCount = 0xbabebabe;
                            LogError("Extracted local count (", std::hex, std::showbase, localCount, ") is too big to add locals to (>= 0xfffe)", std::resetiosflags(std::ios_base::basefield | std::ios_base::showbase));
                            AssertRegex(ERROR_PREFIX_REGEX LR"X(Extracted local count \(0xbabebabe\) is too big to add locals to \(>= 0xfffe\))X" L"\n");
                        }
                        {
                            std::wstring instruction(L"whipit.good");
                            LogError(L"Encountered unsupported instruction while attempting to generate byte code. Instruction: ", instruction);
                            auto str = StdLog.get_dest().str();
                            AssertRegex(ERROR_PREFIX_REGEX LR"X(Encountered unsupported instruction while attempting to generate byte code. Instruction: whipit.good)X" L"\n");
                        }
                    }
                    TEST_METHOD(logger_test_console)
                    {
                        StdLog.SetLevel(Level::LEVEL_TRACE);
                        StdLog.SetConsoleLogging(true);
                        ResetStdLog();

                        LogInfo("blah blah blah");
                        LogWarn("A warning");
                        LogError("An error");

                        AssertMessageCount(0);
                    }

                    TEST_METHOD(logger_test_console_restrictions)
                    {
                        ResetStdLog();
                        StdLog.SetLevel(Level::LEVEL_TRACE);
                        StdLog.SetConsoleLogging(true);
                        Assert::AreEqual(StdLog.GetLevel(), Level::LEVEL_INFO);

                        ResetStdLog();
                        StdLog.SetConsoleLogging(false);
                        StdLog.SetLevel(Level::LEVEL_TRACE);
                        Assert::AreEqual(StdLog.GetLevel(), Level::LEVEL_TRACE);

                        ResetStdLog();
                        StdLog.SetLevel(Level::LEVEL_WARN);
                        StdLog.SetConsoleLogging(true);
                        Assert::AreEqual(StdLog.GetLevel(), Level::LEVEL_WARN);

                        // Disabling console logging should revert to the original log level
                        StdLog.SetConsoleLogging(true);
                        StdLog.SetLevel(Level::LEVEL_DEBUG);
                        Assert::AreEqual(StdLog.GetLevel(), Level::LEVEL_INFO);
                        StdLog.SetConsoleLogging(false);
                        Assert::AreEqual(StdLog.GetLevel(), Level::LEVEL_DEBUG);
                    }

                    TEST_METHOD(logger_test_disabled)
                    {
                        StdLog.SetLevel(Level::LEVEL_TRACE);
                        StdLog.SetEnabled(false);
                        ResetStdLog();

                        LogInfo("blah blah blah");
                        LogWarn("A warning");
                        LogError("An error");

                        AssertMessageCount(0);
                    }



                    //------------------------------------------------------------
                    //    End of Tests
                    //------------------------------------------------------------

                    //------------------------------------------------------------
                    //    Beginning of Helpers
                    //------------------------------------------------------------

                private:
                    void AssertRegex(const wchar_t* const re)
                    {
                        Assert::IsTrue(std::regex_match(StdLog.get_dest().str(), std::wregex(re)));
                        ResetStdLog();
                    }

                    void ResetStdLog()
                    {
                        StdLog.get_dest().str(L"");
                    }

                    MessageList GetMessages()
                    {
                        std::wstringstream sx(StdLog.get_dest().str());
                        MessageList messages;
                        std::wstring line;
                        while (std::getline(sx, line))
                            messages.emplace_back(std::move(line));

                        return messages;
                    }

                    void AssertMessageCount(size_t expected)
                    {
                        auto messages = GetMessages();
                        Assert::AreEqual(expected, messages.size());
                    }

                    void AssertLevelChange(Level expected)
                    {
                        StdLog.SetLevel(expected);
                        Assert::AreEqual(expected, StdLog.GetLevel());
                    }

                    void AssertMessagesActuallyLogged(Level level, const MessageList& messages)
                    {
                        switch (level)
                        {
                        case Level::LEVEL_DEBUG:
                            for (auto message : messages)
                            {
                                LogDebug(message);
                            }
                            break;

                        case Level::LEVEL_ERROR:
                            for (auto message : messages)
                            {
                                LogError(message);
                            }
                            break;

                        case Level::LEVEL_TRACE:
                            for (auto message : messages)
                            {
                                LogTrace(message);
                            }
                            break;

                        case Level::LEVEL_INFO:
                            for (auto message : messages)
                            {
                                LogInfo(message);
                            }
                            break;

                        case Level::LEVEL_WARN:
                            for (auto message : messages)
                            {
                                LogWarn(message);
                            }
                            break;
                        }

                        // Retrieve logged messages and verify contents. Order doesn't matter.
                        auto loggedMessages = GetMessages();
                        //wchar_t* testing = loggedMessages[0];

                        Assert::AreEqual(messages.size(), loggedMessages.size());

                        bool found = false;
                        for (auto logMessage : loggedMessages)
                        {
                            found = false;

                            for (auto original : messages)
                            {
                                if (Strings::Contains(logMessage, original))
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                Assert::Fail(L"Message sent to log was not found in logged messages");
                                break;
                            }
                        }
                    }
                };
            }
        }
    }
}
