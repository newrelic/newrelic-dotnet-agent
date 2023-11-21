/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../Logging/Logger.h"
#include "../RapidXML/rapidxml.hpp"
#include "Exceptions.h"
#include "Strings.h"
#include <memory>
#include <regex>
#include <set>
#include <string>
#include <utility>

#ifdef PAL_STDCPP_COMPAT
#include "../Profiler/UnixSystemCalls.h"
#else
#include "../Profiler/SystemCalls.h"
#endif

namespace NewRelic { namespace Profiler { namespace Configuration {
    typedef std::set<xstring_t> Processes;
    typedef std::shared_ptr<Processes> ProcessesPtr;
    typedef std::set<xstring_t> ApplicationPools;
    typedef std::shared_ptr<ApplicationPools> ApplicationPoolsPtr;

    class Configuration {
    public:
        // intentionally doesn't take a const wstring& because rapidxml will do destructive operations on the string
        Configuration(xstring_t globalNewRelicConfiguration, std::pair<xstring_t, bool> localNewRelicConfiguration, xstring_t applicationConfiguration = _X(""), std::shared_ptr<NewRelic::Profiler::Logger::IFileDestinationSystemCalls> systemCalls = nullptr)
            : _agentEnabled(true)
            , _agentEnabledInLocalConfig(false)
            , _logLevel(Logger::Level::LEVEL_INFO)
            , _consoleLogging(false)
            , _loggingEnabled(true)
            , _processes(new Processes())
            , _applicationPoolsWhiteList(new ApplicationPools())
            , _applicationPoolsBlackList(new ApplicationPools())
            , _applicationPoolsAreEnabledByDefault(true)
            , _agentEnabledSetInApplicationConfiguration(false)
            , _agentEnabledViaApplicationConfiguration(false)
            , _systemCalls(systemCalls)
        {
            try {
                rapidxml::xml_document<xchar_t> globalNewRelicConfigurationDocument;
                globalNewRelicConfigurationDocument.parse<rapidxml::parse_trim_whitespace | rapidxml::parse_normalize_whitespace>(const_cast<xchar_t*>(globalNewRelicConfiguration.c_str()));

                auto globalNewRelicConfigurationNode  = GetConfigurationNode(globalNewRelicConfigurationDocument);
                if (globalNewRelicConfigurationNode == nullptr) 
                {
                    LogError(L"Unable to locate configuration node in the global newrelic.config file.");
                    throw ConfigurationException();
                }

                auto appliedNewRelicConfigurationNode = globalNewRelicConfigurationNode;

                if (localNewRelicConfiguration.second)
                {
                    try
                    {
                        rapidxml::xml_document<xchar_t> localNewRelicConfigurationDocument;
                        localNewRelicConfigurationDocument.parse<rapidxml::parse_trim_whitespace | rapidxml::parse_normalize_whitespace>(const_cast<xchar_t*>(localNewRelicConfiguration.first.c_str()));

                        auto localNewRelicConfigurationNode = GetConfigurationNode(localNewRelicConfigurationDocument);
                        if (localNewRelicConfigurationNode == nullptr)
                        {
                            LogWarn(L"Unable to find the configuration node in local newrelic.config file. Defaulting to global version.");
                            SetAgentEnabled(globalNewRelicConfigurationNode);
                        }
                        else
                        {
                            appliedNewRelicConfigurationNode = localNewRelicConfigurationNode;
                            SetAgentEnabled(globalNewRelicConfigurationNode, localNewRelicConfigurationNode);
                        }
                    }
                    catch (...)
                    {
                        LogWarn(L"Unable to parse local newrelic.config. Defaulting to global version.");
                        SetAgentEnabled(globalNewRelicConfigurationNode);
                    }
                }
                else
                {
                    SetAgentEnabled(globalNewRelicConfigurationNode);
                }

                SetLogLevel(appliedNewRelicConfigurationNode);
                SetProcesses(appliedNewRelicConfigurationNode);
                SetApplicationPools(appliedNewRelicConfigurationNode);

            } catch (const rapidxml::parse_error& exception) {
                // We log two separate error messages here because sometimes the logging macros hang when
                // logging the "where" contents
                LogError(L"Exception thrown while attempting to parse main newrelic.config file: ", exception.what());
                LogError(L"Exception thrown at ", exception.where<wchar_t>());
                throw ConfigurationException();
            }

            try {
                SetEnabledViaApplicationConfiguration(applicationConfiguration);
            } catch (const rapidxml::parse_error& exception) {
                // We log two separate error messages here because sometimes the logging macros hang when
                // logging the "where" contents
                LogError(L"Exception thrown while attempting to parse app-specific config file: ", exception.what());
                LogError(L"Exception thrown at ", exception.where<wchar_t>());
                throw ConfigurationException();
            }
        }

