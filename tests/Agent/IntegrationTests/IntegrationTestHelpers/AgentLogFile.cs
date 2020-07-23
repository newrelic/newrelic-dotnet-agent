using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers
{
	public class AgentLogFile
	{
		public const string HarvestLogLineRegex = @"^.*?NewRelic INFO: Harvest starting";
		public const string HarvestFinishedLogLineRegex = @"^.*? NewRelic DEBUG: Harvest finished.";
		public const string AgentReportingToLogLineRegex = @"^.*? NewRelic INFO: Reporting to: (.*)";
		public const string AgentConnectedLogLineRegex = @"^.*? NewRelic INFO: Agent fully connected.";
		public const string ConnectLogLineRegex = @"^.*? NewRelic DEBUG: Invoking ""connect"" with : (.*)";
		public const string TransactionSampleLogLineRegex = @"^.*? NewRelic DEBUG: Invoking ""transaction_sample_data"" with : (.*)";
		public const string MetricDataLogLineRegex = @"^.* NewRelic DEBUG: Invoking ""metric_data"" with : (.*)";
		public const string ErrorTraceDataLogLineRegex = @"^.* NewRelic DEBUG: Invoking ""error_data"" with : (.*)";
		public const string SqlTraceDataLogLineRegex = @"^.* NewRelic DEBUG: Invoking ""sql_trace_data"" with : (.*)";
		public const string AnalyticsEventDataLogLineRegex = @"^.* NewRelic DEBUG: Invoking ""analytic_event_data"" with : (.*)";
		public const string AgentWrapperApiCallLogLineRegex = @"^.* NewRelic DEBUG: AgentWrapperApi call: (.*)\((.*)\)(?: - (.*))?";
		public const string ErrorEventDataLogLineRegex = @"^.* NewRelic DEBUG: Invoking ""error_event_data"" with : (.*)";
		public const string AgentInvokingMethodErrorLineRegex = @"^.*? NewRelic DEBUG: An error occurred invoking method";
		public const string AgentConnectionErrorLineRegex = @"^.*? NewRelic ERROR: Unable to connect to the New Relic service at";
		public const string ThreadProfileStartingLogLineRegex = @"^.*? NewRelic INFO: Starting a thread profiling session";
		public const string ThreadProfileDataLogLineRegex = @"^.*? NewRelic DEBUG: Invoking ""profile_data"" with : (.*)";

		[NotNull]
		private readonly string _filePath;

		public AgentLogFile([NotNull] string logDirectoryPath, TimeSpan? timeoutOrZero = null)
		{
			Contract.Assert(logDirectoryPath != null);

			var timeout = timeoutOrZero ?? TimeSpan.Zero;

			var timeTaken = Stopwatch.StartNew();
			do
			{
				var mostRecentlyUpdatedFile = Directory.EnumerateFiles(logDirectoryPath, "newrelic_agent_*.log")
					.Where(file => file != null)
					.OrderByDescending(File.GetLastWriteTimeUtc)
					.FirstOrDefault();
				if (mostRecentlyUpdatedFile != null)
				{
					_filePath = mostRecentlyUpdatedFile;
					return;
				}

				Thread.Sleep(TimeSpan.FromSeconds(1));
			} while (timeTaken.Elapsed < timeout);

			throw new Exception("No agent log file found.");
		}

		[NotNull] public String GetAccountId(TimeSpan? timeoutOrZero = null)
		{
			var reportingAppLink = GetReportingAppLink(timeoutOrZero);
			var reportingAppUri = new Uri(reportingAppLink);
			var accountId = reportingAppUri.Segments[2];
			if (accountId == null)
				throw new Exception("Could not find account ID in second segment of reporting app link: " + reportingAppLink);
			return accountId.TrimEnd('/');
		}

		[NotNull] public String GetApplicationId(TimeSpan? timeoutOrZero = null)
		{
			var reportingAppLink = GetReportingAppLink(timeoutOrZero);
			var reportingAppUri = new Uri(reportingAppLink);
			var applicationId = reportingAppUri.Segments[4];
			if (applicationId == null)
				throw new Exception("Could not find application ID in second segment of reporting app link: " + reportingAppLink);
			return applicationId.TrimEnd('/');
		}

		[NotNull] public String GetReportingAppLink(TimeSpan? timeoutOrZero = null)
		{
			var match = WaitForLogLine(AgentReportingToLogLineRegex, timeoutOrZero);
			return match.Groups[1].Value;
		}

		public void WaitForConnect(TimeSpan? timeoutOrZero = null)
		{
			WaitForLogLine(AgentConnectedLogLineRegex, timeoutOrZero);
		}

		#region Log Lines

		[NotNull]
		public IEnumerable<Match> WaitForLogLines([NotNull] String regularExpression, TimeSpan? timeoutOrZero = null)
		{
			Contract.Assert(regularExpression != null);

			var timeout = timeoutOrZero ?? TimeSpan.Zero;

			var timeTaken = Stopwatch.StartNew();
			do
			{
				var matches = TryGetLogLines(regularExpression).ToList();
				if (matches.Any())
					return matches;
				Thread.Sleep(TimeSpan.FromMilliseconds(100));
			} while (timeTaken.Elapsed < timeout);

			var message = String.Format("Log line did not appear within {0} seconds.  Expected line expression: {1}", timeout.TotalSeconds, regularExpression);
			throw new Exception(message);
		}

		[NotNull]
		public Match WaitForLogLine([NotNull] String regularExpression, TimeSpan? timeoutOrZero = null)
		{
			return WaitForLogLines(regularExpression, timeoutOrZero).First();
		}

		[NotNull]
		public IEnumerable<Match> TryGetLogLines([NotNull] String regularExpression)
		{
			var regex = new Regex(regularExpression);
			return GetFileLines()
				.Where(line => line != null)
				.Select(line => regex.Match(line))
				.Where(match => match != null)
				.Where(match => match.Success);
		}

		[CanBeNull]
		public Match TryGetLogLine([NotNull] String regularExpression)
		{
			return TryGetLogLines(regularExpression).LastOrDefault();
		}

		[NotNull]
		public IEnumerable<String> GetFileLines()
		{
			String line;
			using (var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (var streamReader = new StreamReader(fileStream))
			while ((line = streamReader.ReadLine()) != null)
			{
				yield return line;
			}
		}

		[NotNull]
		public String GetFullLogAsString()
		{
			using (var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (var streamReader = new StreamReader(fileStream))
			{
				return streamReader.ReadToEnd();
			}
		}

		#endregion Log Lines

		#region JSON helpers

		private static String TryExtractJson([CanBeNull] Match match, Int32 captureGroupIndex)
		{
			if (match == null || match.Groups.Count < (captureGroupIndex + 1))
				return null;

			return match.Groups[captureGroupIndex].Value;
		}

		private static IEnumerable<T> TryExtractFromJsonArray<T>([CanBeNull] String json, Int32 arrayIndex)
		{
			if (json == null)
				return Enumerable.Empty<T>();

			var jArray = JArray.Parse(json);
			if (jArray == null || jArray.Count < (arrayIndex + 1))
				return Enumerable.Empty<T>();

			var nestedJArray = jArray[arrayIndex];
			if (nestedJArray == null || nestedJArray.Type != JTokenType.Array)
				return Enumerable.Empty<T>();

			return nestedJArray
				.Where(jToken => jToken != null)
				.Select(jToken => jToken.ToObject<T>())
				.Where(data => data != null);
		}

		#endregion JSON helpers

		#region TransactionSamples

		[NotNull]
		public IEnumerable<TransactionSample> GetTransactionSamples()
		{
			return TryGetLogLines(TransactionSampleLogLineRegex)
				.Select(match => TryExtractJson(match, 1))
				.SelectMany(json => TryExtractFromJsonArray<TransactionSample>(json, 1))
				.Where(transactionSample => transactionSample != null);
		}

		[CanBeNull]
		public TransactionSample TryGetTransactionSample(String transactionName)
		{
			return GetTransactionSamples()
				.Where(sample => sample?.Path == transactionName)
				.FirstOrDefault();
		}

		public T GetTransactionTraceAttribute<T>([CanBeNull] String transactionName, TransactionTraceAttributeType attributeType, [NotNull] String parameterName)
		{
			return GetTransactionSamples()
				.Where(trace => trace != null)
				.Where(trace => trace.Path == transactionName || transactionName == null)
				.Where(trace => trace.TraceData != null)
				.Where(trace => trace.TraceData.Attributes != null)
				.Select(trace => trace.TraceData.Attributes)
				.SelectMany(attributes => attributes.GetByType(attributeType))
				.Where(pair => pair.Key == parameterName)
				.Select(pair => pair.Value)
				.OfType<T>()
				.FirstOrDefault();
		}

		#endregion TransactionSamples

		#region TransactionEvents

		[NotNull]
		public IEnumerable<TransactionEvent> GetTransactionEvents()
		{
			return TryGetLogLines(AnalyticsEventDataLogLineRegex)
				.Select(match => TryExtractJson(match, 1))
				.SelectMany(json => TryExtractFromJsonArray<TransactionEvent>(json, 1))
				.Where(transactionEvent => transactionEvent != null);
		}

		[CanBeNull]
		public TransactionEvent TryGetTransactionEvent(String transactionName)
		{
			return GetTransactionEvents()
				.Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == transactionName)
				.FirstOrDefault();
		}

		#endregion TransactionEvents

		#region ErrorEvents

		[NotNull]
		public IEnumerable<ErrorEventPayload> GetErrorEvents()
		{
			return TryGetLogLines(ErrorEventDataLogLineRegex)
				.Select(match => TryExtractJson(match, 1))
				.Select(json => JsonConvert.DeserializeObject<ErrorEventPayload>(json))
				.Where(errorEvent => errorEvent != null);
		}

		#endregion ErrorEvents

		#region ErrorTraces

		[NotNull]
		public IEnumerable<ErrorTrace> GetErrorTraces()
		{
			return TryGetLogLines(ErrorTraceDataLogLineRegex)
				.Select(match => TryExtractJson(match, 1))
				.SelectMany(json => TryExtractFromJsonArray<ErrorTrace>(json, 1))
				.Where(errorTrace => errorTrace != null);
		}

		[CanBeNull]
		public ErrorTrace TryGetErrorTrace(String transactionName)
		{
			return GetErrorTraces()
				.Where(trace => trace?.Path == transactionName)
				.FirstOrDefault();
		}

		public T GetErrorTraceAttribute<T>([CanBeNull] String transactionName, TransactionTraceAttributeType attributeType, [NotNull] String parameterName)
		{
			return GetErrorTraces()
				.Where(trace => trace != null)
				.Where(trace => trace.Path == transactionName || transactionName == null)
				.Where(trace => trace.Attributes != null)
				.Select(trace => trace.Attributes)
				.SelectMany(attributes => FilterAttributesByType(attributes, attributeType))
				.Where(pair => pair.Key == parameterName)
				.Select(pair => pair.Value)
				.OfType<T>()
				.FirstOrDefault();
		}

		[NotNull]
		private static IEnumerable<KeyValuePair<String, Object>> FilterAttributesByType([CanBeNull] ErrorTraceAttributes attributesCollection, TransactionTraceAttributeType attributeType)
		{
			if (attributesCollection == null)
				return new Dictionary<String, Object>();

			IDictionary<String, Object> attributes;
			switch (attributeType)
			{
				case TransactionTraceAttributeType.Intrinsic:
					attributes = attributesCollection.IntrinsicAttributes;
					break;
				case TransactionTraceAttributeType.Agent:
					attributes = attributesCollection.AgentAttributes;
					break;
				case TransactionTraceAttributeType.User:
					attributes = attributesCollection.UserAttributes;
					break;
				default:
					throw new NotImplementedException();
			}

			return attributes ?? new Dictionary<String, Object>();
		}

		#endregion ErrorTraces

		#region SqlTraces

		[NotNull]
		public IEnumerable<SqlTrace> GetSqlTraces()
		{
			return TryGetLogLines(SqlTraceDataLogLineRegex)
				.Select(match => TryExtractJson(match, 1))
				.SelectMany(json => TryExtractFromJsonArray<SqlTrace>(json, 0))
				.Where(sqlTrace => sqlTrace != null);
		}

		#endregion ErrorTraces

		[NotNull]
		public ConnectData GetConnectData()
		{
			var json = TryGetLogLines(ConnectLogLineRegex)
				.Select(match => TryExtractJson(match, 1))
				.FirstOrDefault();

			json = json?.Trim('[', ']');

			var data = JsonConvert.DeserializeObject<ConnectData>(json);
			return data;
		}

		#region Metrics

		[NotNull]
		public IEnumerable<Metric> GetMetrics()
		{
			return TryGetLogLines(MetricDataLogLineRegex)
				.Select(match => TryExtractJson(match, 1))
				.Where(json => json != null)
				.Select(JsonConvert.DeserializeObject<MetricData>)
				.Where(metricData => metricData != null)
				.SelectMany(metricData => metricData.Metrics)
				.Where(metric => metric != null);
		}

		[CanBeNull]
		public Metric GetMetricByName([NotNull] String name, [CanBeNull] String scope = null)
		{
			return GetMetrics()
				.Where(metric => metric != null)
				.Where(metric => metric.MetricSpec != null)
				.Where(metric => metric.MetricSpec.Name == name)
				.Where(metric => metric.MetricSpec.Scope == scope || scope == null)
				.FirstOrDefault();
		}

		#endregion Metrics

		public void ClearLog()
		{
			File.Delete(_filePath);
		}
	}
}
