// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#include <functional>
#include "stdafx.h"
#define LOGGER_DEFINE_STDLOG
#include "CppUnitTest.h"
#include "ConfigurationTestTemplates.h"
#include "../Configuration/Configuration.h"
#include "../LoggingTest/DefaultFileLogLocationTest.cpp"

#include "../Profiler/Win32Helpers.h"

#include <corerror.h>
using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace NewRelic { namespace Profiler { namespace Configuration { namespace Test
{
    TEST_CLASS(ConfigurationTest)
    {
    public:
        TEST_METHOD(agent_enabled)
        {
            Configuration configuration(true);
            Assert::IsTrue(configuration.IsAgentEnabled());
        }

        TEST_METHOD(agent_disabled)
        {
            Configuration configuration(false);
            Assert::IsFalse(configuration.IsAgentEnabled());
        }

        TEST_METHOD(log_level_info)
        {
            Configuration configuration(true, Logger::Level::LEVEL_INFO);
            Assert::AreEqual(Logger::Level::LEVEL_INFO, configuration.GetLoggingLevel());
        }

        TEST_METHOD(log_level_debug)
        {
            Configuration configuration(true, Logger::Level::LEVEL_DEBUG);
            Assert::AreEqual(Logger::Level::LEVEL_DEBUG, configuration.GetLoggingLevel());
        }

        TEST_METHOD(should_instrument_w3wp)
        {
            Configuration configuration(true);
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"foo", L"", false));
        }

        TEST_METHOD(should_instrument_WcfSvcHost)
        {
            Configuration configuration(true);
            Assert::IsTrue(configuration.ShouldInstrument(L"WcfSvcHost.exe", L"", L"foo", L"", false));
        }

        TEST_METHOD(should_not_instrument_SMSvcHost)
        {
            Configuration configuration(true);
            Assert::IsFalse(configuration.ShouldInstrument(L"SMSvcHost.exe", L"", L"foo", L"", false));
        }

        TEST_METHOD(should_not_instrument_if_disabled)
        {
            Configuration configuration(false);
            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"foo", L"", false));
        }

        TEST_METHOD(should_not_instrument_process)
        {
            Configuration configuration(true);
            Assert::IsFalse(configuration.ShouldInstrument(L"foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(should_not_instrument_azure_function_app_pool_id_in_commandline)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <log level=\"deBug\"/>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            systemCalls->environmentVariables[L"FUNCTIONS_WORKER_RUNTIME"] = L"dotnet-isolated";

            Configuration configuration(configurationXml, _missingConfig, L"", systemCalls);

            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"FooBarBaz", L"blah blah blah FooBarBaz blah blah blah", true));
        }

        TEST_METHOD(should_instrument_azure_function_fallback_to_app_pool_checking)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <log level=\"deBug\"/>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            systemCalls->environmentVariables[L"FUNCTIONS_WORKER_RUNTIME"] = L"dotnet-isolated";

            Configuration configuration(configurationXml, _missingConfig, L"", systemCalls);

            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"foo", L"", true));
        }

        TEST_METHOD(should_not_instrument_azure_function_func_exe_process_path)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <log level=\"deBug\"/>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            systemCalls->environmentVariables[L"FUNCTIONS_WORKER_RUNTIME"] = L"dotnet-isolated";

            Configuration configuration(configurationXml, _missingConfig, L"", systemCalls);

            Assert::IsFalse(configuration.ShouldInstrument(L"func.exe", L"", L"", L"blah blah blah FooBarBaz blah blah blah", true));
        }

        TEST_METHOD(should_instrument_azure_function_functionsnethost_exe_process_path)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <log level=\"deBug\"/>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            systemCalls->environmentVariables[L"FUNCTIONS_WORKER_RUNTIME"] = L"dotnet-isolated";

            Configuration configuration(configurationXml, _missingConfig, L"", systemCalls);

            Assert::IsTrue(configuration.ShouldInstrument(L"functionsnethost.exe", L"", L"", L"blah blah blah FooBarBaz blah blah blah", true));
        }

        TEST_METHOD(instrument_process)
        {
            ProcessesPtr processes(new Processes());
            processes->emplace(L"foo.exe");
            Configuration configuration(true, Logger::Level::LEVEL_INFO, processes);
            Assert::IsTrue(configuration.ShouldInstrument(L"foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(global_xml_agent_enabled_missing_local_xml_agent_enabled_missing)
        {
            Configuration configuration(_missingAgentEnabledXml, _missingAgentEnabledConfigPair);
            Assert::IsTrue(configuration.IsAgentEnabled());
        }

        TEST_METHOD(global_xml_agent_enabled_missing_local_xml_agent_enabled_true) {
            Configuration configuration(_missingAgentEnabledXml, _agentEnabledPair);
            Assert::IsTrue(configuration.IsAgentEnabled());
        }

        TEST_METHOD(global_xml_agent_enabled_missing_local_xml_agent_enabled_false) {
            Configuration configuration(_missingAgentEnabledXml, _agentDisabledPair);
            Assert::IsFalse(configuration.IsAgentEnabled());
        }

        TEST_METHOD(global_xml_missing) {
            std::function<void(void)> func = [this]() {
                Configuration configuration(_noXml, _agentDisabledPair);
                };

            Assert::ExpectException<NewRelic::Profiler::Configuration::ConfigurationException>(func, L"Should throw configuration exception when there's no global config.");
        }

        TEST_METHOD(global_xml_agent_enabled_false_local_xml_agent_enabled_false) {
            Configuration configuration(_agentDisabledXml, _agentDisabledPair);
            Assert::IsFalse(configuration.IsAgentEnabled());
        }

        TEST_METHOD(global_xml_agent_enabled_false_local_xml_agent_enabled_missing)
        {
            Configuration configuration(_agentDisabledXml, _missingAgentEnabledConfigPair);
            Assert::IsFalse(configuration.IsAgentEnabled());
        }

        TEST_METHOD(global_xml_agent_enabled_true_local_xml_agent_enabled_false)
        {
            Configuration configuration(_agentEnabledXml, _agentDisabledPair);
            Assert::IsFalse(configuration.IsAgentEnabled());
        }

        TEST_METHOD(gloabal_xml_agent_enabled_true_local_xml_agent_enabled_true)
        {
            Configuration configuration(_agentEnabledXml, _agentEnabledPair);
            Assert::IsTrue(configuration.IsAgentEnabled());
        }

        TEST_METHOD(global_xml_agent_enabled_true_local_xml_agent_enabled_missing)
        {
            Configuration configuration(_agentEnabledXml, _missingAgentEnabledConfigPair);
            Assert::IsTrue(configuration.IsAgentEnabled());
        }

        TEST_METHOD(global_xml_agent_enabled_false_local_xml_agent_enabled_true)
        {
            Configuration configuration(_agentDisabledXml, _agentEnabledPair);
            Assert::IsFalse(configuration.IsAgentEnabled());
        }

        TEST_METHOD(global_xml_agent_enabled_true_local_xml_missing)
        {
            Configuration configuration(_agentEnabledXml, _missingConfig);
            Assert::IsTrue(configuration.IsAgentEnabled());
        }

        TEST_METHOD(global_xml_agent_enabled_false_local_xml_missing)
        {
            Configuration configuration(_agentDisabledXml, _missingConfig);
            Assert::IsFalse(configuration.IsAgentEnabled());
        }

        TEST_METHOD(global_xml_agent_enabled_missing_local_xml_missing)
        {
            Configuration configuration(_missingAgentEnabledXml, _missingConfig);
            Assert::IsTrue(configuration.IsAgentEnabled());
        }

        TEST_METHOD(log_level_debug_from_xml)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <log level=\"deBug\"/>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::AreEqual(Logger::Level::LEVEL_DEBUG, configuration.GetLoggingLevel());
        }

        TEST_METHOD(log_level_from_environment_over_xml)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <log level=\"deBug\"/>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();

            systemCalls->environmentVariables[L"NEWRELIC_LOG_LEVEL"] = L"FiNeSt";

            Configuration configuration(configurationXml, _missingConfig, L"", systemCalls);
            Assert::AreEqual(Logger::Level::LEVEL_TRACE, configuration.GetLoggingLevel());
        }

        TEST_METHOD(instrument_process_from_xml)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <instrumentation>\
            <applications>\
                <application name=\"foo.exe\"/>\
            </applications>\
        </instrumentation>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(instrument_multiple_processes_from_xml)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <instrumentation>\
            <applications>\
                <application name=\"foo.exe\"/>\
                <application name=\"bar.exe\"/>\
            </applications>\
        </instrumentation>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"", L"", L"", false));
            Assert::IsTrue(configuration.ShouldInstrument(L"Bar.exe", L"", L"", L"", false));
        }

        TEST_METHOD(do_not_instrument_process_not_in_xml)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <instrumentation>\
            <applications>\
                <application name=\"foo.exe\"/>\
                <application name=\"bar.exe\"/>\
            </applications>\
        </instrumentation>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsFalse(configuration.ShouldInstrument(L"Baz.exe", L"", L"", L"", false));
        }

        TEST_METHOD(exception_on_missing_configuration_node)
        {
            std::wstring configurationXml(L"<?xml version=\"1.0\"?><foo/>");
            try
            {
                Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            }
            catch (...)
            {
                return;
            }

            Assert::Fail(L"Expected exception.");
        }

        TEST_METHOD(Azure_WebSites_background_application_pool_ignored)
        {
            std::wstring configurationXml(L"<?xml version=\"1.0\"?><configuration/>");
            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"~Foo", L"", false));
        }

        TEST_METHOD(tilde_in_string_but_not_at_start_is_not_ignored)
        {
            std::wstring configurationXml(L"<?xml version=\"1.0\"?><configuration/>");
            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"F~oo", L"", false));
        }


        TEST_METHOD(application_pools_instrument_by_default)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <applicationPool name='foo' instrument='false'/>\
        </applicationPools>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"bar", L"", false));
        }

        TEST_METHOD(application_pool_blacklist_without_default)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <applicationPool name='foo' instrument='false'/>\
        </applicationPools>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"foo", L"", false));
        }

        TEST_METHOD(application_pool_whitelist_without_default)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <applicationPool name='foo' instrument='true'/>\
        </applicationPools>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"foo", L"", false));
        }

        TEST_METHOD(application_pool_blacklist)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <defaultBehavior instrument='false'/>\
        </applicationPools>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"foo", L"", false));
        }

        TEST_METHOD(application_pool_whitelist)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <defaultBehavior instrument='true'/>\
        </applicationPools>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"foo", L"", false));
        }

        TEST_METHOD(application_pools_some_white_some_black_some_default_black)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <defaultBehavior instrument='false'/>\
            <applicationPool name='whiteFoo' instrument='true'/>\
            <applicationPool name='whiteBar' instrument='true'/>\
            <applicationPool name='blackFoo' instrument='false'/>\
            <applicationPool name='blackBar' instrument='false'/>\
        </applicationPools>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"whiteFoo", L"", false));
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"whiteBar", L"", false));
            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"blackFoo", L"", false));
            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"blackBar", L"", false));
            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"defaultFoo", L"", false));
            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"defaultBar", L"", false));
        }

        TEST_METHOD(application_pools_some_white_some_black_some_default_white)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <defaultBehavior instrument='true'/>\
            <applicationPool name='whiteFoo' instrument='true'/>\
            <applicationPool name='whiteBar' instrument='true'/>\
            <applicationPool name='blackFoo' instrument='false'/>\
            <applicationPool name='blackBar' instrument='false'/>\
        </applicationPools>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair);
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"whiteFoo", L"", false));
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"whiteBar", L"", false));
            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"blackFoo", L"", false));
            Assert::IsFalse(configuration.ShouldInstrument(L"w3wp.exe", L"", L"blackBar", L"", false));
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"defaultFoo", L"", false));
            Assert::IsTrue(configuration.ShouldInstrument(L"w3wp.exe", L"", L"defaultBar", L"", false));
        }

        TEST_METHOD(application_pools_oop_instrument_by_default)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <applicationPool name='foo' instrument='false'/>\
        </applicationPools>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair, L"", systemCalls);
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"bar", L"", true));
        }

        TEST_METHOD(application_pool_oop_blacklist_without_default)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <applicationPool name='foo' instrument='false'/>\
        </applicationPools>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair, L"", systemCalls);
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"foo", L"", true));
        }

        TEST_METHOD(application_pool_oop_whitelist_without_default)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <applicationPool name='foo' instrument='true'/>\
        </applicationPools>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair, L"", systemCalls);
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"foo", L"", true));
        }

        TEST_METHOD(application_pool_oop_blacklist)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <defaultBehavior instrument='false'/>\
        </applicationPools>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair, L"", systemCalls);
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"foo", L"", true));
        }

        TEST_METHOD(application_pool_oop_whitelist)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <defaultBehavior instrument='true'/>\
        </applicationPools>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair, L"", systemCalls);
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"foo", L"", true));
        }

        TEST_METHOD(application_pools_oop_some_white_some_black_some_default_black)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <defaultBehavior instrument='false'/>\
            <applicationPool name='whiteFoo' instrument='true'/>\
            <applicationPool name='whiteBar' instrument='true'/>\
            <applicationPool name='blackFoo' instrument='false'/>\
            <applicationPool name='blackBar' instrument='false'/>\
        </applicationPools>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair, L"", systemCalls);
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"whiteFoo", L"", true));
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"whiteBar", L"", true));
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"blackFoo", L"", true));
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"blackBar", L"", true));
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"defaultFoo", L"", true));
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"defaultBar", L"", true));
        }

        TEST_METHOD(application_pools_oop_some_white_some_black_some_default_white)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <applicationPools>\
            <defaultBehavior instrument='true'/>\
            <applicationPool name='whiteFoo' instrument='true'/>\
            <applicationPool name='whiteBar' instrument='true'/>\
            <applicationPool name='blackFoo' instrument='false'/>\
            <applicationPool name='blackBar' instrument='false'/>\
        </applicationPools>\
    </configuration>\
    ");

            auto systemCalls = std::make_shared<NewRelic::Profiler::Logger::Test::SystemCalls>();
            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair, L"", systemCalls);
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"whiteFoo", L"", true));
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"whiteBar", L"", true));
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"blackFoo", L"", true));
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"blackBar", L"", true));
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"defaultFoo", L"", true));
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"w3wp.exe", L"defaultBar", L"", true));
        }

        TEST_METHOD(agent_enabled_via_application_configuration)
        {
            std::wstring newRelicConfigXml(L"\
    <?xml version='1.0'?>\
    <configuration agentEnabled='true'/>\
    ");

            std::wstring appConfigXml(L"\
    <?xml version='1.0' encoding='utf-8'?>\
    <configuration>\
        <appSettings>\
            <add key='NewRelic.AgentEnabled' value='true'/>\
        </appSettings>\
    </configuration>\
    ");

            Configuration configuration(newRelicConfigXml, _missingAgentEnabledConfigPair, appConfigXml);
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(agent_disabled_via_application_configuration)
        {
            std::wstring newRelicConfigXml(L"\
    <?xml version='1.0'?>\
    <configuration agentEnabled='false'/>\
    ");

            std::wstring appConfigXml(L"\
    <?xml version='1.0' encoding='utf-8'?>\
    <configuration>\
        <appSettings>\
            <add key='NewRelic.AgentEnabled' value='false'/>\
        </appSettings>\
    </configuration>\
    ");

            Configuration configuration(newRelicConfigXml, _missingAgentEnabledConfigPair, appConfigXml);
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(agent_disabled_when_missing_from_application_configuration)
        {
            std::wstring newRelicConfigXml(L"\
    <?xml version='1.0'?>\
    <configuration agentEnabled='false'/>\
    ");

            std::wstring appConfigXml(L"\
    <?xml version='1.0' encoding='utf-8'?>\
    <configuration>\
        <appSettings>\
        </appSettings>\
    </configuration>\
    ");

            Configuration configuration(newRelicConfigXml, _missingAgentEnabledConfigPair, appConfigXml);
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(agent_disabled_when_app_config_does_not_exist)
        {
            std::wstring newRelicConfigXml(L"\
    <?xml version='1.0'?>\
    <configuration agentEnabled='false'/>\
    ");

            std::wstring appConfigXml(L"");

            Configuration configuration(newRelicConfigXml, _missingAgentEnabledConfigPair, appConfigXml);
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(agent_enabled_in_application_config_is_case_insensitive)
        {
            std::wstring newRelicConfigXml(L"\
    <?xml version='1.0'?>\
    <configuration agentEnabled='true'/>\
    ");

            std::wstring appConfigXml(L"\
    <?xml version='1.0' encoding='utf-8'?>\
    <configuration>\
        <appSettings>\
            <aDD KEY='NeWrEliC.AgEnTeNablED' vAlue='TrUe'/>\
        </appSettings>\
    </configuration>\
    ");

            Configuration configuration(newRelicConfigXml, _missingAgentEnabledConfigPair, appConfigXml);
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(agent_disabled_when_disabled_in_application_config_but_listed_in_newrelic_config)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <instrumentation>\
            <applications>\
                <application name=\"foo.exe\"/>\
                <application name=\"bar.exe\"/>\
            </applications>\
        </instrumentation>\
    </configuration>\
    ");

            std::wstring appConfigXml(L"\
    <?xml version='1.0' encoding='utf-8'?>\
    <configuration>\
        <appSettings>\
            <add key='NewRelic.AgentEnabled' value='false'/>\
        </appSettings>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair, appConfigXml);
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(agent_disabled_when_junk_in_application_config)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration/>\
    ");

            std::wstring appConfigXml(L"\
    <?xml version='1.0' encoding='utf-8'?>\
    <configuration>\
        <appSettings>\
            <add key='NewRelic.AgentEnabled' value='junk'/>\
        </appSettings>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair, appConfigXml);
            Assert::IsFalse(configuration.ShouldInstrument(L"Foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(agent_enabled_when_in_process_list_and_no_flag_in_application_config)
        {
            std::wstring configurationXml(L"\
    <?xml version=\"1.0\"?>\
    <configuration>\
        <instrumentation>\
            <applications>\
                <application name=\"foo.exe\"/>\
            </applications>\
        </instrumentation>\
    </configuration>\
    ");

            std::wstring appConfigXml(L"\
    <?xml version='1.0' encoding='utf-8'?>\
    <configuration>\
        <appSettings>\
        </appSettings>\
    </configuration>\
    ");

            Configuration configuration(configurationXml, _missingAgentEnabledConfigPair, appConfigXml);
            Assert::IsTrue(configuration.ShouldInstrument(L"Foo.exe", L"", L"", L"", false));
        }

        TEST_METHOD(win32helper_throw_on_error_throws_on_failure)
        {
            std::function<void(void)> func = []() {
                auto AlwaysEFAIL = [](int) { return E_FAIL; };
                ThrowOnError(AlwaysEFAIL, 1);
                };
            Assert::ExpectException<NewRelic::Profiler::Win32Exception>(func, L"ThrowOnError should throw when it receives a failing HRESULT.");
        }

        TEST_METHOD(win32helper_throw_on_error_doesnt_throw_on_success)
        {
            std::function<void(void)> func = []() {
                auto AlwaysSOK = [](int) { return S_OK; };
                ThrowOnError(AlwaysSOK, 1);
                };
            func();
        }

        TEST_METHOD(win32helper_throw_on_null_handle_throws_on_nullptr)
        {
            std::function<void(void)> func = []() {
                auto AlwaysReturnsNull = [](int) { return nullptr; };
                ThrowOnNullHandle(AlwaysReturnsNull, 1);
                };
            Assert::ExpectException<NewRelic::Profiler::Win32NullHandleException>(func, L"ThrowOnNullHandle should throw when it receives a nullptr.");
        }

        TEST_METHOD(win32helper_throw_on_null_handle_doesnt_throw_on_non_nullptr)
        {
            std::function<void(void)> func = [this]() {
                auto AlwaysReturnsNonNull = [this](int) { return this; };
                ThrowOnNullHandle(AlwaysReturnsNonNull, 1);
                };
            func();
        }

    private:

        const std::wstring _agentDisabledXml = L"\
    <?xml version=\"1.0\"?>\
    <configuration xmlns=\"urn:newrelic-config\" agentEnabled=\"false\"/>\
    ";
        const std::wstring _agentEnabledXml = L"\
    <?xml version=\"1.0\"?>\
    <configuration xmlns=\"urn:newrelic-config\" agentEnabled=\"true\"/>\
    ";
        const std::wstring _missingAgentEnabledXml = L"\
    <?xml version=\"1.0\"?>\
    <configuration/>\
    ";

        const std::wstring _noXml = L"";

        const std::pair<xstring_t, bool> _missingAgentEnabledConfigPair = std::make_pair(L"<?xml version=\"1.0\"?><configuration/>", false);
        const std::pair<xstring_t, bool> _missingConfig = std::make_pair(L"", false);
        const std::pair<xstring_t, bool> _agentDisabledPair = std::make_pair(L"\
    <?xml version=\"1.0\"?>\
    <configuration xmlns=\"urn:newrelic-config\" agentEnabled=\"false\"/>\
    ", true);
        const std::pair<xstring_t, bool> _agentEnabledPair = std::make_pair(L"\
    <?xml version=\"1.0\"?>\
    <configuration xmlns=\"urn:newrelic-config\" agentEnabled=\"true\"/>\
    ", true);
    };
}}}}
