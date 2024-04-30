// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETFRAMEWORK
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;

namespace NewRelic.Agent.IntegrationTests.WCF
{
    /// <summary>
    /// Depending on which hosting model is used, the test will produce one log (Self Hosted)
    /// or two logs (IIS hosted).  This interface abstracts the functions that access the logs
    /// so that they can be combined as needed.
    /// </summary>
    public interface IWCFLogHelpers
    {
        TransactionSample[] TrxSamples_Client { get; }
        TransactionSample[] TrxSamples_Service { get; }

        TransactionEvent[] TrxEvents { get; }
        TransactionEvent[] TrxEvents_Client { get; }
        TransactionEvent[] TrxEvents_Service { get; }

        ErrorTrace[] ErrorTraces { get; }

        ErrorEventEvents[] ErrorEvents { get; }

        string[] TrxIDs_Client { get; }
        string[] TrxIDs_Service { get; }

        string[] TrxTripIDs_Client { get; }

        Metric[] MetricValues { get; }

        SpanEvent[] SpanEvents_Client { get; }
        SpanEvent[] SpanEvents_Service { get; }

        ConnectResponseData ConnectResponse_Client { get; }
        ConnectResponseData ConnectResponse_Service { get; }

        IEnumerable<T> QueryLog<T>(Func<AgentLogFile, IEnumerable<T>> logFunction);
    }


