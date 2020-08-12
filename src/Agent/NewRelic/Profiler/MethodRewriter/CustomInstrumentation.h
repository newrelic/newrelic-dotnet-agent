// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#pragma once

#include "../Logging/Logger.h"
#include "../Configuration/InstrumentationConfiguration.h"
#include <map>


namespace NewRelic { namespace Profiler { namespace MethodRewriter {

    // This class tracks the custom or "live" instrumentation that has been sent to the agent
    // from the server, specifically the set of instrumentation that is currently applied to the process.
    class CustomInstrumentation
    {
    private:
        std::mutex _mutex;
        Configuration::InstrumentationXmlSetPtr _customInstrumentationXml;

    public:
        Configuration::InstrumentationXmlSetPtr GetCustomInstrumentationXml()
        {
            std::lock_guard<std::mutex> lock(_mutex);
            if (_customInstrumentationXml == nullptr)
            {
                return std::make_shared<Configuration::InstrumentationXmlSet>();
            }
            return std::make_shared<Configuration::InstrumentationXmlSet>(*_customInstrumentationXml);
        }

        void ReplaceCustomInstrumentationXml(Configuration::InstrumentationXmlSetPtr instrumentation)
        {
            std::lock_guard<std::mutex> lock(_mutex);
            _customInstrumentationXml = std::make_shared<Configuration::InstrumentationXmlSet>(*instrumentation);
        }
    };

    // This class tracks the xml that is sent from the server as "live instrumentation" when the agent connects.
    // When the managed agent has added all of the live instrumentation files to this builder (currently we'll
    // only receive one document) it'll call `Apply`.  At that point we'll call Build on this class to get the
    // instrumentation points, rejit that stuff and call ReplaceCustomInstrumentationXml on the above class.
    // (yeah, this isn't really a builder in the classic sense, but we use it to track
    // the custom xml being added by the managed agent.)
    class CustomInstrumentationBuilder
    {
    private:
        std::mutex _xmlMapMutex;
        std::map<xstring_t, xstring_t> _xmlMap;
    public:
        void AddCustomInstrumentationXml(xstring_t fileName, xstring_t xml)
        {
            std::lock_guard<std::mutex> lock(_xmlMapMutex);
            _xmlMap[fileName] = xml;
        }

        Configuration::InstrumentationXmlSetPtr Build()
        {
            std::lock_guard<std::mutex> lock(_xmlMapMutex);
            auto xmlMap = std::make_shared<Configuration::InstrumentationXmlSet>(_xmlMap);
            _xmlMap.clear();

            return xmlMap;
        }
    };
}}}