        Configuration(
            bool agentEnabled = true,
            Logger::Level logLevel = Logger::Level::LEVEL_INFO,
            ProcessesPtr processes = ProcessesPtr(new Processes()),
            ApplicationPoolsPtr whiteList = ApplicationPoolsPtr(new ApplicationPools()),
            ApplicationPoolsPtr blackList = ApplicationPoolsPtr(new ApplicationPools()),
            bool poolsEnabledByDefault = true,
            bool agentEnabledSetInApplicationConfiguration = false,
            bool agentEnabledViaApplicationConfiguration = false)
            : _agentEnabled(agentEnabled)
            , _agentEnabledInLocalConfig(false)
            , _logLevel(logLevel)
            , _consoleLogging(false)
            , _loggingEnabled(true)
            , _processes(processes)
            , _applicationPoolsWhiteList(whiteList)
            , _applicationPoolsBlackList(blackList)
            , _applicationPoolsAreEnabledByDefault(poolsEnabledByDefault)
            , _agentEnabledSetInApplicationConfiguration(agentEnabledSetInApplicationConfiguration)
            , _agentEnabledViaApplicationConfiguration(agentEnabledViaApplicationConfiguration)
        {
        }

        virtual bool IsAgentEnabled()
        {
            return _agentEnabled;
        }

        void Tokenize(xstring_t const& str, std::vector<xstring_t>& out)
        {
            xstring_t const delims = _X(" ");

            size_t start, end = 0;
            while ((start = str.find_first_not_of(delims, end)) != std::string::npos) {
                end = str.find_first_of(delims, start + 1);
                out.push_back(str.substr(start, end - start));
            }
        }

        // Does a case insensitive string comparison and returns true if the sourceString ends strictly with suffixString.
        // The suffixString must start from beginning or after ' ', '"', '/', '\\' or '\'' character in the sourceString.
        // For example: return true if sourceString = "c://program//dotnet.exe" and suffixString="dotnet.exe"
        // return false if sourceString = "c://program//abcdotnet.exe" and suffixString="dotnet.exe"
        bool EndsWithAnExactSubStringCaseInsensitive(xstring_t const& sourceString, xstring_t const& suffixString)
        {
            if (suffixString.size() > sourceString.size()) {
                return false;
            }

            xchar_t c = '\0';

            if (suffixString.size() < sourceString.size()) {
                c = sourceString[sourceString.size() - suffixString.size() - 1];
            }

            //The variable c has the character right before the possibly suffixString that the sourceString ends with.
            //We are checking if the suffixString starts from beginning or after ' ', '"', '/', '\\' or '\'' character in the sourceString by evaluating c.
            if (c != '\0' && c != xchar_t(' ') && c != xchar_t('/') && c != xchar_t('\\') && c != xchar_t('\'') && c != xchar_t('"')) {
                return false;
            }

            return std::equal(suffixString.rbegin(), suffixString.rend(), sourceString.rbegin(),
                [](const xchar_t a, const xchar_t b) {
                    return tolower(a) == tolower(b);
                });
        }

