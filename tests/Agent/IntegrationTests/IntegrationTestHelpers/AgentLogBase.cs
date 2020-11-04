// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public abstract class AgentLogBase
    {
        public const string LogLineContextDataRegex = @"\[pid: \d+, tid: .+\] ";
        public const string InfoLogLinePrefixRegex = @"^.*?NewRelic\s+INFO: " + LogLineContextDataRegex;
        public const string DebugLogLinePrefixRegex = @"^.*?NewRelic\s+DEBUG: " + LogLineContextDataRegex;
        public const string ErrorLogLinePrefixRegex = @"^.*?NewRelic\s+ERROR: " + LogLineContextDataRegex;
        public const string FinestLogLinePrefixRegex = @"^.*?NewRelic\s+FINEST: " + LogLineContextDataRegex;
        public const string WarnLogLinePrefixRegex = @"^.*?NewRelic\s+WARN: " + LogLineContextDataRegex;
        public const string HarvestLogLineRegex = InfoLogLinePrefixRegex + @"Harvest starting";
        public const string HarvestFinishedLogLineRegex = DebugLogLinePrefixRegex + @"Metric harvest finished.";
        public const string AgentReportingToLogLineRegex = InfoLogLinePrefixRegex + @"Reporting to: (.*)";
        public const string AgentConnectedLogLineRegex = InfoLogLinePrefixRegex + @"Agent fully connected.";
        public const string ConnectLogLineRegex = DebugLogLinePrefixRegex + @"Invoking ""connect"" with : (.*)";
        public const string ReceivedLogLineRegex = DebugLogLinePrefixRegex + @"Received : {""return_value""(.*)";
        public const string ConnectResponseLogLineRegex = DebugLogLinePrefixRegex + @"Received : {""return_value"":{""agent_run_id""(.*)";
        public const string TransactionSampleLogLineRegex = DebugLogLinePrefixRegex + @"Invoking ""transaction_sample_data"" with : (.*)";
        public const string MetricDataLogLineRegex = DebugLogLinePrefixRegex + @"Invoking ""metric_data"" with : (.*)";
        public const string ErrorTraceDataLogLineRegex = DebugLogLinePrefixRegex + @"Invoking ""error_data"" with : (.*)";
        public const string SqlTraceDataLogLineRegex = DebugLogLinePrefixRegex + @"Invoking ""sql_trace_data"" with : (.*)";
        public const string AnalyticsEventDataLogLineRegex = DebugLogLinePrefixRegex + @"Invoking ""analytic_event_data"" with : (.*)";
        public const string AgentWrapperApiCallLogLineRegex = DebugLogLinePrefixRegex + @"AgentWrapperApi call: (.*)\((.*)\)(?: - (.*))?";
        public const string ErrorEventDataLogLineRegex = DebugLogLinePrefixRegex + @"Invoking ""error_event_data"" with : (.*)";
        public const string AgentInvokingMethodErrorLineRegex = DebugLogLinePrefixRegex + @"An error occurred invoking method";
        public const string AgentConnectionErrorLineRegex = ErrorLogLinePrefixRegex + @"Unable to connect to the New Relic service at";
        public const string ThreadProfileStartingLogLineRegex = InfoLogLinePrefixRegex + @"Starting a thread profiling session";
        public const string ThreadProfileDataLogLineRegex = DebugLogLinePrefixRegex + @"Invoking ""profile_data"" with : (.*)";
        public const string InstrumentationUpdateCommandLogLineRegex = DebugLogLinePrefixRegex + @"instrumentation_update command complete.";
        public const string RejitInstrumentationFileChanged = InfoLogLinePrefixRegex + @"Instrumentation change detected:(.*)";
        public const string InstrumentationRefreshFileWatcherStarted = InfoLogLinePrefixRegex + @"Starting instrumentation refresh from InstrumentationWatcher";
        public const string InstrumentationRefreshFileWatcherComplete = InfoLogLinePrefixRegex + @"Completed instrumentation refresh from InstrumentationWatcher";
        public const string ShutdownLogLineRegex = InfoLogLinePrefixRegex + @"The New Relic .NET Agent v.* has shutdown";
        public const string TransactionTransformCompletedLogLineRegex = FinestLogLinePrefixRegex + @"Transaction (.*) \((.*)\) transform completed.";
        public const string SpanEventDataLogLineRegex = DebugLogLinePrefixRegex + @"Invoking ""span_event_data"" with : (.*)";
        public const string TransactionEndedByGCFinalizerLogLineRegEx = DebugLogLinePrefixRegex + @"Transaction was garbage collected without ever ending(.*)";
        public const string TransactionHasAlreadyCapturedResponseTimeLogLineRegEx = FinestLogLinePrefixRegex + @"Transaction has already captured the response time(.*)";

        public abstract IEnumerable<string> GetFileLines();

        public string GetAccountId(TimeSpan? timeoutOrZero = null)
        {
            var reportingAppLink = GetReportingAppLink(timeoutOrZero);
            var reportingAppUri = new Uri(reportingAppLink);
            var accountId = reportingAppUri.Segments[2];
            if (accountId == null)
                throw new Exception("Could not find account ID in second segment of reporting app link: " + reportingAppLink);
            return accountId.TrimEnd('/');
        }

        public string GetApplicationId(TimeSpan? timeoutOrZero = null)
        {
            var reportingAppLink = GetReportingAppLink(timeoutOrZero);
            var reportingAppUri = new Uri(reportingAppLink);
            var applicationId = reportingAppUri.Segments[4];
            if (applicationId == null)
                throw new Exception("Could not find application ID in second segment of reporting app link: " + reportingAppLink);
            return applicationId.TrimEnd('/');
        }

        public string GetCrossProcessId(TimeSpan? timeoutOrZero = null)
        {
            return $@"{GetAccountId()}#{GetApplicationId()}";
        }

        public string GetReportingAppLink(TimeSpan? timeoutOrZero = null)
        {
            var match = WaitForLogLine(AgentReportingToLogLineRegex, timeoutOrZero);
            return match.Groups[1].Value;
        }

        public void WaitForConnect(TimeSpan? timeoutOrZero = null)
        {
            WaitForLogLine(AgentConnectedLogLineRegex, timeoutOrZero);
        }

        #region Log Lines

        public IEnumerable<Match> WaitForLogLines(string regularExpression, TimeSpan? timeoutOrZero = null)
        {
            return WaitForLogLines(regularExpression, timeoutOrZero, 1);
        }

        public IEnumerable<Match> WaitForLogLines(string regularExpression, TimeSpan? timeoutOrZero, int minimumCount)
        {
            Contract.Assert(regularExpression != null);

            var timeout = timeoutOrZero ?? TimeSpan.Zero;

            var timeTaken = Stopwatch.StartNew();
            do
            {
                var matches = TryGetLogLines(regularExpression).ToList();
                if (matches.Count >= minimumCount)
                {
                    return matches;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            } while (timeTaken.Elapsed < timeout);

            var message = $"Log line did not appear a minimum of {minimumCount} times within {timeout.TotalSeconds} seconds.  Expected line expression: {regularExpression}";
            throw new Exception(message);
        }

        public Match WaitForLogLine(string regularExpression, TimeSpan? timeoutOrZero = null)
        {
            return WaitForLogLines(regularExpression, timeoutOrZero).First();
        }

        public IEnumerable<Match> TryGetLogLines(string regularExpression)
        {
            var regex = new Regex(regularExpression);
            return GetFileLines()
                .Where(line => line != null)
                .Select(line => regex.Match(line))
                .Where(match => match != null)
                .Where(match => match.Success);
        }

        public Match TryGetLogLine(string regularExpression)
        {
            return TryGetLogLines(regularExpression).LastOrDefault();
        }


        #endregion Log Lines

        #region JSON helpers

        private static string TryExtractJson(Match match, int captureGroupIndex)
        {
            if (match == null || match.Groups.Count < (captureGroupIndex + 1))
                return null;

            return match.Groups[captureGroupIndex].Value;
        }

        private static IEnumerable<SpanEvent> TryExtractSpanEventsFromJsonArray(string json, int arrayIndex)
        {
            if (json == null)
                return Enumerable.Empty<SpanEvent>();

            var jArray = JArray.Parse(json);
            if (jArray == null || jArray.Count < (arrayIndex + 1))
                return Enumerable.Empty<SpanEvent>();

            var nestedJArray = jArray[arrayIndex];
            if (nestedJArray == null || nestedJArray.Type != JTokenType.Array)
                return Enumerable.Empty<SpanEvent>();

            var spansEvents = new List<SpanEvent>();
            foreach (var jToken in nestedJArray)
            {
                var innerJArray = (JArray)jToken;
                if (innerJArray == null)
                {
                    continue;
                }

                var intrinsics = innerJArray[0].ToObject<Dictionary<string, object>>();
                var user = innerJArray[1].ToObject<Dictionary<string, object>>();
                var agent = innerJArray[2].ToObject<Dictionary<string, object>>();

                spansEvents.Add(new SpanEvent(intrinsics, user, agent));
            }

            return spansEvents;
        }

        private static IEnumerable<T> TryExtractFromJsonArray<T>(string json, int arrayIndex)
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

        public IEnumerable<TransactionSample> GetTransactionSamples()
        {
            //order transactions chronologically
            return TryGetLogLines(TransactionSampleLogLineRegex)
                .Select(match => TryExtractJson(match, 1))
                .SelectMany(json => TryExtractFromJsonArray<TransactionSample>(json, 1))
                .Where(transactionSample => transactionSample != null)
                .OrderBy(transactionSample => transactionSample.Timestamp);
        }

        public TransactionSample TryGetTransactionSample(string transactionName)
        {
            return GetTransactionSamples()
                .Where(sample => sample?.Path == transactionName)
                .FirstOrDefault();
        }

        public T GetTransactionTraceAttribute<T>(string transactionName, TransactionTraceAttributeType attributeType, string parameterName)
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

        public IEnumerable<TransactionEvent> GetTransactionEvents()
        {
            return TryGetLogLines(AnalyticsEventDataLogLineRegex)
                .Select(match => TryExtractJson(match, 1))
                .SelectMany(json => TryExtractFromJsonArray<TransactionEvent>(json, 2))
                .Where(transactionEvent => transactionEvent != null);
        }

        public TransactionEvent TryGetTransactionEvent(string transactionName)
        {
            return GetTransactionEvents()
                .Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == transactionName)
                .FirstOrDefault();
        }

        public IEnumerable<string> TryGetTransactionEndedByGarbageCollector()
        {
            return TryGetLogLines(TransactionEndedByGCFinalizerLogLineRegEx)
                .Select(m => m.Value)
                .ToList();
        }

        public IEnumerable<string> TryGetTransactionHasAlreadyCapturedResponseTime()
        {
            return TryGetLogLines(TransactionHasAlreadyCapturedResponseTimeLogLineRegEx)
                .Select(m => m.Value)
                .ToList();
        }

        #endregion TransactionEvents

        #region SpanEvents

        public IEnumerable<SpanEvent> GetSpanEvents()
        {
            return TryGetLogLines(SpanEventDataLogLineRegex)
                .Select(match => TryExtractJson(match, 1))
                .SelectMany(json => TryExtractSpanEventsFromJsonArray(json, 2))
                .Where(spanEvent => spanEvent != null);
        }

        public SpanEvent TryGetSpanEvent(string spanName)
        {
            return GetSpanEvents()
                .Where(@event => @event?.IntrinsicAttributes?["name"]?.ToString() == spanName)
                .FirstOrDefault();
        }

        #endregion

        #region ErrorEvents

        public IEnumerable<ErrorEventPayload> GetErrorEventPayloads()
        {
            return TryGetLogLines(ErrorEventDataLogLineRegex)
                .Select(match => TryExtractJson(match, 1))
                .Select(json => JsonConvert.DeserializeObject<ErrorEventPayload>(json))
                .Where(errorEvent => errorEvent != null);
        }

        public IEnumerable<ErrorEventEvents> GetErrorEvents()
        {
            return GetErrorEventPayloads().SelectMany(payload => payload.Events);
        }

        #endregion ErrorEvents

        #region ErrorTraces

        public IEnumerable<ErrorTrace> GetErrorTraces()
        {
            return TryGetLogLines(ErrorTraceDataLogLineRegex)
                .Select(match => TryExtractJson(match, 1))
                .SelectMany(json => TryExtractFromJsonArray<ErrorTrace>(json, 1))
                .Where(errorTrace => errorTrace != null);
        }

        public ErrorTrace TryGetErrorTrace(string transactionName)
        {
            return GetErrorTraces()
                .Where(trace => trace?.Path == transactionName)
                .FirstOrDefault();
        }

        public T GetErrorTraceAttribute<T>(string transactionName, TransactionTraceAttributeType attributeType, string parameterName)
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

        private static IEnumerable<KeyValuePair<string, object>> FilterAttributesByType(ErrorTraceAttributes attributesCollection, TransactionTraceAttributeType attributeType)
        {
            if (attributesCollection == null)
                return new Dictionary<string, object>();

            IDictionary<string, object> attributes;
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

            return attributes ?? new Dictionary<string, object>();
        }

        #endregion ErrorTraces

        #region SqlTraces

        public IEnumerable<SqlTrace> GetSqlTraces()
        {
            return TryGetLogLines(SqlTraceDataLogLineRegex)
                .Select(match => TryExtractJson(match, 1))
                .SelectMany(json => TryExtractFromJsonArray<SqlTrace>(json, 0))
                .Where(sqlTrace => sqlTrace != null);
        }

        #endregion ErrorTraces

        #region Rejit

        public Match GetInstrumentationChanged(int timeOut = 80)
        {
            var match = WaitForLogLine(RejitInstrumentationFileChanged, TimeSpan.FromSeconds(timeOut));
            return match;
        }

        public bool GetRejitRequesting(int timeOut = 80)
        {
            var match = WaitForLogLine(InstrumentationRefreshFileWatcherStarted, TimeSpan.FromSeconds(timeOut));
            return match.Success;
        }

        #endregion

        public ConnectData GetConnectData()
        {
            var json = TryGetLogLines(ConnectLogLineRegex)
                .Select(match => TryExtractJson(match, 1))
                .FirstOrDefault();

            json = json?.Trim('[', ']');

            var data = JsonConvert.DeserializeObject<ConnectData>(json);

            return data;
        }

        public ConnectResponseData GetConnectResponseData()
        {
            return GetConnectResponseDatas().FirstOrDefault();
        }

        public IEnumerable<ConnectResponseData> GetConnectResponseDatas()
        {
            var result = new List<ConnectResponseData>();

            var matches = TryGetLogLines(ConnectResponseLogLineRegex)
                .Select(match => TryExtractJson(match, 1))
                .ToList();

            foreach (var match in matches)
            {
                var json = "{ \"agent_run_id\"" + match;
                json = json?.Trim('[', ']');
                json = json.Remove(json.Length - 1); // remove the extra }

                var data = JsonConvert.DeserializeObject<ConnectResponseData>(json);
                result.Add(data);
            }

            return result;
        }

        #region Metrics

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

        public Metric GetMetricByName(string name, string scope = null)
        {
            return GetMetrics()
                .Where(metric => metric != null)
                .Where(metric => metric.MetricSpec != null)
                .Where(metric => metric.MetricSpec.Name == name)
                .Where(metric => metric.MetricSpec.Scope == scope || scope == null)
                .FirstOrDefault();
        }

        #endregion Metrics
    }
}