    public class WCFLogHelpers_IISHosted : IWCFLogHelpers
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        private AgentLogFile _agentLog_Client;
        public AgentLogFile AgentLog_Client
        {
            get
            {
                if (_agentLog_Client == null)
                {
                    var dir = new DirectoryInfo(_fixture.DestinationNewRelicLogFileDirectoryPath);

                    var logFile = dir.EnumerateFileSystemInfos("*.log")
                    .Where(f => !f.Name.StartsWith("NewRelic.Profiler.", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault(f => f.Name.EndsWith("ConsoleMultiFunctionApplicationFW.log", StringComparison.OrdinalIgnoreCase));

                    _agentLog_Client = new AgentLogFile(_fixture.DestinationNewRelicLogFileDirectoryPath, _fixture.TestLogger, logFile.Name);
                }

                return _agentLog_Client;
            }
        }

        private AgentLogFile _agentLog_Service;
        public AgentLogFile AgentLog_Service
        {
            get
            {
                if (_agentLog_Service == null)
                {
                    var dir = new DirectoryInfo(_fixture.DestinationNewRelicLogFileDirectoryPath);

                    var logFile = dir.EnumerateFileSystemInfos("*.log")
                    .Where(f => !f.Name.StartsWith("NewRelic.Profiler.", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault(f => !f.Name.EndsWith("ConsoleMultiFunctionApplicationFW.log", StringComparison.OrdinalIgnoreCase));

                    _agentLog_Service = new AgentLogFile(_fixture.DestinationNewRelicLogFileDirectoryPath, _fixture.TestLogger, logFile.Name);
                }

                return _agentLog_Service;
            }
        }


        public IEnumerable<T> QueryLog<T>(Func<AgentLogFile, IEnumerable<T>> logFunction)
        {
            return logFunction.Invoke(AgentLog_Client)
                .Union(logFunction.Invoke(AgentLog_Service));
        }

        public TransactionSample[] TrxSamples_Client => throw new NotImplementedException();

        private TransactionSample[] _trxSamples_Service;
        public TransactionSample[] TrxSamples_Service => _trxSamples_Service ?? (_trxSamples_Service = AgentLog_Service.GetTransactionSamples().ToArray());

        private TransactionEvent[] _trxEvents;
        public TransactionEvent[] TrxEvents => _trxEvents ?? (_trxEvents = AgentLog_Client.GetTransactionEvents().Union(AgentLog_Service.GetTransactionEvents()).ToArray());

        private TransactionEvent[] _trxEvents_Client;
        public TransactionEvent[] TrxEvents_Client => _trxEvents_Client ?? (_trxEvents_Client = TrxEvents.Where(x => x.IntrinsicAttributes["name"].ToString().Contains("NetFrameworkLibraries.WCF")).ToArray());

        private TransactionEvent[] _trxEvents_Service;
        public TransactionEvent[] TrxEvents_Service => _trxEvents_Service ?? (_trxEvents_Service = TrxEvents.Where(x => x.IntrinsicAttributes["name"].ToString().Contains("Wcf.IWcfService")).ToArray());


        private string[] _trxIDs_Client;
        public string[] TrxIDs_Client => _trxIDs_Client ?? (_trxIDs_Client = TrxEvents_Client
                   .SelectMany(trx => trx.IntrinsicAttributes
                       .Where(attrib => attrib.Key == "nr.guid" || attrib.Key == "guid")
                       .Select(attrib => attrib.Value.ToString()))
                       .Distinct().ToArray());

        /// <summary>
        /// In the event that a CAT call fails, the transaction nr.guid is not properly updated
        /// But, since it was CAT, the guid is also not populated.  In this case, we can use
        /// the nr.tripID to identify the transaction for our testing purposes.
        /// </summary>
        private string[] _trxTripIDs_Client;
        public string[] TrxTripIDs_Client => _trxTripIDs_Client ?? (_trxTripIDs_Client = TrxEvents_Client
                   .SelectMany(trx => trx.IntrinsicAttributes
                       .Where(attrib => attrib.Key == "nr.tripId")
                       .Select(attrib => attrib.Value.ToString()))
                       .Distinct().ToArray());

        private string[] _trxIDs_Service;
        public string[] TrxIDs_Service => _trxIDs_Service ?? (_trxIDs_Service = TrxEvents_Service
                   .SelectMany(trx => trx.IntrinsicAttributes
                       .Where(attrib => attrib.Key == "nr.guid" || attrib.Key == "guid")
                       .Select(attrib => attrib.Value.ToString()))
                       .Distinct().ToArray());


        private Metric[] _metricValues;
        public Metric[] MetricValues => _metricValues ?? (_metricValues =
            ConsolidateMetrics(AgentLog_Client.GetMetrics(), AgentLog_Service.GetMetrics()));

        public Metric[] ConsolidateMetrics(params IEnumerable<Metric>[] metrics)
        {
            var allMetrics = metrics.SelectMany(x => x).ToList();

            var result = new List<Metric>();

            foreach (var metric in allMetrics)
            {
                var matchedMetric = result
                    .Where(x => x.MetricSpec.Name == metric.MetricSpec.Name)
                    .FirstOrDefault(x => x.MetricSpec.Scope == metric.MetricSpec.Scope);

                if (matchedMetric == null)
                {
                    result.Add(metric);
                    continue;
                }

                matchedMetric.Values.Consolidate(metric.Values);
            }

            return result.ToArray();
        }


        private ErrorTrace[] _errorTraces;
        public ErrorTrace[] ErrorTraces => _errorTraces ?? (_errorTraces = AgentLog_Client.GetErrorTraces().Union(AgentLog_Service.GetErrorTraces()).ToArray());


        private ErrorEventEvents[] _errorEvents;
        public ErrorEventEvents[] ErrorEvents => _errorEvents ?? (_errorEvents = AgentLog_Client.GetErrorEvents()
                        .Union(AgentLog_Service.GetErrorEvents()).ToArray());



        private SpanEvent[] _spanEvents;
        public SpanEvent[] SpanEvents => _spanEvents ?? (_spanEvents = AgentLog_Client.GetSpanEvents().Union(AgentLog_Service.GetSpanEvents()).ToArray());

        private SpanEvent[] _spanEvents_Client;
        public SpanEvent[] SpanEvents_Client => _spanEvents_Client ?? (_spanEvents_Client = SpanEvents
                    .Where(x => TrxIDs_Client.Contains(x.IntrinsicAttributes["transactionId"].ToString()))
                    .ToArray());

        private SpanEvent[] _spanEvents_Service;
        public SpanEvent[] SpanEvents_Service => _spanEvents_Service ?? (_spanEvents_Service = SpanEvents
                    .Where(x => TrxIDs_Service.Contains(x.IntrinsicAttributes["transactionId"].ToString()))
                    .ToArray());


        private ConnectResponseData _connectResponse_Client;
        public ConnectResponseData ConnectResponse_Client => _connectResponse_Client ?? (_connectResponse_Client = AgentLog_Client.GetConnectResponseData());


        private ConnectResponseData _connectResponse_Service;
        public ConnectResponseData ConnectResponse_Service => _connectResponse_Service ?? (_connectResponse_Service = AgentLog_Service.GetConnectResponseData());


        public WCFLogHelpers_IISHosted(ConsoleDynamicMethodFixture fixture)
        {
            _fixture = fixture;
        }
    }


    public class WCFLogHelpers_SelfHosted : IWCFLogHelpers
    {
        public WCFLogHelpers_SelfHosted(ConsoleDynamicMethodFixture fixture)
        {
            _fixture = fixture;
        }

        private readonly ConsoleDynamicMethodFixture _fixture;

        public IEnumerable<T> QueryLog<T>(Func<AgentLogFile, IEnumerable<T>> logFunction)
        {
            return logFunction.Invoke(_fixture.AgentLog);
        }

        public TransactionSample[] TrxSamples_Client => throw new NotImplementedException();

        private TransactionSample[] _trxSamples_Service;
        public TransactionSample[] TrxSamples_Service => _trxSamples_Service ?? (_trxSamples_Service = _fixture.AgentLog.GetTransactionSamples().Where(ts => ts.Path.Contains("WcfService")).ToArray());


        private TransactionEvent[] _trxEvents;
        public TransactionEvent[] TrxEvents => _trxEvents ?? (_trxEvents = _fixture.AgentLog.GetTransactionEvents().ToArray());


        private TransactionEvent[] _trxEvents_Client;
        public TransactionEvent[] TrxEvents_Client => _trxEvents_Client ?? (_trxEvents_Client = TrxEvents.Where(x => x.IntrinsicAttributes["name"].ToString().Contains("NetFrameworkLibraries.WCF")).ToArray());


        private string[] _trxIDs_Client;
        public string[] TrxIDs_Client => _trxIDs_Client ?? (_trxIDs_Client = TrxEvents_Client
                   .SelectMany(trx => trx.IntrinsicAttributes
                       .Where(attrib => attrib.Key == "nr.guid" || attrib.Key == "guid")
                       .Select(attrib => attrib.Value.ToString()))
                       .Distinct().ToArray());

        private string[] _trxIDs_Service;
        public string[] TrxIDs_Service => _trxIDs_Service ?? (_trxIDs_Service = TrxEvents_Service
                   .SelectMany(trx => trx.IntrinsicAttributes
                       .Where(attrib => attrib.Key == "nr.guid" || attrib.Key == "guid")
                       .Select(attrib => attrib.Value.ToString()))
                       .Distinct().ToArray());

        /// <summary>
        /// In the event that a CAT call fails, the transaction nr.guid is not properly updated
        /// But, since it was CAT, the guid is also not populated.  In this case, we can use
        /// the nr.tripID to identify the transaction for our testing purposes.
        /// </summary>
        private string[] _trxTripIDs_Client;
        public string[] TrxTripIDs_Client => _trxTripIDs_Client ?? (_trxTripIDs_Client = TrxEvents_Client
                   .SelectMany(trx => trx.IntrinsicAttributes
                       .Where(attrib => attrib.Key == "nr.tripId")
                       .Select(attrib => attrib.Value.ToString()))
                       .Distinct().ToArray());



        private TransactionEvent[] _trxEvents_Service;
        public TransactionEvent[] TrxEvents_Service => _trxEvents_Service ?? (_trxEvents_Service = TrxEvents.Where(x => x.IntrinsicAttributes["name"].ToString().Contains("Wcf.IWcfService")).ToArray());


        private Metric[] _metricValues;
        public Metric[] MetricValues => _metricValues ?? (_metricValues = _fixture.AgentLog.GetMetrics().ToArray());


        private ErrorEventEvents[] _errorEvents;
        public ErrorEventEvents[] ErrorEvents => _errorEvents ?? (_errorEvents = _fixture.AgentLog.GetErrorEvents().ToArray());

        private ErrorTrace[] _errorTraces;
        public ErrorTrace[] ErrorTraces => _errorTraces ?? (_errorTraces = _fixture.AgentLog.GetErrorTraces().ToArray());


        private SpanEvent[] _spanEvents;
        public SpanEvent[] SpanEvents => _spanEvents ?? (_spanEvents = _fixture.AgentLog.GetSpanEvents().ToArray());

        private SpanEvent[] _spanEvents_Client;
        public SpanEvent[] SpanEvents_Client => _spanEvents_Client ?? (_spanEvents_Client = SpanEvents
                    .Where(x => TrxIDs_Client.Contains(x.IntrinsicAttributes["transactionId"].ToString()))
                    .ToArray());

        private SpanEvent[] _spanEvents_Service;
        public SpanEvent[] SpanEvents_Service => _spanEvents_Service ?? (_spanEvents_Service = SpanEvents
                    .Where(x => TrxIDs_Service.Contains(x.IntrinsicAttributes["transactionId"].ToString()))
                    .ToArray());

        private ConnectResponseData[] _connectResponses;
        public ConnectResponseData[] ConnectResponses => _connectResponses ?? (_connectResponses = _fixture.AgentLog.GetConnectResponseDatas().ToArray());

        public ConnectResponseData ConnectResponse_Service => ConnectResponses.FirstOrDefault();

        public ConnectResponseData ConnectResponse_Client => ConnectResponses.FirstOrDefault();
    }

}
#endif
