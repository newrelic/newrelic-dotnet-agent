// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Api
{
    public class TransactionBridgeApi
    {
        public static readonly TransportType[] TransportTypeMapping = new[]
        {
            TransportType.Unknown,
            TransportType.HTTP,
            TransportType.HTTPS,
            TransportType.Kafka,
            TransportType.JMS,
            TransportType.IronMQ,
            TransportType.AMQP,
            TransportType.Queue,
            TransportType.Other
        };

        private readonly ITransaction _transaction;
        private readonly IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;
        private readonly IConfigurationService _configSvc;

        public TransactionBridgeApi(ITransaction transaction, IApiSupportabilityMetricCounters apiSupportabilityMetricCounters, IConfigurationService configSvc)
        {
            _transaction = transaction;
            _apiSupportabilityMetricCounters = apiSupportabilityMetricCounters;
            _configSvc = configSvc;
        }

        [Obsolete("This method does nothing in version 9.x+ of the Agent.")]

        public object CreateDistributedTracePayload()
        {
            try
            {
                Log.Warn("CreateDistributedTracePayload was called by an outdated version of the AgentAPI. This method does nothing in version 9.x+ of the Agent.");
            }
            catch (Exception)
            {
            }

            // I hope people are null checking as this will always return null now
            return null;
        }

        [Obsolete("This method does nothing in version 9.x+ of the Agent")]
        public void AcceptDistributedTracePayload(string payload, int transportType)
        {
            try
            {
                Log.Warn("AcceptDistributedTracePayload was called by an outdated version of the AgentAPI. This method does nothing in version 9.x+ of the Agent");
            }
            catch (Exception)
            {
            }
        }

        public void InsertDistributedTraceHeaders<T>(T carrier, Action<T, string, string> setter)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _apiSupportabilityMetricCounters.Record(ApiMethod.InsertDistributedTraceHeaders);
                    _transaction.InsertDistributedTraceHeaders(carrier, setter);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(ex, "Error in InsertDistributedTraceHeaders<T>(T, Action<T, string, string>)");
                }
                catch (Exception)
                {
                    //Swallow the error
                }
            }
        }

        public void AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, int transportType)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _apiSupportabilityMetricCounters.Record(ApiMethod.AcceptDistributedTraceHeaders);
                    _transaction.AcceptDistributedTraceHeaders(carrier, getter, GetTransportTypeValue(transportType));
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(ex, "Error in AcceptDistributedTraceHeaders<T>(T, Func<T, string, IEnumerable<string>>, TransportType)");
                }
                catch (Exception)
                {
                    //Swallow the error
                }
            }
        }

        private static TransportType GetTransportTypeValue(int transportType)
        {
            if (transportType >= 0 && transportType < TransportTypeMapping.Length)
            {
                return TransportTypeMapping[transportType];
            }

            return TransportType.Unknown;
        }

        public object AddCustomAttribute(string key, object value)
        {
            try
            {
                _apiSupportabilityMetricCounters.Record(ApiMethod.TransactionAddCustomAttribute);

                if (!_configSvc.Configuration.CaptureCustomParameters)
                {
                    return _transaction;
                }

                _transaction.AddCustomAttribute(key, value);
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(ex, "Error in AddCustomAttribute");
                }
                catch (Exception)
                {
                    //Swallow the error
                }

            }

            return _transaction;
        }

        public SpanBridgeApi CurrentSpan
        {
            get
            {
                try
                {
                    using (new IgnoreWork())
                    {
                        _apiSupportabilityMetricCounters.Record(ApiMethod.TransactionGetCurrentSpan);
                        var segment = _transaction.CurrentSegment;
                        return new SpanBridgeApi(segment, _apiSupportabilityMetricCounters, _configSvc);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        Log.Error(ex, "Failed to get CurrentSpan");
                    }
                    catch (Exception)
                    {
                        //Swallow the error
                    }
                    return null;
                }
            }
        }

        /// <summary>
        /// Sets a User Id to be associated with this transaction.
        /// </summary>
        /// <param name="userid">The User Id for this transaction.</param>
        public void SetUserId(string userid)
        {

            try
            {
                _apiSupportabilityMetricCounters.Record(ApiMethod.SetUserId);

                _transaction.SetUserId(userid);
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(ex, "Error in SetUserId");
                }
                catch (Exception)
                {
                    //Swallow the error
                }
            }
        }
    }
}