        bool ShouldInstrument(xstring_t const& processPath, xstring_t const& parentProcessPath, xstring_t const& appPoolId, xstring_t const& commandLine, bool isCoreClr)
        {
            if (isCoreClr)
            {
                return ShouldInstrumentNetCore(processPath, parentProcessPath, appPoolId, commandLine);
            }
            else
            {
                return ShouldInstrumentNetFramework(processPath, appPoolId);
            }
        }

        virtual Logger::Level GetLoggingLevel()
        {
            return _logLevel;
        }

        bool GetConsoleLogging()
        {
            return _consoleLogging;
        }
        bool GetLoggingEnabled()
        {
            return _loggingEnabled;
        }

    private:
        bool _agentEnabled;
        bool _agentEnabledInLocalConfig;
        Logger::Level _logLevel;
        bool _consoleLogging;
        bool _loggingEnabled;
        ProcessesPtr _processes;
        ApplicationPoolsPtr _applicationPoolsWhiteList;
        ApplicationPoolsPtr _applicationPoolsBlackList;
        bool _applicationPoolsAreEnabledByDefault;
        bool _agentEnabledSetInApplicationConfiguration;
        bool _agentEnabledViaApplicationConfiguration;
        std::shared_ptr<NewRelic::Profiler::Logger::IFileDestinationSystemCalls> _systemCalls;

        rapidxml::xml_node<xchar_t>* GetConfigurationNode(const rapidxml::xml_document<xchar_t>& document)
        {
            auto configurationNode = document.first_node(_X("configuration"), 0, false);
            if (configurationNode == nullptr) {
                return nullptr;
            }

            return configurationNode;
        }

        void SetAgentEnabled(rapidxml::xml_node<xchar_t>* configurationNode)
        {
            auto agentEnabledAttribute = configurationNode->first_attribute(_X("agentEnabled"), 0, false);
            if (agentEnabledAttribute == nullptr)
                return;

            auto agentEnabledString = agentEnabledAttribute->value();
            _agentEnabled = Strings::AreEqualCaseInsensitive(agentEnabledString, _X("true"));
        }

        void SetAgentEnabled(rapidxml::xml_node<xchar_t>* globalConfigurationNode, rapidxml::xml_node<xchar_t>* localConfigurationNode)
        {
            auto globalAgentEnabledAttribute = globalConfigurationNode->first_attribute(_X("agentEnabled"), 0, false);
            auto globalAgentEnabledString = globalAgentEnabledAttribute == nullptr ? _X("true") : globalAgentEnabledAttribute->value();
            auto enabledInGlobalConfig = Strings::AreEqualCaseInsensitive(globalAgentEnabledString, _X("true"));
            LogInfo(L"Global config agentEnabled=", enabledInGlobalConfig ? L"true" : L"false");

            auto localAgentEnabledAttribute = localConfigurationNode->first_attribute(_X("agentEnabled"), 0, false);
            auto localAgentEnabledString = localAgentEnabledAttribute == nullptr ? _X("true") : localAgentEnabledAttribute->value();
            auto enabledInLocalConfig = Strings::AreEqualCaseInsensitive(localAgentEnabledString, _X("true"));
            LogInfo(L"Local config agentEnabled=", enabledInLocalConfig ? L"true" : L"false");

            _agentEnabled = enabledInGlobalConfig && enabledInLocalConfig;
            _agentEnabledInLocalConfig = enabledInLocalConfig;
        }

