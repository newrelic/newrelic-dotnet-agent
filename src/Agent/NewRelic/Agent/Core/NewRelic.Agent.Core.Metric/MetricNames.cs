/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;

namespace NewRelic.Agent.Core.Metric
{
    /// <summary>
    /// This holds a representation of a metric name.  It is used to defer string concatenation objects
    /// to a later time.  For instance, when aggregating a transactions metrics we aggregate the same metric name
    /// over and over.  Rather than computing the full string name for the metric we can use this class as the key, 
    /// aggregate all the data under a single MetricName key, then generate the metric string.
    /// 
    /// Subclasses of this class are not designed to implement equality in a perfect way.  For instance, a
    /// MetricName created with "One/Two" will not equal one created with ["One, Two"] even though the values 
    /// returned by ToString() do in fact equal.  This should not be an issue because we generate MetricNames 
    /// through very controlled paths and we should not have any instances in which we generate the same 
    /// metric name string with MetricName instances created through calls to different MetricName.Create methods.
    /// </summary>
    public abstract class MetricName
    {
        private readonly int _hashCode;
        private readonly int _length;

        public static MetricName Create(string prefix, params string[] segments)
        {
            var hashCode = 31 + prefix.GetHashCode();
            var length = segments.Length + prefix.Length;
            foreach (var segment in segments)
            {
                hashCode = hashCode * 31 + segment.GetHashCode();
                length += segment.Length;
            }
            return new MetricNameWithSegments(length, hashCode, prefix, segments);
        }

        public static MetricName Create(string prefix, params object[] segments)
        {
            var segmentStrings = new string[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                segmentStrings[i] = segments[i] == null ? "" : segments[i].ToString();
            }
            return MetricName.Create(prefix, segmentStrings);
        }

        /// <summary>
        /// Creates a MetricName from a string.  We often use this constructor for "constant" style 
        /// metric names for which we want to perform all of the string concatenation once up front.
        /// </summary>
        public static MetricName Create(string name)
        {
            return new SimpleMetricName(name);
        }

        private MetricName(int length, int hashCode)
        {
            _length = length;
            _hashCode = hashCode;
        }

        private class SimpleMetricName : MetricName
        {
            private readonly string _name;

            public SimpleMetricName(string name) : base(name.Length, name.GetHashCode())
            {
                _name = name;
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }

            public override bool Equals(object obj)
            {
                if (this == obj) return true;
                if (obj is SimpleMetricName)
                {
                    return _name.Equals((obj as SimpleMetricName)._name);
                }
                return false;
            }

            public override string ToString()
            {
                return _name;
            }
        }

        private class MetricNameWithSegments : MetricName
        {
            private readonly string _prefix;
            private readonly string[] _segments;

            public MetricNameWithSegments(int length, int hashCode, string prefix, string[] segments) : base(length, hashCode)
            {
                this._prefix = prefix;
                this._segments = segments;
            }

            public override bool Equals(object obj)
            {
                if (this == obj) return true;

                var other = obj as MetricNameWithSegments;

                if (null != other)
                {
                    if (_length != other._length)
                    {
                        return false;
                    }
                    else if (_segments.Length != other._segments.Length)
                    {
                        return false;
                    }
                    if (!_prefix.Equals(other._prefix)) return false;
                    for (int i = 0; i < _segments.Length; i++)
                    {
                        if (!_segments[i].Equals(other._segments[i]))
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }

            public override string ToString()
            {
                var stringBuilder = new StringBuilder(_length).Append(_prefix);
                foreach (var segment in _segments)
                {
                    stringBuilder.Append(MetricNames.PathSeparatorChar).Append(segment);
                }
                return stringBuilder.ToString();
            }
        }
    }

    /// <summary>
    /// An enumeration of standard metric names that the agent can generate.
    /// </summary>
    public static class MetricNames
    {
        public const string PathSeparator = "/";
        public static readonly char PathSeparatorChar = Convert.ToChar(PathSeparator);

