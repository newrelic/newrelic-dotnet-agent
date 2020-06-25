/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include "../Configuration/Configuration.h"
#include "../Configuration/InstrumentationConfiguration.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
    class InstrumentationSettings {
    public:
        InstrumentationSettings(Configuration::InstrumentationConfigurationPtr instrumentationConfig, xstring_t corePath) :
            _instrumentationConfig(instrumentationConfig),
            _corePath(corePath)
        {}

        xstring_t GetCorePath()
        {
            return _corePath;
        }

        Configuration::InstrumentationConfigurationPtr GetInstrumentationConfiguration()
        {
            return _instrumentationConfig;
        }

    private:
        Configuration::InstrumentationConfigurationPtr _instrumentationConfig;
        xstring_t _corePath;
    };

    typedef std::shared_ptr<InstrumentationSettings> InstrumentationSettingsPtr;
}}}