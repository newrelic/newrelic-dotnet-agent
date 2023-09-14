// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Configuration;
using NewRelic.Core.Logging;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Api
{
    public class AgentBridgeApi
    {
        private readonly IAgent _agent;
        private readonly IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;
        private readonly IConfigurationService _configSvc;

        public AgentBridgeApi(IAgent agent, IApiSupportabilityMetricCounters apiSupportabilityMetricCounters, IConfigurationService configSvc)
        {
            _agent = agent;
            _configSvc = configSvc;
            _apiSupportabilityMetricCounters = apiSupportabilityMetricCounters;
        }

        public TransactionBridgeApi CurrentTransaction
        {
            get
            {
                try
                {
                    using (new IgnoreWork())
                    {
                        _apiSupportabilityMetricCounters.Record(ApiMethod.CurrentTransaction);
                        var transaction = _agent.CurrentTransaction;
                        return new TransactionBridgeApi(transaction, _apiSupportabilityMetricCounters, _configSvc);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        Log.Error(ex, "Failed to get CurrentTransaction");
                    }
                    catch (Exception)
                    {
                        //Swallow the error
                    }
                    return null;
                }
            }
        }

        public object TraceMetadata
        {
            get
            {
                try
                {
                    using (new IgnoreWork())
                    {
                        _apiSupportabilityMetricCounters.Record(ApiMethod.TraceMetadata);
                        return _agent.TraceMetadata;
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        Log.Error(ex, "Failed to get TraceMetadata");
                    }
                    catch (Exception)
                    {
                        //Swallow the error
                    }
                    return null;
                }

            }
        }

        public Dictionary<string, string> GetLinkingMetadata()
        {
            try
            {
                using (new IgnoreWork())
                {
                    _apiSupportabilityMetricCounters.Record(ApiMethod.GetLinkingMetadata);
                    return _agent.GetLinkingMetadata();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(ex, "Error in GetLinkingMetadata");
                }
                catch (Exception)
                {
                    //Swallow the error
                }

                return null;
            }

        }
    }
}