        #region Apdex

        // Apdex metrics spec: https://newrelic.atlassian.net/wiki/display/eng/OtherTransactions+as+Key+Transactions
        public readonly static MetricName ApdexAll = MetricName.Create("ApdexAll");
        public readonly static MetricName ApdexAllWeb = MetricName.Create("Apdex");
        public readonly static MetricName ApdexAllOther = MetricName.Create("ApdexOther");

        private static string Join(params string[] strings)
        {
            return string.Join(PathSeparator, strings);
        }
        public static MetricName GetApdexAllWebOrOther(bool isWebTransaction)
        {
            return isWebTransaction ? ApdexAllWeb : ApdexAllOther;
        }

        public const string ApdexWeb = "Apdex";
        public const string ApdexOther = "ApdexOther/Transaction";

        /// <summary>
        /// Takes a transaction metric name and returns an appropriate apdex metric name. For example, WebTransaction/MVC/MyApp becomes Apdex/MVC/MyApp.
        /// </summary>
        /// <param name="transactionMetricName">The transaction metric name. Must be a valid transaction metric name.</param>
        /// <returns>An apdex metric name.</returns>
        public static string GetTransactionApdex(TransactionMetricName transactionMetricName)
        {
            var apdexPrefix = transactionMetricName.IsWebTransactionName ? ApdexWeb : ApdexOther;
            return Join(apdexPrefix, transactionMetricName.UnPrefixedName);
        }

        #endregion Apdex

        public readonly static MetricName Dispatcher = MetricName.Create("HttpDispatcher");

        #region Errors

        public const string Errors = "Errors";
        public readonly static MetricName ErrorsAll = MetricName.Create(Errors + PathSeparator + All);
        public readonly static MetricName ErrorsAllWeb = MetricName.Create(Errors + PathSeparator + AllWeb);
        public readonly static MetricName ErrorsAllOther = MetricName.Create(Errors + PathSeparator + AllOther);
        public static MetricName GetErrorTransaction(string transactionMetricName)
        {
            return MetricName.Create(Errors, transactionMetricName);
        }

        #endregion Errors

        public const string OtherTransactionPrefix = "OtherTransaction";
        public const string WebTransactionPrefix = "WebTransaction";

        public readonly static MetricName RequestQueueTime = MetricName.Create("WebFrontend/QueueTime");

        public const string Controller = "DotNetController";
        public const string Uri = "Uri";
        public const string NormalizedUri = "NormalizedUri";

        public const string All = "all";
        public const string AllWeb = "allWeb";
        public const string AllOther = "allOther";

        public const string Custom = "Custom";

        private static readonly string[] databaseVendorNames = Enum.GetNames(typeof(DatastoreVendor));
        private static readonly Func<DatastoreVendor, MetricName> databaseVendorAll;
        private static readonly Func<DatastoreVendor, MetricName> databaseVendorAllWeb;
        private static readonly Func<DatastoreVendor, MetricName> databaseVendorAllOther;
        private static readonly Func<DatastoreVendor, Func<string, MetricName>> databaseVendorOperations;

        static MetricNames()
        {
            databaseVendorAll = GetEnumerationFunc<DatastoreVendor, MetricName>(vendor => MetricName.Create(Datastore + PathSeparator + vendor + PathSeparator + All));
            databaseVendorAllWeb = GetEnumerationFunc<DatastoreVendor, MetricName>(vendor => MetricName.Create(Datastore + PathSeparator + vendor + PathSeparator + AllWeb));
            databaseVendorAllOther = GetEnumerationFunc<DatastoreVendor, MetricName>(vendor => MetricName.Create(Datastore + PathSeparator + vendor + PathSeparator + AllOther));

            var operations = new HashSet<string>(SqlParser.Operations);
            operations.Add(DatastoreUnknownOperationName);
            databaseVendorOperations = GetEnumerationFunc<DatastoreVendor, Func<string, MetricName>>(
                vendor =>
                {
                    var dict = new Dictionary<string, MetricName>();
                    foreach (var operation in operations)
                    {
                        dict[operation] = MetricName.Create(DatastoreOperation + PathSeparator + ToString(vendor) + PathSeparator + operation);
                    }
                    return operation =>
                    {
                        if (dict.TryGetValue(operation, out MetricName name))
                        {
                            return name;
                        }
                        return MetricName.Create(DatastoreOperation, ToString(vendor), operation);
                    };
                });
        }

