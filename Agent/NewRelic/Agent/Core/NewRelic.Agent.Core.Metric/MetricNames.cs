using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

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

		public static MetricName Create([NotNull] string prefix, [NotNull] params string[] segments)
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

		public static MetricName Create([NotNull] string prefix, [NotNull] params object[] segments)
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
		public static MetricName Create([NotNull] string name)
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
		public const char PathSeparatorChar = '/';

		#region Apdex

		// Apdex metrics spec: https://newrelic.atlassian.net/wiki/display/eng/OtherTransactions+as+Key+Transactions
		public static readonly MetricName ApdexAll = MetricName.Create("ApdexAll");
		public static readonly MetricName ApdexAllWeb = MetricName.Create("Apdex");
		public static readonly MetricName ApdexAllOther = MetricName.Create("ApdexOther");

		private static string Join(params string[] strings)
		{
			return String.Join(PathSeparator, strings);
		}

		[NotNull]
		public static MetricName GetApdexAllWebOrOther(Boolean isWebTransaction)
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
		[NotNull]
		public static string GetTransactionApdex(TransactionMetricName transactionMetricName)
		{
			var apdexPrefix = transactionMetricName.IsWebTransactionName ? ApdexWeb : ApdexOther;
			return Join(apdexPrefix, transactionMetricName.UnPrefixedName);
		}

		#endregion Apdex

		public static readonly MetricName Dispatcher = MetricName.Create("HttpDispatcher");

		#region Errors

		public const string Errors = "Errors";
		public static readonly MetricName ErrorsAll = MetricName.Create(Errors + PathSeparator + All);
		public static readonly MetricName ErrorsAllWeb = MetricName.Create(Errors + PathSeparator + AllWeb);
		public static readonly MetricName ErrorsAllOther = MetricName.Create(Errors + PathSeparator + AllOther);

		[NotNull]
		public static MetricName GetErrorTransaction([NotNull] string transactionMetricName)
		{
			return MetricName.Create(Errors, transactionMetricName);
		}

		#endregion Errors

		public const string OtherTransactionPrefix = "OtherTransaction";
		public const string WebTransactionPrefix = "WebTransaction";

		public static readonly MetricName RequestQueueTime = MetricName.Create("WebFrontend/QueueTime");

		public const string Controller = "DotNetController";
		public const string Uri = "Uri";
		public const string NormalizedUri = "NormalizedUri";

		public const string All = "all";
		public const string AllWeb = "allWeb";
		public const string AllOther = "allOther";

		public const string Custom = "Custom";

		private static readonly string[] DatabaseVendorNames = Enum.GetNames(typeof(DatastoreVendor));
		private static readonly Func<DatastoreVendor, MetricName> DatabaseVendorAll;
		private static readonly Func<DatastoreVendor, MetricName> DatabaseVendorAllWeb;
		private static readonly Func<DatastoreVendor, MetricName> DatabaseVendorAllOther;
		private static readonly Func<DatastoreVendor, Func<string, MetricName>> DatabaseVendorOperations;

		static MetricNames()
		{
			DatabaseVendorAll = GetEnumerationFunc<DatastoreVendor, MetricName>(vendor =>
				MetricName.Create(Datastore + PathSeparator + vendor + PathSeparator + All));
			DatabaseVendorAllWeb = GetEnumerationFunc<DatastoreVendor, MetricName>(vendor =>
				MetricName.Create(Datastore + PathSeparator + vendor + PathSeparator + AllWeb));
			DatabaseVendorAllOther = GetEnumerationFunc<DatastoreVendor, MetricName>(vendor =>
				MetricName.Create(Datastore + PathSeparator + vendor + PathSeparator + AllOther));

			var operations = new HashSet<string>(SqlParser.Operations);
			operations.Add(DatastoreUnknownOperationName);
			DatabaseVendorOperations = GetEnumerationFunc<DatastoreVendor, Func<string, MetricName>>(
				vendor =>
				{
					var dict = new Dictionary<string, MetricName>(operations.Count);
					var metricNamePrefix = DatastoreOperation + PathSeparator + GetCachedVendorNameString(vendor) + PathSeparator;
					foreach (var operation in operations)
					{
						dict[operation] = MetricName.Create(metricNamePrefix + operation);
					}

					return operation => (dict.TryGetValue(operation, out var name))
						? name
						: MetricName.Create(DatastoreOperation, GetCachedVendorNameString(vendor), operation);
				});
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
				array[(int) key] = valueSupplier.Invoke((TEnum) key);
			}

			return key => array[(int) (object) key];
		}

		[NotNull]
		public static MetricName GetCustom([NotNull] string suffix)
		{
			return MetricName.Create(Custom, suffix);
		}

		#region DotNetInvocation

		private const string DotNetInvocation = "DotNet";

		[NotNull]
		public static MetricName GetDotNetInvocation([NotNull] params string[] segments)
		{
			return MetricName.Create(DotNetInvocation, segments);
		}

		#endregion

		#region Transactions

		public static readonly MetricName WebTransactionAll = MetricName.Create(WebTransactionPrefix);

		public static readonly MetricName OtherTransactionAll =
			MetricName.Create(OtherTransactionPrefix + PathSeparator + All);

		public static TransactionMetricName WebTransaction([NotNull] string category, [NotNull] string name)
		{
			var unprefixedName = Join(category, name);
			return new TransactionMetricName(WebTransactionPrefix, unprefixedName);
		}

		public static TransactionMetricName UriTransaction([NotNull] string uri)
		{
			var unprefixedName = Join("Uri", uri);
			return new TransactionMetricName(WebTransactionPrefix, unprefixedName);
		}

		public static TransactionMetricName OtherTransaction([NotNull] string category, [NotNull] string name)
		{
			var unprefixedName = Join(category, name);
			return new TransactionMetricName(OtherTransactionPrefix, unprefixedName);
		}

		public static TransactionMetricName CustomTransaction([NotNull] string name, bool isWeb)
		{
			var unprefixedName = Join(Custom, name);
			var prefix = isWeb ? WebTransactionPrefix : OtherTransactionPrefix;
			return new TransactionMetricName(prefix, unprefixedName);
		}

		private const string MessagePs = "Message" + PathSeparator;
		private const string PsNamedPs = PathSeparator + MessageBrokerNamed + PathSeparator;
		private const string PsTemp = PathSeparator + MessageBrokerTemp;

		public static TransactionMetricName MessageBrokerTransaction([NotNull] string type, [NotNull] string vendor,
			[CanBeNull] string name)
		{
			//Message/{vendor}/{type}
			var unprefixedName = new StringBuilder(MessagePs).Append(vendor).Append(PathSeparator).Append(type);
			if (name != null)
			{
				//Message/{vendor}/{type}/Named/{name}
				unprefixedName.Append(PsNamedPs).Append(name);
			}
			else
			{
				//Message/{vendor}/{type}/Temp
				unprefixedName.Append(PsTemp);
			}

			return new TransactionMetricName(OtherTransactionPrefix, unprefixedName.ToString());
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

		public static readonly MetricName WebTransactionTotalTimeAll = MetricName.Create("WebTransactionTotalTime");
		public static readonly MetricName OtherTransactionTotalTimeAll = MetricName.Create("OtherTransactionTotalTime");

		[NotNull]
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

		[NotNull]
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

		[NotNull]
		public static MetricName GetMessageBroker(MessageBrokerDestinationType type, MessageBrokerAction action,
			[NotNull] string vendor, [CanBeNull] string queueName)
		{
			var normalizedType = NormalizeMessageBrokerDestinationTypeForMetricName(type);
			return (queueName != null)
				? MetricName.Create(MessageBrokerPrefix, vendor, normalizedType, action, MessageBrokerNamed, queueName)
				: MetricName.Create(MessageBrokerPrefix, vendor, normalizedType, action, MessageBrokerTemp);
		}

		private static MessageBrokerDestinationType NormalizeMessageBrokerDestinationTypeForMetricName(
			MessageBrokerDestinationType type)
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
		public static readonly MetricName DatastoreAll = MetricName.Create(Datastore + PathSeparator + All);
		public static readonly MetricName DatastoreAllWeb = MetricName.Create(Datastore + PathSeparator + AllWeb);
		public static readonly MetricName DatastoreAllOther = MetricName.Create(Datastore + PathSeparator + AllOther);
		private const string DatastoreOperation = Datastore + PathSeparator + "operation";
		private const string DatastoreStatement = Datastore + PathSeparator + "statement";
		private const string DatastoreInstance = Datastore + PathSeparator + "instance";
		public const string DatastoreUnknownOperationName = "other";

		[NotNull, Pure]
		public static string GetCachedVendorNameString(DatastoreVendor vendor)
		{
			return DatabaseVendorNames[(int) vendor];
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreVendorAll(this DatastoreVendor vendor)
		{
			return DatabaseVendorAll.Invoke(vendor);
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreVendorAllWeb(this DatastoreVendor vendor)
		{
			return DatabaseVendorAllWeb.Invoke(vendor);
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreVendorAllOther(this DatastoreVendor vendor)
		{
			return DatabaseVendorAllOther.Invoke(vendor);
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreOperation(this DatastoreVendor vendor, string operation = null)
		{
			operation = operation ?? DatastoreUnknownOperationName;
			return DatabaseVendorOperations.Invoke(vendor).Invoke(operation);
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreStatement(DatastoreVendor vendor, [NotNull] string model,
			string operation = null)
		{
			operation = operation ?? DatastoreUnknownOperationName;
			return MetricName.Create(DatastoreStatement, GetCachedVendorNameString(vendor), model, operation);
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreInstance(DatastoreVendor vendor, string host, string portPathOrId)
		{
			return MetricName.Create(DatastoreInstance, GetCachedVendorNameString(vendor), host, portPathOrId);
		}


		#endregion Datastore

		#region External

		private const string External = "External";
		public static readonly MetricName ExternalAll = MetricName.Create(External + PathSeparator + All);
		public static readonly MetricName ExternalAllWeb = MetricName.Create(External + PathSeparator + AllWeb);
		public static readonly MetricName ExternalAllOther = MetricName.Create(External + PathSeparator + AllOther);

		[NotNull]
		public static MetricName GetExternalHostRollup([NotNull] string host)
		{
			return MetricName.Create(External, host, All);
		}

		[NotNull]
		public static MetricName GetExternalHost([NotNull] string host, [NotNull] string library,
			[CanBeNull] string operation = null)
		{
			return operation != null
				? MetricName.Create(External, host, library, operation)
				: MetricName.Create(External, host, library);
		}

		[NotNull]
		public static MetricName GetExternalErrors([NotNull] string server)
		{
			return MetricName.Create(External, server, "errors");
		}

		[NotNull]
		public static MetricName GetClientApplication([NotNull] string crossProcessId)
		{
			return MetricName.Create("ClientApplication", crossProcessId, All);
		}

		[NotNull]
		public static MetricName GetExternalApp([NotNull] string host, [NotNull] string crossProcessId)
		{
			return MetricName.Create("ExternalApp", host, crossProcessId, All);
		}

		[NotNull]
		public static MetricName GetExternalTransaction([NotNull] string host, [NotNull] string crossProcessId,
			[NotNull] string transactionName)
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

		private const string Supportability = "Supportability";
		private const string SupportabilityPs = Supportability + PathSeparator;

		private const string SupportabilityAgentVersionPs = SupportabilityPs + "AgentVersion" + PathSeparator;

		[NotNull]
		public static string GetSupportabilityAgentVersion([NotNull] string version)
		{
			return SupportabilityAgentVersionPs + version;
		}

		[NotNull]
		public static string GetSupportabilityAgentVersionByHost([NotNull] string host, [NotNull] string version)
		{
			return SupportabilityAgentVersionPs + host + PathSeparator + version;
		}

		[NotNull]
		public static string GetSupportabilityLinuxOs() => SupportabilityPs + "OS" + PathSeparator + "Linux";

		private const string SupportabilityUtilizationPs = SupportabilityPs + "utilization" + PathSeparator;

		private const string SupportabilityUtilizationBootIdError =
			SupportabilityUtilizationPs + "boot_id" + PathSeparator + "error";

		private const string SupportabilityUtilizationAwsError =
			SupportabilityUtilizationPs + "aws" + PathSeparator + "error";

		private const string SupportabilityUtilizationAzureError =
			SupportabilityUtilizationPs + "azure" + PathSeparator + "error";

		private const string SupportabilityUtilizationGcpError =
			SupportabilityUtilizationPs + "gcp" + PathSeparator + "error";

		private const string SupportabilityUtilizationPcfError =
			SupportabilityUtilizationPs + "pcf" + PathSeparator + "error";

		[NotNull]
		public static string GetSupportabilityBootIdError() => SupportabilityUtilizationBootIdError;

		[NotNull]
		public static string GetSupportabilityAwsUsabilityError() => SupportabilityUtilizationAwsError;

		[NotNull]
		public static string GetSupportabilityAzureUsabilityError() => SupportabilityUtilizationAzureError;

		[NotNull]
		public static string GetSupportabilityGcpUsabilityError() => SupportabilityUtilizationGcpError;

		[NotNull]
		public static string GetSupportabilityPcfUsabilityError() => SupportabilityUtilizationPcfError;

		public static string GetSupportabilityAgentTimingMetric(string suffix)
		{
			return Supportability + PathSeparator + "AgentTiming" + PathSeparator + suffix;
		}

		// Metrics
		// NOTE: This metric is REQUIRED by the collector (it is used as a heartbeat)
		public const string SupportabilityMetricHarvestTransmit =
			SupportabilityPs + "MetricHarvest" + PathSeparator + "transmit";

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

		public const string SupportabilityTransactionEventsCollected =
			SupportabilityTransactionEventsPs + "TotalEventsCollected";

		public const string SupportabilityTransactionEventsRecollected =
			SupportabilityTransactionEventsPs + "TotalEventsRecollected";

		public const string SupportabilityTransactionEventsReservoirResize =
			SupportabilityTransactionEventsPs + "TryResizeReservoir";

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

		[NotNull] public static readonly MetricName SupportabilitySqlTracesCollected =
			MetricName.Create(SupportabilitySqlTracesPs + "TotalSqlTracesCollected");

		public const string SupportabilitySqlTracesRecollected = SupportabilitySqlTracesPs + "TotalSqlTracesRecollected";

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

		// Agent health events
		[NotNull]
		public static string GetSupportabilityAgentHealthEvent(AgentHealthEvent agentHealthEvent,
			[CanBeNull] string additionalData = null)
		{
			var metricName = SupportabilityPs + agentHealthEvent;
			return (additionalData == null) ? metricName : metricName + PathSeparator + additionalData;
		}

		// Agent features
		private const string SupportabilityFeatureEnabledPs = SupportabilityPs + "FeatureEnabled" + PathSeparator;

		[NotNull]
		public static string GetSupportabilityFeatureEnabled([NotNull] string featureName)
		{
			return SupportabilityFeatureEnabledPs + featureName;
		}

		// Agent API
		private const string SupportabilityAgentApiPs = SupportabilityPs + "ApiInvocation" + PathSeparator;

		[NotNull]
		public static string GetSupportabilityAgentApi([NotNull] string methodName)
		{
			return SupportabilityAgentApiPs + methodName;
		}

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

		[NotNull]
		public static string GetSupportabilityErrorHttpStatusCodeFromCollector(HttpStatusCode statusCode)
		{
			return Supportability + PathSeparator + "Agent/Collector/HTTPError" + PathSeparator + (int) statusCode;
		}

		[NotNull]
		public static string GetSupportabilityEndpointMethodErrorAttempts(string enpointMethod)
		{
			return Supportability + PathSeparator + "Agent/Collector" + PathSeparator + enpointMethod + PathSeparator +
			       "Attempts";
		}

		[NotNull]
		public static string GetSupportabilityEndpointMethodErrorDuration(string enpointMethod)
		{
			return Supportability + PathSeparator + "Agent/Collector" + PathSeparator + enpointMethod + PathSeparator +
			       "Duration";
		}

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
		[NotNull]
		private static string GetDistributedTraceMetricPrefix(
			[NotNull] string metricTag,
			[CanBeNull] string type,
			[CanBeNull] string accountId,
			[CanBeNull] string app,
			[CanBeNull] string transport)
		{
			const int reserveCapacity = 100;
			var sb = new StringBuilder(metricTag, reserveCapacity)
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
			[CanBeNull] string type,
			[CanBeNull] string accountId,
			[CanBeNull] string app,
			[CanBeNull] string transport,
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
			[CanBeNull] string type,
			[CanBeNull] string accountId,
			[CanBeNull] string app,
			[CanBeNull] string transport,
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
			[CanBeNull] string type,
			[CanBeNull] string accountId,
			[CanBeNull] string app,
			[CanBeNull] string transport,
			bool isWeb)
		{
			var prefix = GetDistributedTraceMetricPrefix(DistributedTraceTransportDurationPs, type, accountId, app, transport);
			return (all: MetricName.Create(prefix + All), webOrOther: MetricName.Create(prefix + (isWeb ? AllWeb : AllOther)));
		}

		#endregion Distributed Trace Metrics

		#region Span Metrics

		private const string SupportabilitySpanEventsPs = SupportabilityPs + "SpanEvent" + PathSeparator;
		public const string SupportabilitySpanEventsSent = SupportabilitySpanEventsPs + "TotalEventsSent";
		public const string SupportabilitySpanEventsSeen = SupportabilitySpanEventsPs + "TotalEventsSeen";

		#endregion Span Metrics

	}
}