        void SetLogLevel(rapidxml::xml_node<xchar_t>* configurationNode)
        {
            if (_systemCalls) {
                auto envLevel = _systemCalls->GetNewRelicLogLevel();
                if (envLevel) {
                    _logLevel = TryParseLogLevel(*envLevel);
                    return;
                }
            }

            auto logNode = configurationNode->first_node(_X("log"), 0, false);
            if (logNode == nullptr)
                return;

            auto consoleAttribute = logNode->first_attribute(_X("console"), 0, false);
            if (consoleAttribute != nullptr)
            {
                _consoleLogging = Strings::AreEqualCaseInsensitive(consoleAttribute->value(), _X("true"));
            }

            auto enabledAttribute = logNode->first_attribute(_X("enabled"), 0, false);
            if (enabledAttribute != nullptr)
            {
                _loggingEnabled = Strings::AreEqualCaseInsensitive(enabledAttribute->value(), _X("true"));
            }

            auto logLevelAttribute = logNode->first_attribute(_X("level"), 0, false);
            if (logLevelAttribute == nullptr)
                return;

            auto level = logLevelAttribute->value();
            _logLevel = TryParseLogLevel(level);
        }

        Logger::Level TryParseLogLevel(const xstring_t& logText)
        {
            if (Strings::AreEqualCaseInsensitive(logText, _X("off"))) {
                return Logger::Level::LEVEL_ERROR;
            } else if (Strings::AreEqualCaseInsensitive(logText, _X("error"))) {
                return Logger::Level::LEVEL_ERROR;
            } else if (Strings::AreEqualCaseInsensitive(logText, _X("warn"))) {
                return Logger::Level::LEVEL_WARN;
            } else if (Strings::AreEqualCaseInsensitive(logText, _X("info"))) {
                return Logger::Level::LEVEL_INFO;
            } else if (Strings::AreEqualCaseInsensitive(logText, _X("debug"))) {
                return Logger::Level::LEVEL_DEBUG;
            } else if (Strings::AreEqualCaseInsensitive(logText, _X("fine"))) {
                return Logger::Level::LEVEL_DEBUG;
            } else if (Strings::AreEqualCaseInsensitive(logText, _X("verbose"))) {
                return Logger::Level::LEVEL_TRACE;
            } else if (Strings::AreEqualCaseInsensitive(logText, _X("finest"))) {
                return Logger::Level::LEVEL_TRACE;
            } else if (Strings::AreEqualCaseInsensitive(logText, _X("all"))) {
                return Logger::Level::LEVEL_TRACE;
            } else {
                return _logLevel;
            }
        }

        void SetProcesses(rapidxml::xml_node<xchar_t>* configurationNode)
        {
            auto instrumentationNode = configurationNode->first_node(_X("instrumentation"), 0, false);
            if (instrumentationNode == nullptr)
                return;

            auto applicationsNode = instrumentationNode->first_node(_X("applications"), 0, false);
            if (applicationsNode == nullptr)
                return;

            for (auto applicationNode = applicationsNode->first_node(_X("application"), 0, false); applicationNode; applicationNode = applicationNode->next_sibling(_X("application"), 0, false)) {
                auto processName = applicationNode->first_attribute(_X("name"), 0, false);
                if (processName == nullptr)
                    continue;
                _processes->emplace(processName->value());
            }
        }

        void SetApplicationPools(rapidxml::xml_node<xchar_t>* configurationNode)
        {
            auto applicationPoolsNode = configurationNode->first_node(_X("applicationPools"), 0, false);
            if (applicationPoolsNode == nullptr)
                return;

            SetDefaultApplicationPoolBehavior(applicationPoolsNode);

            for (auto applicationPoolNode = applicationPoolsNode->first_node(_X("applicationPool"), 0, false); applicationPoolNode; applicationPoolNode = applicationPoolNode->next_sibling(_X("applicationPool"), 0, false)) {
                SetApplicationPoolBehavior(applicationPoolNode);
            }
        }

        void SetDefaultApplicationPoolBehavior(rapidxml::xml_node<xchar_t>* applicationPoolsNode)
        {
            auto defaultApplicationPoolNode = applicationPoolsNode->first_node(_X("defaultBehavior"), 0, false);
            if (defaultApplicationPoolNode == nullptr)
                return;

            auto defaultInstrumentAttrbitue = defaultApplicationPoolNode->first_attribute(_X("instrument"), 0, false);
            if (defaultInstrumentAttrbitue == nullptr)
                return;

            auto defaultInstrumentString = defaultInstrumentAttrbitue->value();
            _applicationPoolsAreEnabledByDefault = Strings::AreEqualCaseInsensitive(defaultInstrumentString, _X("true"));
        }