        /// <summary>
        /// Returns a func that returns R for a given value of an enum E.
        /// It uses the valueSupplier to compute the values of R and stores them
        /// in an array.
        /// </summary>
        private static Func<E, R> GetEnumerationFunc<E, R>(Func<E, R> valueSupplier)
        {
            var keys = Enum.GetValues(typeof(E));
            var array = new R[keys.Length];

            // we can cast the enum to an int
            foreach (var key in keys)
            {
                array[(int)key] = valueSupplier.Invoke((E)key);
            }

            return key =>
            {
                return array[(int)(object)key];
            };
        }
        public static MetricName GetCustom(string suffix)
        {
            return MetricName.Create(Custom, suffix);
        }

        #region DotNetInvocation

        private const string DotNetInvocation = "DotNet";
        public static MetricName GetDotNetInvocation(params string[] segments)
        {
            return MetricName.Create(DotNetInvocation, segments);
        }

        #endregion

        #region Transactions

        public readonly static MetricName WebTransactionAll = MetricName.Create(WebTransactionPrefix);
        public readonly static MetricName OtherTransactionAll = MetricName.Create(OtherTransactionPrefix + PathSeparator + All);

        public static TransactionMetricName WebTransaction(string category, string name)
        {
            var unprefixedName = Join(category, name);
            return new TransactionMetricName(WebTransactionPrefix, unprefixedName);
        }

        public static TransactionMetricName UriTransaction(string uri)
        {
            var unprefixedName = Join("Uri", uri);
            return new TransactionMetricName(WebTransactionPrefix, unprefixedName);
        }

        public static TransactionMetricName OtherTransaction(string category, string name)
        {
            var unprefixedName = Join(category, name);
            return new TransactionMetricName(OtherTransactionPrefix, unprefixedName);
        }

        public static TransactionMetricName CustomTransaction(string name, bool isWeb)
        {
            var unprefixedName = Join(Custom, name);
            var prefix = isWeb ? WebTransactionPrefix : OtherTransactionPrefix;
            return new TransactionMetricName(prefix, unprefixedName);
        }

        public static TransactionMetricName MessageBrokerTransaction(string type, string vendor, string name)
        {
            var unprefixedName = (name != null)
                ? $"Message/{vendor}/{type}/Named/{name}"
                : $"Message/{vendor}/{type}/Temp";
            return new TransactionMetricName(OtherTransactionPrefix, unprefixedName);
        }

        public enum WebTransactionType
        {
            Action,
            Custom,
            ASP,
            MVC,
            WCF,
            WebAPI,
            WebService,
            MonoRail,
            OpenRasta
        }

        #region Total time

        public readonly static MetricName WebTransactionTotalTimeAll = MetricName.Create("WebTransactionTotalTime");
        public readonly static MetricName OtherTransactionTotalTimeAll = MetricName.Create("OtherTransactionTotalTime");
        public static MetricName TransactionTotalTime(TransactionMetricName transactionMetricName)
        {
            var prefix = transactionMetricName.IsWebTransactionName ? WebTransactionTotalTimeAll : OtherTransactionTotalTimeAll;
            return MetricName.Create(prefix.ToString(), transactionMetricName.UnPrefixedName);
        }

        #endregion Total time

        #region CPU time

        private const string CpuTimePrefix = "CPU";

