using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Tracer;
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
		public const String PathSeparator = "/";
		public static readonly Char PathSeparatorChar = Convert.ToChar(PathSeparator);

		#region Apdex

		// Apdex metrics spec: https://newrelic.atlassian.net/wiki/display/eng/OtherTransactions+as+Key+Transactions
		public readonly static MetricName ApdexAll = MetricName.Create("ApdexAll");
		public readonly static MetricName ApdexAllWeb = MetricName.Create("Apdex");
		public readonly static MetricName ApdexAllOther = MetricName.Create("ApdexOther");

		private static string Join(params string[] strings)
		{
			return String.Join(PathSeparator, strings);
		}

		[NotNull]
		public static MetricName GetApdexAllWebOrOther(Boolean isWebTransaction)
		{
			return isWebTransaction ? ApdexAllWeb : ApdexAllOther;
		}

		public const String ApdexWeb = "Apdex";
		public const String ApdexOther = "ApdexOther/Transaction";

		/// <summary>
		/// Takes a transaction metric name and returns an appropriate apdex metric name. For example, WebTransaction/MVC/MyApp becomes Apdex/MVC/MyApp.
		/// </summary>
		/// <param name="transactionMetricName">The transaction metric name. Must be a valid transaction metric name.</param>
		/// <returns>An apdex metric name.</returns>
		[NotNull]
		public static String GetTransactionApdex(TransactionMetricName transactionMetricName)
		{
			var apdexPrefix = transactionMetricName.IsWebTransactionName ? ApdexWeb : ApdexOther;
			return Join(apdexPrefix, transactionMetricName.UnPrefixedName);
		}

		#endregion Apdex

		public readonly static MetricName Dispatcher = MetricName.Create("HttpDispatcher");

		#region Errors

		public const String Errors = "Errors";
		public readonly static MetricName ErrorsAll = MetricName.Create(Errors + PathSeparator + All);
		public readonly static MetricName ErrorsAllWeb = MetricName.Create(Errors + PathSeparator + AllWeb);
		public readonly static MetricName ErrorsAllOther = MetricName.Create(Errors + PathSeparator + AllOther);

		[NotNull]
		public static MetricName GetErrorTransaction([NotNull] String transactionMetricName)
		{
			return MetricName.Create(Errors, transactionMetricName);
		}

		#endregion Errors

		public const String OtherTransactionPrefix = "OtherTransaction";
		public const String WebTransactionPrefix = "WebTransaction";

		public readonly static MetricName RequestQueueTime = MetricName.Create("WebFrontend/QueueTime");

		public const String Controller = "DotNetController";
		public const String Uri = "Uri";
		public const String NormalizedUri = "NormalizedUri";

		public const String All = "all";
		public const String AllWeb = "allWeb";
		public const String AllOther = "allOther";

		public const String Custom = "Custom";

		private static readonly string[] databaseVendorNames = Enum.GetNames(typeof(DatastoreVendor));
		private static readonly Func<DatastoreVendor, MetricName> databaseVendorAll;
		private static readonly Func<DatastoreVendor, MetricName> databaseVendorAllWeb;
		private static readonly Func<DatastoreVendor, MetricName> databaseVendorAllOther;
		private static readonly Func<DatastoreVendor, Func<string, MetricName>> databaseVendorOperations;

		static MetricNames() {
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

		[NotNull]
		public static MetricName GetCustom([NotNull] String suffix)
		{
			return MetricName.Create(Custom, suffix);
		}

		#region DotNetInvocation

		private const String DotNetInvocation = "DotNet";

		[NotNull]
		public static MetricName GetDotNetInvocation([NotNull] params string[] segments)
		{
			return MetricName.Create(DotNetInvocation, segments);
		}

		#endregion

		#region Transactions

		public readonly static MetricName WebTransactionAll = MetricName.Create(WebTransactionPrefix);
		public readonly static MetricName OtherTransactionAll = MetricName.Create(OtherTransactionPrefix + PathSeparator + All);

		public static TransactionMetricName WebTransaction([NotNull] String category, [NotNull] String name)
		{
			var unprefixedName = Join(category, name);
			return new TransactionMetricName(WebTransactionPrefix, unprefixedName);
		}

		public static TransactionMetricName UriTransaction([NotNull] String uri)
		{
			var unprefixedName = Join("Uri", uri);
			return new TransactionMetricName(WebTransactionPrefix, unprefixedName);
		}

		public static TransactionMetricName OtherTransaction([NotNull] String category, [NotNull] String name)
		{
			var unprefixedName = Join(category, name);
			return new TransactionMetricName(OtherTransactionPrefix, unprefixedName);
		}

		public static TransactionMetricName CustomTransaction([NotNull] String name, Boolean isWeb)
		{
			var unprefixedName = Join(Custom, name);
			var prefix = isWeb ? WebTransactionPrefix : OtherTransactionPrefix;
			return new TransactionMetricName(prefix, unprefixedName);
		}

		public static TransactionMetricName MessageBrokerTransaction([NotNull] String type, [NotNull] String vendor, [CanBeNull] String name)
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

		[NotNull]
		public static MetricName TransactionTotalTime(TransactionMetricName transactionMetricName)
		{
			var prefix = transactionMetricName.IsWebTransactionName ? WebTransactionTotalTimeAll : OtherTransactionTotalTimeAll;
			return MetricName.Create(prefix.ToString(), transactionMetricName.UnPrefixedName);
		}

		#endregion Total time

		#region CPU time

		private const String CpuTimePrefix = "CPU";

		public const String WebTransactionCpuTimeAll = CpuTimePrefix + PathSeparator + WebTransactionPrefix;
		public const String OtherTransactionCpuTimeAll = CpuTimePrefix + PathSeparator + OtherTransactionPrefix;
		
		[NotNull]
		public static String TransactionCpuTime(TransactionMetricName transactionMetricName)
		{
			var prefix = transactionMetricName.IsWebTransactionName ? WebTransactionCpuTimeAll : OtherTransactionCpuTimeAll;
			return Join(prefix, transactionMetricName.UnPrefixedName);
		}

		#endregion CPU time

		#endregion

		#region MessageBroker

		private const String MessageBrokerPrefix = "MessageBroker";
		private const String MessageBrokerNamed = "Named";
		private const String MessageBrokerTemp = "Temp";

		public const String Msmq = "MSMQ";

		[NotNull]
		public static MetricName GetMessageBroker(MessageBrokerDestinationType type, MessageBrokerAction action, [NotNull] String vendor, [CanBeNull] String queueName)
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

		private const String Datastore = "Datastore";
		public readonly static MetricName DatastoreAll = MetricName.Create(Datastore + PathSeparator + All);
		public readonly static MetricName DatastoreAllWeb = MetricName.Create(Datastore + PathSeparator + AllWeb);
		public readonly static MetricName DatastoreAllOther = MetricName.Create(Datastore + PathSeparator + AllOther);
		private const String DatastoreOperation = Datastore + PathSeparator + "operation";
		private const String DatastoreStatement = Datastore + PathSeparator + "statement";
		private const String DatastoreInstance = Datastore + PathSeparator + "instance";
		public const String DatastoreUnknownOperationName = "other";

		[NotNull, Pure]
		public static string ToString(DatastoreVendor vendor)
		{
			return databaseVendorNames[(int)vendor];
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreVendorAll(this DatastoreVendor vendor)
		{
			return databaseVendorAll.Invoke(vendor);
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreVendorAllWeb(this DatastoreVendor vendor)
		{
			return databaseVendorAllWeb.Invoke(vendor);
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreVendorAllOther(this DatastoreVendor vendor)
		{
			return databaseVendorAllOther.Invoke(vendor);
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreOperation(this DatastoreVendor vendor, String operation = null)
		{
			operation = operation ?? DatastoreUnknownOperationName;
			return databaseVendorOperations.Invoke(vendor).Invoke(operation);
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreStatement(DatastoreVendor vendor, [NotNull] String model, String operation = null)
		{
			operation = operation ?? DatastoreUnknownOperationName;
			return MetricName.Create(DatastoreStatement, ToString(vendor), model, operation);
		}

		[NotNull, Pure]
		public static MetricName GetDatastoreInstance(DatastoreVendor vendor, string host, string portPathOrId)
		{
			return MetricName.Create(DatastoreInstance, ToString(vendor), host, portPathOrId);
		}


		#endregion Datastore

		#region External

		private const String External = "External";
		public readonly static MetricName ExternalAll = MetricName.Create(External + PathSeparator + All);
		public readonly static MetricName ExternalAllWeb = MetricName.Create(External + PathSeparator + AllWeb);
		public readonly static MetricName ExternalAllOther = MetricName.Create(External + PathSeparator + AllOther);

		[NotNull]
		public static MetricName GetExternalHostRollup([NotNull] String host)
		{
			return MetricName.Create(External, host, All);
		}

		[NotNull]
		public static MetricName GetExternalHost([NotNull] String host, [NotNull] String library, [CanBeNull] String operation = null)
		{
			return operation != null
				? MetricName.Create(External, host, library, operation)
				: MetricName.Create(External, host, library);
		}

		[NotNull]
		public static MetricName GetExternalErrors([NotNull] String server)
		{
			return MetricName.Create(External, server, "errors");
		}

		[NotNull]
		public static MetricName GetClientApplication([NotNull] String crossProcessId)
		{
			return MetricName.Create("ClientApplication", crossProcessId, All);
		}

		[NotNull]
		public static MetricName GetExternalApp([NotNull] String host, [NotNull] String crossProcessId)
		{
			return MetricName.Create("ExternalApp", host, crossProcessId, All);
		}

		[NotNull]
		public static MetricName GetExternalTransaction([NotNull] String host, [NotNull] String crossProcessId, [NotNull] String transactionName)
		{
			return MetricName.Create("ExternalTransaction", host, crossProcessId, transactionName);
		}

		#endregion External

		#region Hardware

		public const String MemoryPhysical = "Memory/Physical";
		public const String CpuUserUtilization = "CPU/User/Utilization";
		public const String CpuUserTime = "CPU/User Time";

		#endregion Hardware

		#region Supportability

		public const String Supportability = "Supportability";

		private const String SupportabilityAgentVersion = Supportability + PathSeparator + "AgentVersion";

		[NotNull]
		public static String GetSupportabilityAgentVersion([NotNull] String version)
		{
			return SupportabilityAgentVersion + PathSeparator + version;
		}

		[NotNull]
		public static String GetSupportabilityAgentVersionByHost([NotNull] String host, [NotNull] String version)
		{
			return SupportabilityAgentVersion + PathSeparator + host + PathSeparator + version;
		}

		[NotNull]
		public static String GetSupportabilityLinuxOs()
		{
			return Supportability + PathSeparator + "OS" + PathSeparator + "Linux";
		}

		[NotNull]
		public static string GetSupportabilityBootIdError()
		{
			return Supportability + PathSeparator + "utilization" + PathSeparator + "boot_id" + PathSeparator + "error";
		}

		// Metrics
		// NOTE: This metric is REQUIRED by the collector (it is used as a heartbeat)
		public const String SupportabilityMetricHarvestTransmit = Supportability + PathSeparator + "MetricHarvest" + PathSeparator + "transmit";

		// RUM
		public const String SupportabilityRumHeaderRendered = Supportability + PathSeparator + "RUM/Header";
		public const String SupportabilityRumFooterRendered = Supportability + PathSeparator + "RUM/Footer";
		public const String SupportabilityHtmlPageRendered = Supportability + PathSeparator + "RUM/HtmlPage";

		// Thread Profiling
		public const String SupportabilityThreadProfilingSampleCount = Supportability + PathSeparator + "ThreadProfiling/SampleCount";

		// Transaction Events
		private const String SupportabilityTransactionEvents = Supportability + PathSeparator + "AnalyticsEvents";

		//  Note: these two metrics are REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
		public const String SupportabilityTransactionEventsSent = SupportabilityTransactionEvents + PathSeparator + "TotalEventsSent";
		public const String SupportabilityTransactionEventsSeen = SupportabilityTransactionEvents + PathSeparator + "TotalEventsSeen";

		public const String SupportabilityTransactionEventsCollected = SupportabilityTransactionEvents + PathSeparator + "TotalEventsCollected";
		public const String SupportabilityTransactionEventsRecollected = SupportabilityTransactionEvents + PathSeparator + "TotalEventsRecollected";
		public const String SupportabilityTransactionEventsReservoirResize = SupportabilityTransactionEvents + PathSeparator + "TryResizeReservoir";

		// Custom Events
		private const String SupportabilityCustomEvents = Supportability + PathSeparator + "Events" + PathSeparator + "Customer";

		// Error Events
		private const String SupportabilityErrorEvents = Supportability + PathSeparator + "Events" + PathSeparator + "TransactionError";

		public const String SupportabilityErrorEventsSent = SupportabilityErrorEvents + PathSeparator + "Sent";
		public const String SupportabilityErrorEventsSeen = SupportabilityErrorEvents + PathSeparator + "Seen";

		// Note: Though not required by APM like the transaction event supportability metrics, these metrics should still be created to maintain consistency
		public const String SupportabilityCustomEventsSent = SupportabilityCustomEvents + PathSeparator + "Sent";
		public const String SupportabilityCustomEventsSeen = SupportabilityCustomEvents + PathSeparator + "Seen";

		public const String SupportabilityCustomEventsCollected = SupportabilityCustomEvents + PathSeparator + "TotalEventsCollected";
		public const String SupportabilityCustomEventsRecollected = SupportabilityCustomEvents + PathSeparator + "TotalEventsRecollected";
		public const String SupportabilityCustomEventsReservoirResize = SupportabilityCustomEvents + PathSeparator + "TryResizeReservoir";

		// SQL Trace
		private const String SupportabilitySqlTraces = Supportability + PathSeparator + "SqlTraces";
		public const String SupportabilitySqlTracesSent = SupportabilitySqlTraces + PathSeparator + "TotalSqlTracesSent";
		public readonly static MetricName SupportabilitySqlTracesCollected = MetricName.Create(SupportabilitySqlTraces + PathSeparator + "TotalSqlTracesCollected");
		public const String SupportabilitySqlTracesRecollected = SupportabilitySqlTraces + PathSeparator + "TotalSqlTracesRecollected";

		// Error Traces
		private const String SupportabilityErrorTraces = Supportability + PathSeparator + "Errors";
		public const String SupportabilityErrorTracesSent = SupportabilityErrorTraces + PathSeparator + "TotalErrorsSent";
		public const String SupportabilityErrorTracesCollected = SupportabilityErrorTraces + PathSeparator + "TotalErrorsCollected";
		public const String SupportabilityErrorTracesRecollected = SupportabilityErrorTraces + PathSeparator + "TotalErrorsRecollected";

		// Transaction GarbageCollected
		private const String SupportabilityTransactionBuilderGarbageCollectedPrefix = Supportability + PathSeparator + "TransactionBuilderGarbageCollected";
		public const String SupportabilityTransactionBuilderGarbageCollectedAll = SupportabilityTransactionBuilderGarbageCollectedPrefix + PathSeparator + All;

		// Agent health events
		[NotNull]
		public static String GetSupportabilityAgentHealthEvent(AgentHealthEvent agentHealthEvent, String additionalData = null)
		{
			return additionalData == null
				? Supportability + PathSeparator + agentHealthEvent
				: Supportability + PathSeparator + agentHealthEvent + PathSeparator + additionalData;
		}

		// Agent features
		private const String SupportabilityFeatureEnabled = Supportability + PathSeparator + "FeatureEnabled";

		[NotNull]
		public static String GetSupportabilityFeatureEnabled([NotNull] String featureName)
		{
			return SupportabilityFeatureEnabled + PathSeparator + featureName;
		}

		// Agent API
		public const String SupportabilityAgentApi = "Supportability/ApiInvocation";

		[NotNull]
		public static String GetSupportabilityAgentApi([NotNull] String methodName)
		{
			return SupportabilityAgentApi + PathSeparator + methodName;
		}

		#endregion Supportability
	}
}