        void SetApplicationPoolBehavior(rapidxml::xml_node<xchar_t>* applicationPoolNode)
        {
            auto nameAttribute = applicationPoolNode->first_attribute(_X("name"), 0, false);
            if (nameAttribute == nullptr) {
                LogWarn(L"ApplicationPool element in configuration file is missing the 'name' attribute.");
                return;
            }

            auto instrumentAttribute = applicationPoolNode->first_attribute(_X("instrument"), 0, false);
            if (instrumentAttribute == nullptr) {
                LogWarn(L"ApplicationPool element in configuration file is missing the 'instrument' attribute.");
                return;
            }

            auto instrumentString = instrumentAttribute->value();
            bool instrument = Strings::AreEqualCaseInsensitive(instrumentString, _X("true"));

            if (instrument) {
                _applicationPoolsWhiteList->emplace(nameAttribute->value());
            } else {
                _applicationPoolsBlackList->emplace(nameAttribute->value());
            }
        }

        void SetEnabledViaApplicationConfiguration(const xstring_t& applicationConfiguration)
        {
            if (applicationConfiguration.empty())
                return;

            rapidxml::xml_document<xchar_t> document;
            document.parse<rapidxml::parse_trim_whitespace | rapidxml::parse_normalize_whitespace>(const_cast<xchar_t*>(applicationConfiguration.c_str()));
            auto configurationNode = GetConfigurationNode(document);

            auto appSettingsNode = configurationNode->first_node(_X("appSettings"), 0, false);
            if (appSettingsNode == nullptr)
                return;

            for (auto addNode = appSettingsNode->first_node(_X("add"), 0, false); addNode; addNode = addNode->next_sibling(_X("add"), 0, false)) {
                auto keyAttribute = addNode->first_attribute(_X("key"), 0, false);
                if (keyAttribute == nullptr)
                    continue;
                if (!Strings::AreEqualCaseInsensitive(keyAttribute->value(), _X("NewRelic.AgentEnabled")))
                    continue;

                _agentEnabledSetInApplicationConfiguration = true;

                auto valueAttribute = addNode->first_attribute(_X("value"), 0, false);
                if (valueAttribute == nullptr)
                    continue;
                if (Strings::AreEqualCaseInsensitive(valueAttribute->value(), _X("true"))) {
                    _agentEnabledViaApplicationConfiguration = true;
                }
            }
        }

        static bool ApplicationPoolIsOnWhiteList(const xstring_t& appPoolId, const ApplicationPoolsPtr& whiteList)
        {
            if (whiteList->find(appPoolId) != whiteList->end())
                return true;
            else
                return false;
        }

        static bool ApplicationPoolIsOnBlackList(const xstring_t& appPoolId, const ApplicationPoolsPtr& blackList)
        {
            if (blackList->find(appPoolId) != blackList->end())
                return true;
            else
                return false;
        }

        static bool IsProcessInProcessList(const ProcessesPtr& processes, const xstring_t& processName)
        {
            // check the processes loaded from configuration
            for (auto validProcessName : *processes) {
                if (Strings::EndsWith(processName, validProcessName)) {
                    return true;
                }
            }

            return false;
        }

        bool IsIgnoreProcess(const xstring_t& processName)
        {
            //Since instrumenting the SMSvcHost.exe proccess was reported to cause connection failure, force the profiler to ignore this process for safety.
            if (Strings::EndsWith(processName, _X("SMSvcHost.exe")))
            {
                LogInfo(_X("The SMSvcHost.exe process has been identified as an ignored process."));
                return true;
            }

            return false;
        }