        public const string WebTransactionCpuTimeAll = CpuTimePrefix + PathSeparator + WebTransactionPrefix;
        public const string OtherTransactionCpuTimeAll = CpuTimePrefix + PathSeparator + OtherTransactionPrefix;
        public static string TransactionCpuTime(TransactionMetricName transactionMetricName)
        {
            var prefix = transactionMetricName.IsWebTransactionName ? WebTransactionCpuTimeAll : OtherTransactionCpuTimeAll;
            return Join(prefix, transactionMetricName.UnPrefixedName);
        }

        #endregion CPU time

        #endregion

        #region MessageBroker

        private const string MessageBrokerPrefix = "MessageBroker";
        private const string MessageBrokerNamed = "Named";
        private const string MessageBrokerTemp = "Temp";

        public const string Msmq = "MSMQ";
        public static MetricName GetMessageBroker(MessageBrokerDestinationType type, MessageBrokerAction action, string vendor, string queueName)
        {
            var normalizedType = NormalizeMessageBrokerDestinationTypeForMetricName(type);
            return (queueName != null)
                ? MetricName.Create(MessageBrokerPrefix, vendor, normalizedType, action, MessageBrokerNamed, queueName)
                : MetricName.Create(MessageBrokerPrefix, vendor, normalizedType, action, MessageBrokerTemp);
        }

        private static MessageBrokerDestinationType NormalizeMessageBrokerDestinationTypeForMetricName(MessageBrokerDestinationType type)
        {
            if (type == MessageBrokerDestinationType.TempQueue)
                return MessageBrokerDestinationType.Queue;
            if (type == MessageBrokerDestinationType.TempTopic)
                return MessageBrokerDestinationType.Topic;
            return type;
        }

        public enum MessageBrokerDestinationType
        {
            Queue,
            Topic,
            TempQueue,
            TempTopic
        }

        public enum MessageBrokerAction
        {
            Produce,
            Consume,
            Peek,
            Purge,
        }

        #endregion MessageBroker

        #region Datastore

        private const string Datastore = "Datastore";
        public readonly static MetricName DatastoreAll = MetricName.Create(Datastore + PathSeparator + All);
        public readonly static MetricName DatastoreAllWeb = MetricName.Create(Datastore + PathSeparator + AllWeb);
        public readonly static MetricName DatastoreAllOther = MetricName.Create(Datastore + PathSeparator + AllOther);
        private const string DatastoreOperation = Datastore + PathSeparator + "operation";
        private const string DatastoreStatement = Datastore + PathSeparator + "statement";
        private const string DatastoreInstance = Datastore + PathSeparator + "instance";
        public const string DatastoreUnknownOperationName = "other";
        public static string ToString(DatastoreVendor vendor)
        {
            return databaseVendorNames[(int)vendor];
        }
        public static MetricName GetDatastoreVendorAll(this DatastoreVendor vendor)
        {
            return databaseVendorAll.Invoke(vendor);
        }
        public static MetricName GetDatastoreVendorAllWeb(this DatastoreVendor vendor)
        {
            return databaseVendorAllWeb.Invoke(vendor);
        }
        public static MetricName GetDatastoreVendorAllOther(this DatastoreVendor vendor)
        {
            return databaseVendorAllOther.Invoke(vendor);
        }
        public static MetricName GetDatastoreOperation(this DatastoreVendor vendor, string operation = null)
        {
            operation = operation ?? DatastoreUnknownOperationName;
            return databaseVendorOperations.Invoke(vendor).Invoke(operation);
        }
        public static MetricName GetDatastoreStatement(DatastoreVendor vendor, string model, string operation = null)
        {
            operation = operation ?? DatastoreUnknownOperationName;
            return MetricName.Create(DatastoreStatement, ToString(vendor), model, operation);
        }
        public static MetricName GetDatastoreInstance(DatastoreVendor vendor, string host, string portPathOrId)
        {
            return MetricName.Create(DatastoreInstance, ToString(vendor), host, portPathOrId);
        }


        #endregion Datastore

