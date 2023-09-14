// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Configuration;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Api
{
    public class SpanBridgeApi
    {
        private readonly ISpan _span;
        private readonly IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;
        private readonly IConfigurationService _configSvc;

        public SpanBridgeApi(ISpan span, IApiSupportabilityMetricCounters apiSupportabilityMetricCounters, IConfigurationService configSvc)
        {
            _span = span;
            _apiSupportabilityMetricCounters = apiSupportabilityMetricCounters;
            _configSvc = configSvc;
        }

        public object AddCustomAttribute(string key, object value)
        {
            try
            {
                _apiSupportabilityMetricCounters.Record(ApiMethod.SpanAddCustomAttribute);

                if (!_configSvc.Configuration.CaptureCustomParameters)
                {
                    return _span;
                }

                _span.AddCustomAttribute(key, value);
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(ex, "Error in AddCustomAttribute");
                }
                catch (Exception)
                {
                    // Swallow the error.. nom nom
                }
            }

            return _span;
        }

        public object SetName(string name)
        {
            try
            {
                _apiSupportabilityMetricCounters.Record(ApiMethod.SpanSetName);

                _span.SetName(name);
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(ex, "Error in SetName");
                }
                catch (Exception)
                {
                    // Swallow the error.. nom nom
                }
            }

            return _span;
        }
    }
}
