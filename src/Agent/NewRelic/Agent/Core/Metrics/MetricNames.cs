// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core;
using NewRelic.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace NewRelic.Agent.Core.Metrics
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
            for (var i = 0; i < segments.Length; i++)
            {
                segmentStrings[i] = segments[i]?.ToString() ?? string.Empty;
            }

            return Create(prefix, segmentStrings);
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
                if (this == obj)
                {
                    return true;
                }

                return obj is SimpleMetricName name && _name.Equals(name._name);
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
                _prefix = prefix;
                _segments = segments;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                return obj is MetricNameWithSegments other &&
                    _length == other._length &&
                    _segments.Length == other._segments.Length &&
                    _prefix.Equals(other._prefix) &&
                    _segments.SequenceEqual(other._segments);
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
        public const string Controller = "DotNetController";
        public const string Uri = "Uri";
        public const string NormalizedUri = "NormalizedUri";
        public const string All = "all";
        public const string AllWeb = "allWeb";
        public const string AllOther = "allOther";
        public const string Custom = "Custom";
        public const string OtherTransactionPrefix = "OtherTransaction";
        public const string WebTransactionPrefix = "WebTransaction";
        public const string SupportabilityPayloadsDroppedDueToMaxPayloadLimitPrefix = Supportability + PathSeparator + "DotNet/Collector" + PathSeparator + "MaxPayloadSizeLimit";
        public const string KafkaMessageBrokerConsume = "Consume";

        public static readonly char PathSeparatorChar = PathSeparator[0];
        public static readonly char[] PathSeparatorCharArray = { PathSeparatorChar };

        public static readonly MetricName Dispatcher = MetricName.Create("HttpDispatcher");
        public static readonly MetricName RequestQueueTime = MetricName.Create("WebFrontend/QueueTime");

        private static readonly Func<DatastoreVendor, MetricName> _databaseVendorAll;
        private static readonly Func<DatastoreVendor, MetricName> _databaseVendorAllWeb;
        private static readonly Func<DatastoreVendor, MetricName> _databaseVendorAllOther;
        private static readonly Func<DatastoreVendor, Func<string, MetricName>> _databaseVendorOperations;

        static MetricNames()
        {
            _databaseVendorAll = GetEnumerationFunc<DatastoreVendor, MetricName>(vendor =>
                MetricName.Create(Datastore + PathSeparator + EnumNameCache<DatastoreVendor>.GetName(vendor) + PathSeparator + All));
            _databaseVendorAllWeb = GetEnumerationFunc<DatastoreVendor, MetricName>(vendor =>
                MetricName.Create(Datastore + PathSeparator + EnumNameCache<DatastoreVendor>.GetName(vendor) + PathSeparator + AllWeb));
            _databaseVendorAllOther = GetEnumerationFunc<DatastoreVendor, MetricName>(vendor =>
                MetricName.Create(Datastore + PathSeparator + EnumNameCache<DatastoreVendor>.GetName(vendor) + PathSeparator + AllOther));

            var operations = new HashSet<string>(SqlParser.Operations);
            operations.Add(DatastoreUnknownOperationName);
            _databaseVendorOperations = GetEnumerationFunc<DatastoreVendor, Func<string, MetricName>>(
                vendor =>
                {
                    var dict = new Dictionary<string, MetricName>(operations.Count);
                    var metricNamePrefix = DatastoreOperation + PathSeparator + EnumNameCache<DatastoreVendor>.GetName(vendor) + PathSeparator;
                    foreach (var operation in operations)
                    {
                        dict[operation] = MetricName.Create(metricNamePrefix + operation);
                    }

                    return operation => (dict.TryGetValue(operation, out var name))
                        ? name
                        : MetricName.Create(DatastoreOperation, EnumNameCache<DatastoreVendor>.GetName(vendor), operation);
                });
        }

        
        public static MetricName GetCustom(string suffix)
        {
            return MetricName.Create(Custom, suffix);
        }

        /// <summary>
        /// Returns a func that returns R for a given value of an enum E.
        /// It uses the valueSupplier to compute the values of R and stores them
        /// in an array.
        /// </summary>
        private static Func<TEnum, TResult> GetEnumerationFunc<TEnum, TResult>(Func<TEnum, TResult> valueSupplier)
        {
            var keys = Enum.GetValues(typeof(TEnum));
            var array = new TResult[keys.Length];

            // we can cast the enum to an int
            foreach (var key in keys)
            {
                array[(int)key] = valueSupplier.Invoke((TEnum)key);
            }

            return key => array[(int)(object)key];
        }

        #region Apdex

        public const string ApdexWeb = "Apdex";
        public const string ApdexOther = "ApdexOther/Transaction";

        // Apdex metrics spec: https://newrelic.atlassian.net/wiki/display/eng/OtherTransactions+as+Key+Transactions
        public static readonly MetricName ApdexAll = MetricName.Create("ApdexAll");
        public static readonly MetricName ApdexAllWeb = MetricName.Create("Apdex");
        public static readonly MetricName ApdexAllOther = MetricName.Create("ApdexOther");

        private static string Join(params string[] strings)
        {
            return string.Join(PathSeparator, strings);
        }

        public static MetricName GetApdexAllWebOrOther(bool isWebTransaction)
        {
            return isWebTransaction ? ApdexAllWeb : ApdexAllOther;
        }

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

        #region Errors

        public const string Errors = "Errors";
        public const string ErrorsExpected = "ErrorsExpected";


        public static readonly MetricName ErrorsAll = MetricName.Create(Errors + PathSeparator + All);
        public static readonly MetricName ErrorsAllWeb = MetricName.Create(Errors + PathSeparator + AllWeb);
        public static readonly MetricName ErrorsAllOther = MetricName.Create(Errors + PathSeparator + AllOther);
        public static readonly MetricName ErrorsExpectedAll = MetricName.Create(ErrorsExpected + PathSeparator + All);

        public static MetricName GetErrorTransaction(string transactionMetricName)
        {
            return MetricName.Create(Errors, transactionMetricName);
        }

        #endregion Errors

        #region DotNetInvocation

        private const string DotNetInvocation = "DotNet";

        public static MetricName GetDotNetInvocation(params string[] segments)
        {
            return MetricName.Create(DotNetInvocation, segments);
        }

        #endregion

        #region Transactions

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

        public const string Message = "Message";

        public static readonly MetricName WebTransactionAll = MetricName.Create(WebTransactionPrefix);
        public static readonly MetricName OtherTransactionAll = MetricName.Create(OtherTransactionPrefix + PathSeparator + All);

        #region Total time

        public static readonly MetricName WebTransactionTotalTimeAll = MetricName.Create("WebTransactionTotalTime");
        public static readonly MetricName OtherTransactionTotalTimeAll = MetricName.Create("OtherTransactionTotalTime");

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

        public const string MessageBrokerPrefix = "MessageBroker";
        public const string MessageBrokerNamed = "Named";
        public const string MessageBrokerTemp = "Temp";
        public const string Msmq = "MSMQ";
        public const string Serialization = "Serialization";

        public static MetricName GetMessageBroker(MessageBrokerDestinationType type, MessageBrokerAction action,
            string vendor, string queueName)
        {
            var normalizedType = NormalizeMessageBrokerDestinationTypeForMetricName(type);
            return (queueName != null)
                ? MetricName.Create(MessageBrokerPrefix, vendor, normalizedType, action, MessageBrokerNamed, queueName)
                : MetricName.Create(MessageBrokerPrefix, vendor, normalizedType, action, MessageBrokerTemp);
        }

        public static MetricName GetMessageBrokerSerialization(MessageBrokerDestinationType type, MessageBrokerAction action,
            string vendor, string queueName, string kind)
        {
            var normalizedType = NormalizeMessageBrokerDestinationTypeForMetricName(type);
            return MetricName.Create(MessageBrokerPrefix, vendor, normalizedType, action, MessageBrokerNamed, queueName, Serialization, kind);

        }

        private static MessageBrokerDestinationType NormalizeMessageBrokerDestinationTypeForMetricName(
            MessageBrokerDestinationType type)
        {
            if (type == MessageBrokerDestinationType.TempQueue)
            {
                return MessageBrokerDestinationType.Queue;
            }

            if (type == MessageBrokerDestinationType.TempTopic)
            {
                return MessageBrokerDestinationType.Topic;
            }

            return type;
        }

        private const string KakfaTopic = "Topic";
        private const string KakfaReceived = "Received";
        private const string KakfaMessages = "Messages";
        public static MetricName GetKafkaMessagesReceivedPerConsume(string topic)
        {
            return MetricName.Create(Message, "Kafka", KakfaTopic, MessageBrokerNamed, topic, KakfaReceived, KakfaMessages);
        }

        #endregion MessageBroker

        #region Datastore

        private const string Datastore = "Datastore";
        private const string DatastoreOperation = Datastore + PathSeparator + "operation";
        private const string DatastoreStatement = Datastore + PathSeparator + "statement";
        private const string DatastoreInstance = Datastore + PathSeparator + "instance";

        public const string DatastoreUnknownOperationName = "other";

        public static readonly MetricName DatastoreAll = MetricName.Create(Datastore + PathSeparator + All);
        public static readonly MetricName DatastoreAllWeb = MetricName.Create(Datastore + PathSeparator + AllWeb);
        public static readonly MetricName DatastoreAllOther = MetricName.Create(Datastore + PathSeparator + AllOther);

        public static MetricName GetDatastoreVendorAll(this DatastoreVendor vendor)
        {
            return _databaseVendorAll.Invoke(vendor);
        }

        public static MetricName GetDatastoreVendorAllWeb(this DatastoreVendor vendor)
        {
            return _databaseVendorAllWeb.Invoke(vendor);
        }

        public static MetricName GetDatastoreVendorAllOther(this DatastoreVendor vendor)
        {
            return _databaseVendorAllOther.Invoke(vendor);
        }

        public static MetricName GetDatastoreOperation(this DatastoreVendor vendor, string operation = null)
        {
            operation = operation ?? DatastoreUnknownOperationName;
            return _databaseVendorOperations.Invoke(vendor).Invoke(operation);
        }

        public static MetricName GetDatastoreStatement(DatastoreVendor vendor, string model,
            string operation = null)
        {
            operation = operation ?? DatastoreUnknownOperationName;
            return MetricName.Create(DatastoreStatement, EnumNameCache<DatastoreVendor>.GetName(vendor), model, operation);
        }

        public static MetricName GetDatastoreInstance(DatastoreVendor vendor, string host, string portPathOrId)
        {
            return MetricName.Create(DatastoreInstance, EnumNameCache<DatastoreVendor>.GetName(vendor), host, portPathOrId);
        }

        #endregion Datastore

        #region External

        private const string External = "External";

        public static readonly MetricName ExternalAll = MetricName.Create(External + PathSeparator + All);
        public static readonly MetricName ExternalAllWeb = MetricName.Create(External + PathSeparator + AllWeb);
        public static readonly MetricName ExternalAllOther = MetricName.Create(External + PathSeparator + AllOther);

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

        #region Supportability

        private const string Supportability = "Supportability";
        private const string SupportabilityDotnetPs = Supportability + PathSeparator + "Dotnet" + PathSeparator;
        private const string SupportabilityPs = Supportability + PathSeparator;
        private const string SupportabilityNetFrameworkVersionPs = SupportabilityDotnetPs + "NetFramework" + PathSeparator;
        private const string SupportabilityNetCoreVersionPs = SupportabilityDotnetPs + "NetCore" + PathSeparator;

        // Metrics
        // NOTE: This metric is REQUIRED by the collector (it is used as a heartbeat)
        public const string SupportabilityMetricHarvestTransmit = SupportabilityPs + "MetricHarvest" + PathSeparator + "transmit";

        public static string GetSupportabilityAgentTimingMetric(string suffix)
        {
            return Supportability + PathSeparator + "AgentTiming" + PathSeparator + suffix;
        }

        // RUM
        public const string SupportabilityRumHeaderRendered = SupportabilityPs + "RUM/Header";
        public const string SupportabilityRumFooterRendered = SupportabilityPs + "RUM/Footer";
        public const string SupportabilityHtmlPageRendered = SupportabilityPs + "RUM/HtmlPage";

        // Thread Profiling
        public const string SupportabilityThreadProfilingSampleCount = SupportabilityPs + "ThreadProfiling/SampleCount";

        // Transaction Events
        private const string SupportabilityTransactionEventsPs = SupportabilityPs + "AnalyticsEvents" + PathSeparator;

        //  Note: these two metrics are REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
        public const string SupportabilityTransactionEventsSent = SupportabilityTransactionEventsPs + "TotalEventsSent";
        public const string SupportabilityTransactionEventsSeen = SupportabilityTransactionEventsPs + "TotalEventsSeen";

        public const string SupportabilityTransactionEventsCollected = SupportabilityTransactionEventsPs + "TotalEventsCollected";
        public const string SupportabilityTransactionEventsRecollected = SupportabilityTransactionEventsPs + "TotalEventsRecollected";
        public const string SupportabilityTransactionEventsReservoirResize = SupportabilityTransactionEventsPs + "TryResizeReservoir";

        // Custom Events
        private const string SupportabilityEventsPs = SupportabilityPs + "Events" + PathSeparator;
        private const string SupportabilityCustomEventsPs = SupportabilityEventsPs + "Customer" + PathSeparator;

        // Error Events
        private const string SupportabilityErrorEventsPs = SupportabilityEventsPs + "TransactionError" + PathSeparator;
        public const string SupportabilityErrorEventsSent = SupportabilityErrorEventsPs + "Sent";
        public const string SupportabilityErrorEventsSeen = SupportabilityErrorEventsPs + "Seen";

        // Note: Though not required by APM like the transaction event supportability metrics, these metrics should still be created to maintain consistency
        public const string SupportabilityCustomEventsSent = SupportabilityCustomEventsPs + "Sent";
        public const string SupportabilityCustomEventsSeen = SupportabilityCustomEventsPs + "Seen";

        public const string SupportabilityCustomEventsCollected = SupportabilityCustomEventsPs + "TotalEventsCollected";
        public const string SupportabilityCustomEventsRecollected = SupportabilityCustomEventsPs + "TotalEventsRecollected";
        public const string SupportabilityCustomEventsReservoirResize = SupportabilityCustomEventsPs + "TryResizeReservoir";

        // SQL Trace
        private const string SupportabilitySqlTracesPs = SupportabilityPs + "SqlTraces" + PathSeparator;
        public const string SupportabilitySqlTracesSent = SupportabilitySqlTracesPs + "TotalSqlTracesSent";
        public const string SupportabilitySqlTracesRecollected = SupportabilitySqlTracesPs + "TotalSqlTracesRecollected";

        public const string SupportabilityCachePrefix = SupportabilityPs + "Cache" + PathSeparator;

        public static readonly MetricName SupportabilitySqlTracesCollected =
            MetricName.Create(SupportabilitySqlTracesPs + "TotalSqlTracesCollected");

        // Error Traces
        private const string SupportabilityErrorTracesPs = SupportabilityPs + "Errors" + PathSeparator;
        public const string SupportabilityErrorTracesSent = SupportabilityErrorTracesPs + "TotalErrorsSent";
        public const string SupportabilityErrorTracesCollected = SupportabilityErrorTracesPs + "TotalErrorsCollected";
        public const string SupportabilityErrorTracesRecollected = SupportabilityErrorTracesPs + "TotalErrorsRecollected";

        // Transaction GarbageCollected
        private const string SupportabilityTransactionBuilderGarbageCollectedPs =
            SupportabilityPs + "TransactionBuilderGarbageCollected" + PathSeparator;

        public const string SupportabilityTransactionBuilderGarbageCollectedAll =
            SupportabilityTransactionBuilderGarbageCollectedPs + All;

        //Faster Event Harvest
        //These metric names are used in conjunction with GetSupportabilityName to build the full name
        public const string SupportabilityEventHarvestPs = "EventHarvest" + PathSeparator;
        public const string SupportabilityEventHarvestReportPeriod = SupportabilityEventHarvestPs + "ReportPeriod";
        private const string SupportabilityEventHarvestHarvestLimit = PathSeparator + "HarvestLimit";
        public const string SupportabilityEventHarvestErrorEventHarvestLimit = SupportabilityEventHarvestPs + "ErrorEventData" + SupportabilityEventHarvestHarvestLimit;
        public const string SupportabilityEventHarvestCustomEventHarvestLimit = SupportabilityEventHarvestPs + "CustomEventData" + SupportabilityEventHarvestHarvestLimit;
        public const string SupportabilityEventHarvestTransactionEventHarvestLimit = SupportabilityEventHarvestPs + "AnalyticEventData" + SupportabilityEventHarvestHarvestLimit;

        public static string GetSupportabilityCATConditionMetricName(CATSupportabilityCondition condition)
        {
            if (_catMetricNames.TryGetValue(condition, out var metricName))
            {
                return metricName;
            }

            return SupportabilityCAT + EnumNameCache<CATSupportabilityCondition>.GetName(condition);
        }

        public static string GetSupportabilityName(string metricName)
        {
            if (metricName.StartsWith(Supportability + PathSeparator))
            {
                return metricName;
            }

            return Supportability + PathSeparator + metricName;
        }

        public static string GetSupportabilityDotnetFrameworkVersion(DotnetFrameworkVersion version)
        {
            return SupportabilityNetFrameworkVersionPs + version;
        }

        public static string GetSupportabilityDotnetCoreVersion(DotnetCoreVersion version)
        {
            return SupportabilityNetCoreVersionPs + version;
        }

        private const string SupportabilityAgentVersionPs = SupportabilityPs + "AgentVersion" + PathSeparator;

        public static string GetSupportabilityAgentVersion(string version)
        {
            return SupportabilityAgentVersionPs + version;
        }

        public static string GetSupportabilityAgentVersionByHost(string host, string version)
        {
            return SupportabilityAgentVersionPs + host + PathSeparator + version;
        }

        private const string SupportabilityLibraryVersionPs = SupportabilityPs + "Library" + PathSeparator;

        public static string GetSupportabilityLibraryVersion(string assemblyName, string assemblyVersion)
        {
            return SupportabilityLibraryVersionPs + assemblyName + PathSeparator + assemblyVersion;
        }

        // Utilization
        private const string SupportabilityUtilizationPs = SupportabilityPs + "utilization" + PathSeparator;
        private const string SupportabilityUtilizationBootIdError = SupportabilityUtilizationPs + "boot_id" + PathSeparator + "error";
        private const string SupportabilityUtilizationKubernetesError = SupportabilityUtilizationPs + "kubernetes" + PathSeparator + "error";
        private const string SupportabilityUtilizationAwsError = SupportabilityUtilizationPs + "aws" + PathSeparator + "error";
        private const string SupportabilityUtilizationAzureError = SupportabilityUtilizationPs + "azure" + PathSeparator + "error";
        private const string SupportabilityUtilizationGcpError = SupportabilityUtilizationPs + "gcp" + PathSeparator + "error";
        private const string SupportabilityUtilizationPcfError = SupportabilityUtilizationPs + "pcf" + PathSeparator + "error";

        public static string GetSupportabilityLinuxOs() => SupportabilityPs + "OS" + PathSeparator + "Linux";

        public static string GetSupportabilityBootIdError() => SupportabilityUtilizationBootIdError;

        public static string GetSupportabilityKubernetesUsabilityError() => SupportabilityUtilizationKubernetesError;

        public static string GetSupportabilityAwsUsabilityError() => SupportabilityUtilizationAwsError;

        public static string GetSupportabilityAzureUsabilityError() => SupportabilityUtilizationAzureError;

        public static string GetSupportabilityGcpUsabilityError() => SupportabilityUtilizationGcpError;

        public static string GetSupportabilityPcfUsabilityError() => SupportabilityUtilizationPcfError;

        public static string GetSupportabilityPayloadsDroppedDueToMaxPayloadLimit(string endpoint)
        {
            return SupportabilityPayloadsDroppedDueToMaxPayloadLimitPrefix + PathSeparator + endpoint;
        }

        // Agent health events
        public static string GetSupportabilityAgentHealthEvent(AgentHealthEvent agentHealthEvent,
            string additionalData = null)
        {
            var metricName = SupportabilityPs + agentHealthEvent;
            return (additionalData == null) ? metricName : metricName + PathSeparator + additionalData;
        }

        // Agent features
        private const string SupportabilityFeatureEnabledPs = SupportabilityPs + "FeatureEnabled" + PathSeparator;

        public static string GetSupportabilityFeatureEnabled(string featureName)
        {
            return SupportabilityFeatureEnabledPs + featureName;
        }

        // Agent API
        private const string SupportabilityAgentApiPs = SupportabilityPs + "ApiInvocation" + PathSeparator;

        public static string GetSupportabilityAgentApi(string methodName)
        {
            return SupportabilityAgentApiPs + methodName;
        }

        // CAT
        private const string SupportabilityCAT = SupportabilityPs + "CrossApplicationTracing" + PathSeparator;
        private const string SupportabilityCATRequest = SupportabilityCAT + "Request" + PathSeparator;
        private const string SupportabilityCATRequestCreate = SupportabilityCATRequest + "Create" + PathSeparator;
        private const string SupportabilityCATRequestAccept = SupportabilityCATRequest + "Accept" + PathSeparator;
        private const string SupportabilityCATResponse = SupportabilityCAT + "Response" + PathSeparator;
        private const string SupportabilityCATResponseCreate = SupportabilityCATResponse + "Create" + PathSeparator;
        private const string SupportabilityCATResponseAccept = SupportabilityCATResponse + "Accept" + PathSeparator;

        private static readonly Dictionary<CATSupportabilityCondition, string> _catMetricNames = new Dictionary<CATSupportabilityCondition, string>()
        {
            { CATSupportabilityCondition.Request_Create_Success,   SupportabilityCATRequestCreate + "Success" },
            { CATSupportabilityCondition.Request_Create_Failure,   SupportabilityCATRequestCreate + "Exception" },
            { CATSupportabilityCondition.Request_Create_Failure_XProcID, SupportabilityCATRequestCreate + "Exception/CrossProcessID" },
            { CATSupportabilityCondition.Request_Accept_Success,   SupportabilityCATRequestAccept + "Success" },
            { CATSupportabilityCondition.Request_Accept_Failure,   SupportabilityCATRequestAccept + "Exception" },
            { CATSupportabilityCondition.Request_Accept_Failure_NotTrusted,   SupportabilityCATRequestAccept + "Ignored/NotTrusted" },
            { CATSupportabilityCondition.Request_Accept_Failure_Decode, SupportabilityCATRequestAccept + "Ignored/UnableToDecode" },
            { CATSupportabilityCondition.Request_Accept_Multiple,  SupportabilityCATRequestAccept + "Warning" + PathSeparator + "MultipleAttempts" },
            { CATSupportabilityCondition.Response_Create_Success,  SupportabilityCATResponseCreate + "Success" },
            { CATSupportabilityCondition.Response_Create_Failure,  SupportabilityCATResponseCreate + "Exception" },
            { CATSupportabilityCondition.Response_Create_Failure_XProcID, SupportabilityCATResponseCreate + "Exception/CrossProcessID" },
            { CATSupportabilityCondition.Response_Accept_Success,  SupportabilityCATResponseAccept + "Success" },
            { CATSupportabilityCondition.Response_Accept_Failure,  SupportabilityCATResponseAccept + "Exception" },
            { CATSupportabilityCondition.Response_Accept_MultipleResponses, SupportabilityCATResponseAccept + "Ignored" + PathSeparator + "MultipleAttempts" }
        };


        ///DistributedTracing

        private const string SupportabilityDistributedTracePs = SupportabilityPs + "DistributedTrace" + PathSeparator;

        private const string SupportabilityDistributedTraceAcceptPayloadPs =
            SupportabilityDistributedTracePs + "AcceptPayload" + PathSeparator;

        private const string SupportabilityDistributedTraceCreatePayloadPs =
            SupportabilityDistributedTracePs + "CreatePayload" + PathSeparator;

        private const string SupportabilityDistributedTraceAcceptPayloadIgnoredPs =
            SupportabilityDistributedTraceAcceptPayloadPs + "Ignored" + PathSeparator;

        /// <summary>Created when AcceptPayload was called successfully</summary>
        public const string SupportabilityDistributedTraceAcceptPayloadSuccess =
            SupportabilityDistributedTraceAcceptPayloadPs + "Success";

        /// <summary>Created when AcceptPayload had a generic exception</summary>
        public const string SupportabilityDistributedTraceAcceptPayloadException =
            SupportabilityDistributedTraceAcceptPayloadPs + "Exception";

        /// <summary>Created when AcceptPayload had a parsing exception</summary>
        public const string SupportabilityDistributedTraceAcceptPayloadParseException =
            SupportabilityDistributedTraceAcceptPayloadPs + "ParseException";

        /// <summary>Created when AcceptPayload was ignored because CreatePayload had already been called</summary>
        public const string SupportabilityDistributedTraceAcceptPayloadIgnoredCreateBeforeAccept =
            SupportabilityDistributedTraceAcceptPayloadIgnoredPs + "CreateBeforeAccept";

        /// <summary>Created when AcceptPayload was ignored because AcceptPayload had already been called</summary>
        public const string SupportabilityDistributedTraceAcceptPayloadIgnoredMultiple =
            SupportabilityDistributedTraceAcceptPayloadIgnoredPs + "Multiple";

        /// <summary>Created when AcceptPayload was ignored because the payload's major version was greater than the agent's</summary>
        public const string SupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion =
            SupportabilityDistributedTraceAcceptPayloadIgnoredPs + "MajorVersion";

        /// <summary>Created when AcceptPayload was ignored because the payload was null</summary>
        public const string SupportabilityDistributedTraceAcceptPayloadIgnoredNull =
            SupportabilityDistributedTraceAcceptPayloadIgnoredPs + "Null";

        /// <summary>Created when AcceptPayload was ignored because the payload was untrusted</summary>
        public const string SupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount =
            SupportabilityDistributedTraceAcceptPayloadIgnoredPs + "UntrustedAccount";

        /// <summary>Created when CreateDistributedTracePayload was called successfully</summary>
        public const string SupportabilityDistributedTraceCreatePayloadSuccess =
            SupportabilityDistributedTraceCreatePayloadPs + "Success";

        /// <summary>Created when CreateDistributedTracePayload had a generic exception</summary>
        public const string SupportabilityDistributedTraceCreatePayloadException =
            SupportabilityDistributedTraceCreatePayloadPs + "Exception";

        ///Trace Context

        private const string SupportabilityTraceContextPs = SupportabilityPs + "TraceContext" + PathSeparator;

        private const string SupportabilityTraceContextAcceptPs =
            SupportabilityTraceContextPs + "Accept" + PathSeparator;

        private const string SupportabilityTraceContextCreatePs =
            SupportabilityTraceContextPs + "Create" + PathSeparator;

        private const string SupportabilityTraceContextTraceParentPs =
            SupportabilityTraceContextPs + "TraceParent" + PathSeparator;

        private const string SupportabilityTraceContextTraceStatePs =
            SupportabilityTraceContextPs + "TraceState" + PathSeparator;

        public const string SupportabilityTraceContextAcceptSuccess =
            SupportabilityTraceContextAcceptPs + "Success";

        public const string SupportabilityTraceContextAcceptException =
                    SupportabilityTraceContextAcceptPs + "Exception";

        public const string SupportabilityTraceContextCreateSuccess =
            SupportabilityTraceContextCreatePs + "Success";

        public const string SupportabilityTraceContextCreateException =
                    SupportabilityTraceContextCreatePs + "Exception";

        public const string SupportabilityTraceContextTraceParentParseException =
            SupportabilityTraceContextTraceParentPs + "Parse" + PathSeparator + "Exception";

        public const string SupportabilityTraceContextTraceStateParseException =
            SupportabilityTraceContextTraceStatePs + "Parse" + PathSeparator + "Exception";

        public const string SupportabilityTraceContextTraceStateInvalidNrEntry =
            SupportabilityTraceContextTraceStatePs + "InvalidNrEntry";

        public const string SupportabilityTraceContextTraceStateNoNrEntry =
            SupportabilityTraceContextTraceStatePs + "NoNrEntry";

        //Note the following words from https://source.datanerd.us/agents/agent-specs/blob/master/distributed_tracing/Trace-Context-Payload.md
        //In addition to these metrics agents are encouraged to add more specific metrics to assist in debugging issues parsing the payload. 
        //More detailed parse exception metrics SHOULD start with 'Supportability/TraceContext/Parse/Exception'

        public static string GetSupportabilityErrorHttpStatusCodeFromCollector(HttpStatusCode statusCode)
        {
            return Supportability + PathSeparator + "Agent/Collector/HTTPError" + PathSeparator + (int)statusCode;
        }

        public static string GetSupportabilityEndpointMethodErrorAttempts(string enpointMethod)
        {
            return Supportability + PathSeparator + "Agent/Collector" + PathSeparator + enpointMethod + PathSeparator + "Attempts";
        }

        public static string GetSupportabilityEndpointMethodErrorDuration(string enpointMethod)
        {
            return Supportability + PathSeparator + "Agent/Collector" + PathSeparator + enpointMethod + PathSeparator + "Duration";
        }



        // Install Type
        private const string SupportabilityInstallTypePs = SupportabilityPs + "Dotnet/InstallType" + PathSeparator;

        public static string GetSupportabilityInstallType(string installType)
        {
            return SupportabilityInstallTypePs + installType;
        }

        // AppDomain caching disabled
        public const string SupportabilityAppDomainCachingDisabled = "Supportability/DotNET/AppDomainCaching/Disabled";

        public const string SupportabilityLoggingDisabled = "Supportability/DotNET/AgentLogging/Disabled";
        public const string SupportabilityLoggingFatalError = "Supportability/DotNET/AgentLogging/DisabledDueToError";

        #endregion Supportability

        #region Distributed Trace Metrics

        private const string Unknown = "Unknown";

        //DurationByCaller/parent.type/parent.accountId/parent.appId/transport/all
        private const string DistributedTraceDurationByCallerPs = "DurationByCaller" + PathSeparator;

        //ErrorsByCaller/parent.type/parent.accountId/parent.appId/transport/all
        private const string DistributedTraceErrorsByCallerPs = "ErrorsByCaller" + PathSeparator;

        //TransportDuration/parent.type/parent.accountId/parent.appId/transport/all
        private const string DistributedTraceTransportDurationPs = "TransportDuration" + PathSeparator;

        /// <summary>
        /// Returns the metric name based on metricTag
        /// </summary>
        /// <param name="metricTag">e.g. DistributedTraceDurationByCallerPs or DistributedTraceErrorsByCallerPs or DistributedTraceTransportDurationPs</param>
        /// <param name="type">The type of the parent</param>
        /// <param name="accountId">The account Id of the parent</param>
        /// <param name="app">The app value of the parent</param>
        /// <param name="transport">e.g. http, grpc</param>
        /// <returns>A metric prefix: e.g. DurationByCaller/{parent.type}/{parent.accountId}/{parent.appId}/{transport}/
        /// </returns>
        private static string GetDistributedTraceMetricPrefix(
            string metricTag,
            string type,
            string accountId,
            string app,
            string transport)
        {
            const int ReserveCapacity = 100;
            var sb = new StringBuilder(metricTag, ReserveCapacity)
                .Append(type ?? Unknown).Append(PathSeparatorChar)
                .Append(accountId ?? Unknown).Append(PathSeparatorChar)
                .Append(app ?? Unknown).Append(PathSeparatorChar)
                .Append(transport ?? Unknown).Append(PathSeparatorChar);
            return sb.ToString();
        }

        /// <summary>
        /// Returns the a tuple of two metric names. The first one ends in /all and the second one ends in allWeb
        /// or allOther depending on the input value of isWeb.
        /// </summary>
        /// <param name="type">The type of the parent</param>
        /// <param name="accountId">The account Id of the parent</param>
        /// <param name="app">The app value of the parent</param>
        /// <param name="transport">e.g. http, grpc</param>
        /// <param name="isWeb">if true, creates the allWeb metric name. if false, creates the allOther metric name</param>
        /// <returns>A tuple of two strings: 
        /// <para>DurationByCaller/{parent.type}/{parent.accountId}/{parent.appId}/{transport}/all</para>
        /// and
        /// <para>DurationByCaller/{parent.type}/{parent.accountId}/{parent.appId}/{transport}/{allWeb|allOther}</para>
        /// </returns>
        public static (MetricName all, MetricName webOrOther) GetDistributedTraceDurationByCaller(
            string type,
            string accountId,
            string app,
            string transport,
            bool isWeb)
        {
            var prefix = GetDistributedTraceMetricPrefix(DistributedTraceDurationByCallerPs, type, accountId, app, transport);
            return (all: MetricName.Create(prefix + All), webOrOther: MetricName.Create(prefix + (isWeb ? AllWeb : AllOther)));
        }

        /// <summary>
        /// Returns the a tuple of two metric names. The first one ends in /all and the second one ends in allWeb
        /// or allOther depending on the input value of isWeb.
        /// </summary>
        /// <param name="type">The type of the parent</param>
        /// <param name="accountId">The account Id of the parent</param>
        /// <param name="app">The app value of the parent</param>
        /// <param name="transport">e.g. http, grpc</param>
        /// <param name="isWeb">if true, creates the allWeb metric name. if false, creates the allOther metric name</param>
        /// <returns>A tuple of two strings: 
        /// <para>ErrorsByCaller/{parent.type}/{parent.accountId}/{parent.appId}/{transport}/all</para>
        /// and
        /// <para>ErrorsByCaller/{parent.type}/{parent.accountId}/{parent.appId}/{transport}/{allWeb|allOther}</para>
        /// </returns>
        public static (MetricName all, MetricName webOrOther) GetDistributedTraceErrorsByCaller(
            string type,
            string accountId,
            string app,
            string transport,
            bool isWeb)
        {
            var prefix = GetDistributedTraceMetricPrefix(DistributedTraceErrorsByCallerPs, type, accountId, app, transport);
            return (all: MetricName.Create(prefix + All), webOrOther: MetricName.Create(prefix + (isWeb ? AllWeb : AllOther)));
        }

        /// <summary>
        /// Returns the a tuple of two metric names. The first one ends in /all and the second one ends in allWeb
        /// or allOther depending on the input value of isWeb.
        /// </summary>
        /// <param name="type">The type of the parent</param>
        /// <param name="accountId">The account Id of the parent</param>
        /// <param name="app">The app value of the parent</param>
        /// <param name="transport">e.g. http, grpc</param>
        /// <param name="isWeb">if true, creates the allWeb metric name. if false, creates the allOther metric name</param>
        /// <returns>A tuple of two strings: 
        /// <para>TransportDuration/{parent.type}/{parent.accountId}/{parent.appId}/{transport}/all</para>
        /// and
        /// <para>TransportDuration/{parent.type}/{parent.accountId}/{parent.appId}/{transport}/{allWeb|allOther}</para>
        /// </returns>
        public static (MetricName all, MetricName webOrOther) GetDistributedTraceTransportDuration(
            string type,
            string accountId,
            string app,
            string transport,
            bool isWeb)
        {
            var prefix = GetDistributedTraceMetricPrefix(DistributedTraceTransportDurationPs, type, accountId, app, transport);
            return (all: MetricName.Create(prefix + All), webOrOther: MetricName.Create(prefix + (isWeb ? AllWeb : AllOther)));
        }

        #endregion Distributed Trace Metrics

        #region Span Metrics

        private const string SpanEventsPs = "SpanEvent" + PathSeparator;

        public const string SupportabilitySpanEventsSent = SpanEventsPs + "TotalEventsSent";
        public const string SupportabilitySpanEventsSeen = SpanEventsPs + "TotalEventsSeen";
        public const string SupportabilitySpanEventsLimit = SpanEventsPs + "Limit";


        #endregion Span Metrics

        #region Infinite Tracing Metrics

        private const string SupportabilityInfiniteTracing = "InfiniteTracing" + PathSeparator;
        private const string SupportabilityInfiniteTracingSpan = SupportabilityInfiniteTracing + "Span" + PathSeparator;
        public const string SupportabilityInfiniteTracingSpanResponseError = SupportabilityInfiniteTracingSpan + "Response/Error";
        public const string SupportabilityInfiniteTracingSpanAgentQueueDumped = SupportabilityInfiniteTracingSpan + "AgentQueueDumped";
        public const string SupportabilityInfiniteTracingSpanSeen = SupportabilityInfiniteTracingSpan + "Seen";
        public const string SupportabilityInfiniteTracingSpanSent = SupportabilityInfiniteTracingSpan + "Sent";
        public const string SupportabilityInfiniteTracingSpanSentBatchSize = SupportabilityInfiniteTracingSpan + "BatchSize";
        public const string SupportabilityInfiniteTracingSpanQueueSize = SupportabilityInfiniteTracingSpan + "QueueSize";
        public const string SupportabilityInfiniteTracingSpanReceived = SupportabilityInfiniteTracingSpan + "Received";
        public const string SupportabilityInfiniteTracingSpanDropped = SupportabilityInfiniteTracingSpan + "Dropped";
        public const string SupportabilityInfiniteTracingSpanGrpcTimeout = SupportabilityInfiniteTracingSpan + "gRPC" + PathSeparator + "Timeout";

        public static string SupportabilityInfiniteTracingSpanGrpcError(string error)
        {
            return SupportabilityInfiniteTracingSpan + "gRPC" + PathSeparator + error;
        }

        public static string SupportabilityInfiniteTracingCompression(bool compressionEnabled)
        {
            return SupportabilityInfiniteTracing + "Compression" + PathSeparator + (compressionEnabled? "enabled" : "disabled");
        }

        #endregion

        #region Performance Metrics

        private const string _memoryPrefix = "Memory";
        public const string MemoryPhysical = _memoryPrefix + PathSeparator + "Physical"; // Legacy name from before the other memory metrics were added
        public const string MemoryWorkingSet = _memoryPrefix + PathSeparator + "WorkingSet";
        public const string CpuUserUtilization = "CPU/User/Utilization";
        public const string CpuUserTime = "CPU/User Time";

        public const string DotNetPerfThreadpool = "Threadpool" + PathSeparator;
        public const string DotNetPerfThreadpoolThroughput = DotNetPerfThreadpool + "Throughput" + PathSeparator;

        public static string GetThreadpoolUsageStatsName(ThreadType type, ThreadStatus status)
        {
            return DotNetPerfThreadpool + EnumNameCache<ThreadType>.GetName(type) + PathSeparator + EnumNameCache<ThreadStatus>.GetName(status);
        }

        public static string GetThreadpoolThroughputStatsName(ThreadpoolThroughputStatsType type)
        {
            return DotNetPerfThreadpoolThroughput + EnumNameCache<ThreadpoolThroughputStatsType>.GetName(type);
        }

        private static readonly Dictionary<GCSampleType, string> _gcMetricNames = new Dictionary<GCSampleType, string>
        {
            { GCSampleType.HandlesCount , "GC/Handles" },
            { GCSampleType.InducedCount , "GC/Induced" },
            { GCSampleType.PercentTimeInGc , "GC/PercentTimeInGC" },

            { GCSampleType.Gen0CollectionCount , "GC/Gen0/Collections" },
            { GCSampleType.Gen0Size , "GC/Gen0/Size" },
            { GCSampleType.Gen0Promoted , "GC/Gen0/Promoted" },

            { GCSampleType.Gen1CollectionCount , "GC/Gen1/Collections" },
            { GCSampleType.Gen1Size , "GC/Gen1/Size" },
            { GCSampleType.Gen1Promoted , "GC/Gen1/Promoted" },

            { GCSampleType.Gen2CollectionCount , "GC/Gen2/Collections" },
            { GCSampleType.Gen2Size , "GC/Gen2/Size" },
            { GCSampleType.Gen2Survived, "GC/Gen2/Survived" },

            { GCSampleType.LOHSize , "GC/LOH/Size" },
            { GCSampleType.LOHSurvived, "GC/LOH/Survived" },
        };

        public static string GetGCMetricName(GCSampleType sampleType)
        {
            return _gcMetricNames[sampleType];
        }

        #endregion Performance Metrics

        #region Data Usage Metrics
 
        private const string dataUsageRoot = "Supportability/DotNET/";
        private const string outputBytesDecorator = "/Output/Bytes";

        public static string GetPerDestinationDataUsageMetricName(string destination)
        {
            return dataUsageRoot + destination + outputBytesDecorator;
        }

        public static string GetPerDestinationAreaDataUsageMetricName(string destination, string destinationArea)
        {
            return dataUsageRoot + destination + PathSeparator + destinationArea + outputBytesDecorator;
        }

        #endregion Data Usage Metrics

        #region Log Metrics

        private const string LoggingMetrics = "Logging";
        private const string LoggingMetricsDotnetLines = LoggingMetrics + PathSeparator + "lines";
        private const string LoggingMetricsDotnetDenied = LoggingMetrics + PathSeparator + "denied";
        private const string SupportabilityLoggingEventsPs = SupportabilityPs + "Logging" + PathSeparator;
        public const string SupportabilityLoggingEventsSent = SupportabilityLoggingEventsPs + Forwarding + PathSeparator + "Sent";
        public const string SupportabilityLoggingEventsCollected = SupportabilityLoggingEventsPs + Forwarding + PathSeparator + "Seen";
        public const string SupportabilityLoggingEventsDropped = SupportabilityLoggingEventsPs + Forwarding + PathSeparator + "Dropped";
        public const string SupportabilityLoggingEventEmpty = SupportabilityLoggingEventsPs + Forwarding + PathSeparator + "Empty";

        public static string GetLoggingMetricsLinesBySeverityName(string logLevel)
        {
            return LoggingMetricsDotnetLines + PathSeparator + logLevel;
        }

        public static string GetLoggingMetricsLinesName()
        {
            return LoggingMetricsDotnetLines;
        }

        public static string GetLoggingMetricsDeniedBySeverityName(string logLevel)
        {
            return LoggingMetricsDotnetDenied + PathSeparator + logLevel;
        }

        public static string GetLoggingMetricsDeniedName()
        {
            return LoggingMetricsDotnetDenied;
        }

        private const string Enabled = "enabled";
        private const string Disabled = "disabled";
        private const string Metrics = "Metrics";
        private const string Forwarding = "Forwarding";
        private const string LocalDecorating = "LocalDecorating";
        private const string DotNet = "DotNET";

        private const string SupportabilityLogMetricsConfigPs = SupportabilityLoggingEventsPs + Metrics + PathSeparator + DotNet + PathSeparator;
        private const string SupportabilityLogForwardingConfigPs = SupportabilityLoggingEventsPs + Forwarding + PathSeparator + DotNet + PathSeparator;
        private const string SupportabilityLogDecoratingConfigPs = SupportabilityLoggingEventsPs + LocalDecorating + PathSeparator + DotNet + PathSeparator;

        public static string GetSupportabilityLogMetricsConfiguredName(bool enabled)
        {
            return SupportabilityLogMetricsConfigPs + (enabled ? Enabled : Disabled);
        }

        public static string GetSupportabilityLogForwardingConfiguredName(bool enabled)
        {
            return SupportabilityLogForwardingConfigPs + (enabled ? Enabled : Disabled);
        }

        public static string GetSupportabilityLogDecoratingConfiguredName(bool enabled)
        {
            return SupportabilityLogDecoratingConfigPs + (enabled ? Enabled : Disabled);
        }

        private const string SupportabilityLogFrameworkPs = SupportabilityLoggingEventsPs + DotNet + PathSeparator;

        public static string GetSupportabilityLogFrameworkName(string loggingFramework)
        {
            return SupportabilityLogFrameworkPs + loggingFramework + PathSeparator + Enabled;
        }

        private const string SupportabilityLogForwardingEnabledWithFrameworkNamePs = SupportabilityLoggingEventsPs + Forwarding + PathSeparator + DotNet + PathSeparator;

        public static string GetSupportabilityLogForwardingEnabledWithFrameworkName(string loggingFramework)
        {
            return SupportabilityLogForwardingEnabledWithFrameworkNamePs + loggingFramework + PathSeparator + Enabled;
        }

        #endregion
    }
}