        #region External

        private const string External = "External";
        public readonly static MetricName ExternalAll = MetricName.Create(External + PathSeparator + All);
        public readonly static MetricName ExternalAllWeb = MetricName.Create(External + PathSeparator + AllWeb);
        public readonly static MetricName ExternalAllOther = MetricName.Create(External + PathSeparator + AllOther);
        public static MetricName GetExternalHostRollup(string host)
        {
            return MetricName.Create(External, host, All);
        }
        public static MetricName GetExternalHost(string host, string library, string operation = null)
        {
            return operation != null
                ? MetricName.Create(External, host, library, operation)
                : MetricName.Create(External, host, library);
        }
        public static MetricName GetExternalErrors(string server)
        {
            return MetricName.Create(External, server, "errors");
        }
        public static MetricName GetClientApplication(string crossProcessId)
        {
            return MetricName.Create("ClientApplication", crossProcessId, All);
        }
        public static MetricName GetExternalApp(string host, string crossProcessId)
        {
            return MetricName.Create("ExternalApp", host, crossProcessId, All);
        }
        public static MetricName GetExternalTransaction(string host, string crossProcessId, string transactionName)
        {
            return MetricName.Create("ExternalTransaction", host, crossProcessId, transactionName);
        }

        #endregion External

        #region Hardware

        public const string MemoryPhysical = "Memory/Physical";
        public const string CpuUserUtilization = "CPU/User/Utilization";
        public const string CpuUserTime = "CPU/User Time";

        #endregion Hardware

        #region Supportability

        public const string Supportability = "Supportability";
        private const string SupportabilityDotnetPs = Supportability + PathSeparator + "Dotnet" + PathSeparator;

        private const string SupportabilityAgentVersion = Supportability + PathSeparator + "AgentVersion";
        private const string SupportabilityNetFrameworkVersionPs = SupportabilityDotnetPs + "NetFramework" + PathSeparator;

        public static string GetSupportabilityDotnetVersion(string version)
        {
            return SupportabilityNetFrameworkVersionPs + version;
        }
        public static string GetSupportabilityAgentVersion(string version)
        {
            return SupportabilityAgentVersion + PathSeparator + version;
        }
        public static string GetSupportabilityAgentVersionByHost(string host, string version)
        {
            return SupportabilityAgentVersion + PathSeparator + host + PathSeparator + version;
        }
        public static string GetSupportabilityLinuxOs()
        {
            return Supportability + PathSeparator + "OS" + PathSeparator + "Linux";
        }
        public static string GetSupportabilityBootIdError()
        {
            return Supportability + PathSeparator + "utilization" + PathSeparator + "boot_id" + PathSeparator + "error";
        }

        // Metrics
        // NOTE: This metric is REQUIRED by the collector (it is used as a heartbeat)
        public const string SupportabilityMetricHarvestTransmit = Supportability + PathSeparator + "MetricHarvest" + PathSeparator + "transmit";

        // RUM
        public const string SupportabilityRumHeaderRendered = Supportability + PathSeparator + "RUM/Header";
        public const string SupportabilityRumFooterRendered = Supportability + PathSeparator + "RUM/Footer";
        public const string SupportabilityHtmlPageRendered = Supportability + PathSeparator + "RUM/HtmlPage";

        // Thread Profiling
        public const string SupportabilityThreadProfilingSampleCount = Supportability + PathSeparator + "ThreadProfiling/SampleCount";

        // Transaction Events
        private const string SupportabilityTransactionEvents = Supportability + PathSeparator + "AnalyticsEvents";

        //  Note: these two metrics are REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
        public const string SupportabilityTransactionEventsSent = SupportabilityTransactionEvents + PathSeparator + "TotalEventsSent";
        public const string SupportabilityTransactionEventsSeen = SupportabilityTransactionEvents + PathSeparator + "TotalEventsSeen";