        bool IsW3wpProcess(const xstring_t& processName, xstring_t const& parentProcessName)
        {
            auto isIis = Strings::EndsWith(processName, _X("W3WP.EXE")) || Strings::EndsWith(parentProcessName, _X("W3WP.EXE"));

            LogInfo(_X("Process ") + processName + _X(" with parent process ") + parentProcessName + (isIis ? _X(" is") : _X(" is not")) + _X(" IIS."));

            return isIis;
        }

        bool ShouldInstrumentApplicationPool(const xstring_t& appPoolId)
        {
            if (ApplicationPoolIsOnBlackList(appPoolId, _applicationPoolsBlackList)) {
                LogInfo(_X("This application pool (") + appPoolId + _X(") is explicitly configured to NOT be instrumented."));
                return false;
            }

            if (ApplicationPoolIsOnWhiteList(appPoolId, _applicationPoolsWhiteList)) {
                LogInfo(_X("This application pool (") + appPoolId + _X(") is explicitly configured to be instrumented."));
                return true;
            }

            // appPoolName.StartsWith("~") for Azure WebSites background job pool
            if (appPoolId.find(_X("~")) == 0) {
                LogInfo(_X("This application pool (") + appPoolId + _X(") has been identified as an Azure WebSites built-in background application and will be ignored."));
                return false;
            }

            if (_applicationPoolsAreEnabledByDefault) {
                LogInfo(_X("This application pool (") + appPoolId + _X(") is not explicitly configured to be instrumented or not but application pools are set to be enabled by default."));
                return true;
            }

            LogInfo(_X("This application pool (") + appPoolId + _X(") is not explicitly configured to be instrumented or not but application pools are set to be disabled by default."));
            return false;
        }

        static bool ShouldInstrumentDefaultProcess(const xstring_t& processName)
        {
            // Visual Studio web server (Cassini)
            if (Strings::EndsWith(processName, _X("WEBDEV.WEBSERVER40.EXE")))
                return true;
            if (Strings::EndsWith(processName, _X("WEBDEV.WEBSERVER20.EXE")))
                return true;
            // IIS 6 metabase and non-web service host.
            if (Strings::EndsWith(processName, _X("INETINFO.EXE")))
                return true;
            // For Azure worker roles.
            if (Strings::EndsWith(processName, _X("WAWORKERHOST.EXE")))
                return true;
            // For Azure web roles.
            if (Strings::EndsWith(processName, _X("WAWEBHOST.EXE")))
                return true;
            // For WCF self-hosted.
            if (Strings::EndsWith(processName, _X("WCFSVCHOST.EXE")))
                return true;
            // IIS 6 worker process.
            if (Strings::EndsWith(processName, _X("ASPNET_WP.EXE")))
                return true;
            // IIS Express for dev/test.
            if (Strings::EndsWith(processName, _X("IISEXPRESS.EXE")))
                return true;
            if (Strings::EndsWith(processName, _X("DOTNET.EXE")))
                return true;

            return false;
        }

        // Test to see if we should instrument this .NET Core application at all
        bool ShouldInstrumentNetCore(xstring_t const& processPath, xstring_t const& parentProcessPath, xstring_t const& appPoolId, xstring_t const& commandLine)
        {
            //If it contains MsBuild, it is a build command and should not be profiled.
            bool isMsBuildInvocation = NewRelic::Profiler::Strings::ContainsCaseInsensitive(commandLine, _X("MSBuild.dll"));
            bool isKudu = NewRelic::Profiler::Strings::ContainsCaseInsensitive(commandLine, _X("Kudu.Services.Web"));

            std::vector<xstring_t> out;
            Tokenize(commandLine, out);

            //Search for "dotnet run" or "dotnet publish" variations.
            //If it is a hit, it should not instrument the invocation of dotnet.exe
            //Example Hits:    dotnet run
            //                dotnet.exe run -f netcoreapp2.2
            //                "c\program files\dotnet.exe" run
            //                f:\program files\dotnet.exe run -f netcoreapp2.2
            //                all of the above with publish instead of run.

            for (std::size_t i = 0; i < out.size(); i++) {

                bool isDotNetProcess = EndsWithAnExactSubStringCaseInsensitive(out[i], _X("dotnet\"")) ||
                    EndsWithAnExactSubStringCaseInsensitive(out[i], _X("dotnet'")) ||
                    EndsWithAnExactSubStringCaseInsensitive(out[i], _X("dotnet")) ||
                    EndsWithAnExactSubStringCaseInsensitive(out[i], _X("dotnet.exe\"")) ||
                    EndsWithAnExactSubStringCaseInsensitive(out[i], _X("dotnet.exe'")) ||
                    EndsWithAnExactSubStringCaseInsensitive(out[i], _X("dotnet.exe"));

                if (isDotNetProcess && i < out.size() - 1) {
                    if (NewRelic::Profiler::Strings::AreEqualCaseInsensitive(out[i + 1], _X("run")) ||
                        NewRelic::Profiler::Strings::AreEqualCaseInsensitive(out[i + 1], _X("publish")) ||
                        NewRelic::Profiler::Strings::AreEqualCaseInsensitive(out[i + 1], _X("restore")) ||
                        NewRelic::Profiler::Strings::AreEqualCaseInsensitive(out[i + 1], _X("new"))) {

                        LogInfo(L"This process will not be instrumented. Command line identified as invalid invocation for instrumentation");
                        return false;
                    }
                    else {
                        break;
                    }
                }
            }

            if (isMsBuildInvocation || isKudu) {
                LogInfo(L"This process will not be instrumented. Command line identified as invalid invocation for instrumentation");
                return false;
            }

            if (IsW3wpProcess(processPath, parentProcessPath)) {
                return ShouldInstrumentApplicationPool(appPoolId);
            }

            return true;
        }

        // Test to see if we should instrument this .NET Framework application at all
        bool ShouldInstrumentNetFramework(xstring_t const& processPath, xstring_t const& appPoolId)
        {
            return ShouldInstrumentProcess(processPath, _X(""), appPoolId);
        }

        bool ShouldInstrumentProcess(const xstring_t& processName, xstring_t const& parentProcessName, const xstring_t& appPoolId)
        {
            if (!_agentEnabled) {
                LogInfo("New Relic has been disabled via newrelic.config file.");
                return false;
            }

            if (IsIgnoreProcess(processName))
            {
                return false;
            }

            if (_agentEnabledSetInApplicationConfiguration) {
                if (_agentEnabledViaApplicationConfiguration) {
                    LogInfo(L"Enabling instrumentation for this process due to existence of NewRelic.AgentEnabled=true in ", processName, L".config.");
                    return true;
                }
                else {
                    LogInfo(L"Disabling instrumentation for this process due to the existence of NewRelic.AgentEnabled in ", processName, L".config which is set to a value other than 'true'.");
                    return false;
                }
            }

            if (IsProcessInProcessList(_processes, processName)) {
                LogInfo(L"Enabling instrumentation for this process (", processName, ") due to existence of application node in newrelic.config.");
                return true;
            }

            if (IsW3wpProcess(processName, parentProcessName)) {
                return ShouldInstrumentApplicationPool(appPoolId);
            }

            if (ShouldInstrumentDefaultProcess(processName)) {
                LogInfo(L"Enabling instrumentation for this process (", processName, L") due to it being in a predefined set of processes to be instrumented.");
                return true;
            }

            if (_agentEnabledInLocalConfig) {
                LogInfo(L"Enabling instrumentation for this process due to existence of agentEnabled=true in local newrelic.config.");
                return true;
            }

            LogInfo(L"This process (", processName, ") is not configured to be instrumented.");
            return false;
        }

    };
    typedef std::shared_ptr<Configuration> ConfigurationPtr;
}}}