        public const string SupportabilityTransactionEventsCollected = SupportabilityTransactionEvents + PathSeparator + "TotalEventsCollected";
        public const string SupportabilityTransactionEventsRecollected = SupportabilityTransactionEvents + PathSeparator + "TotalEventsRecollected";
        public const string SupportabilityTransactionEventsReservoirResize = SupportabilityTransactionEvents + PathSeparator + "TryResizeReservoir";

        // Custom Events
        private const string SupportabilityCustomEvents = Supportability + PathSeparator + "Events" + PathSeparator + "Customer";

        // Error Events
        private const string SupportabilityErrorEvents = Supportability + PathSeparator + "Events" + PathSeparator + "TransactionError";

        public const string SupportabilityErrorEventsSent = SupportabilityErrorEvents + PathSeparator + "Sent";
        public const string SupportabilityErrorEventsSeen = SupportabilityErrorEvents + PathSeparator + "Seen";

        // Note: Though not required by APM like the transaction event supportability metrics, these metrics should still be created to maintain consistency
        public const string SupportabilityCustomEventsSent = SupportabilityCustomEvents + PathSeparator + "Sent";
        public const string SupportabilityCustomEventsSeen = SupportabilityCustomEvents + PathSeparator + "Seen";

        public const string SupportabilityCustomEventsCollected = SupportabilityCustomEvents + PathSeparator + "TotalEventsCollected";
        public const string SupportabilityCustomEventsRecollected = SupportabilityCustomEvents + PathSeparator + "TotalEventsRecollected";
        public const string SupportabilityCustomEventsReservoirResize = SupportabilityCustomEvents + PathSeparator + "TryResizeReservoir";

        // SQL Trace
        private const string SupportabilitySqlTraces = Supportability + PathSeparator + "SqlTraces";
        public const string SupportabilitySqlTracesSent = SupportabilitySqlTraces + PathSeparator + "TotalSqlTracesSent";
        public readonly static MetricName SupportabilitySqlTracesCollected = MetricName.Create(SupportabilitySqlTraces + PathSeparator + "TotalSqlTracesCollected");
        public const string SupportabilitySqlTracesRecollected = SupportabilitySqlTraces + PathSeparator + "TotalSqlTracesRecollected";

        // Error Traces
        private const string SupportabilityErrorTraces = Supportability + PathSeparator + "Errors";
        public const string SupportabilityErrorTracesSent = SupportabilityErrorTraces + PathSeparator + "TotalErrorsSent";
        public const string SupportabilityErrorTracesCollected = SupportabilityErrorTraces + PathSeparator + "TotalErrorsCollected";
        public const string SupportabilityErrorTracesRecollected = SupportabilityErrorTraces + PathSeparator + "TotalErrorsRecollected";

        // Transaction GarbageCollected
        private const string SupportabilityTransactionBuilderGarbageCollectedPrefix = Supportability + PathSeparator + "TransactionBuilderGarbageCollected";
        public const string SupportabilityTransactionBuilderGarbageCollectedAll = SupportabilityTransactionBuilderGarbageCollectedPrefix + PathSeparator + All;

        // Agent health events
        public static string GetSupportabilityAgentHealthEvent(AgentHealthEvent agentHealthEvent, string additionalData = null)
        {
            return additionalData == null
                ? Supportability + PathSeparator + agentHealthEvent
                : Supportability + PathSeparator + agentHealthEvent + PathSeparator + additionalData;
        }

        // Agent features
        private const string SupportabilityFeatureEnabled = Supportability + PathSeparator + "FeatureEnabled";
        public static string GetSupportabilityFeatureEnabled(string featureName)
        {
            return SupportabilityFeatureEnabled + PathSeparator + featureName;
        }

        // Agent API
        public const string SupportabilityAgentApi = "Supportability/ApiInvocation";
        public static string GetSupportabilityAgentApi(string methodName)
        {
            return SupportabilityAgentApi + PathSeparator + methodName;
        }

        #endregion Supportability
    }
}
