﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class Assertions
    {
        #region Transaction Traces

        public static void TransactionTraceExists([NotNull] AgentLogFile agentLogFile, [NotNull] String transactionName)
        {
            var trace = agentLogFile.GetTransactionSamples()
                .Where(sample => sample != null)
                .Where(sample => sample.Path == transactionName)
                .FirstOrDefault();

            var failureMessage = String.Format("Transaction trace does not exist (but should).  Transaction Name: {0}", transactionName);
            Assert.True(trace != null, failureMessage);
        }

        public static void TransactionTraceHasAttributes([NotNull] IEnumerable<KeyValuePair<String, String>> expectedAttributes, TransactionTraceAttributeType attributeType, [NotNull] TransactionSample sample)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = sample.TraceData.Attributes.GetByType(attributeType);
            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!actualAttributes.ContainsKey(expectedAttribute.Key))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the transaction sample.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }

                var actualValue = actualAttributes[expectedAttribute.Key] as String;
                if (actualValue != expectedAttribute.Value)
                {
                    builder.AppendFormat("Attribute named {0} in the transaction sample had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionTraceHasAttributes([NotNull] IEnumerable<String> expectedAttributes, TransactionTraceAttributeType attributeType, [NotNull] TransactionSample sample)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = sample.TraceData.Attributes.GetByType(attributeType);
            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!actualAttributes.ContainsKey(expectedAttribute))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the transaction sample.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionTraceDoesNotHaveAttributes([NotNull] IEnumerable<String> unexpectedAttributes, TransactionTraceAttributeType attributeType, [NotNull] TransactionSample sample)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = sample.TraceData.Attributes.GetByType(attributeType);
            foreach (var unexpectedAttribute in unexpectedAttributes)
            {
                if (actualAttributes.ContainsKey(unexpectedAttribute))
                {
                    builder.AppendFormat("Attribute named {0} was found in the transaction sample but it should not have been.", unexpectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionTraceSegmentsExist(IEnumerable<String> expectedTraceSegmentNames, TransactionSample sample, Boolean areRegexNames = false)
        {
            var allSegments = sample.TraceData.RootSegment.Flatten(node => node.ChildSegments);

            var succeeded = true;
            var builder = new StringBuilder();
            foreach (var expectedSegmentName in expectedTraceSegmentNames)
            {
                if (!allSegments.Any(
                    segment => areRegexNames && Regex.IsMatch(segment.Name, expectedSegmentName)
                        || !areRegexNames && segment.Name == expectedSegmentName)
                    )
                {
                    builder.AppendFormat("Segment named {0} was not found in the transaction sample.", expectedSegmentName);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionTraceSegmentsNotExist(IEnumerable<String> unexpectedTraceSegmentNames, TransactionSample sample, Boolean areRegexNames = false)
        {
            var allSegments = sample.TraceData.RootSegment.Flatten(node => node.ChildSegments);

            var succeeded = true;
            var builder = new StringBuilder();
            foreach (var unexpectedSegmentName in unexpectedTraceSegmentNames)
            {
                if (allSegments.Any(
                    segment => areRegexNames && Regex.IsMatch(segment.Name, unexpectedSegmentName)
                        || !areRegexNames && segment.Name == unexpectedSegmentName)
                    )
                {
                    builder.AppendFormat("Segment named {0} was found in the transaction sample, but shouldn't be.", unexpectedSegmentName);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionTraceSegmentParametersExist(IEnumerable<ExpectedSegmentParameter> expectedParameters, TransactionSample sample)
        {
            var segments = sample.TraceData.RootSegment.Flatten(node => node.ChildSegments);

            var succeeded = true;
            var builder = new StringBuilder();
            foreach (var expectedParameter in expectedParameters)
            {
                var segment = segments.FirstOrDefault(
                    x => expectedParameter.IsRegexSegmentName && Regex.IsMatch(x.Name, expectedParameter.segmentName) ||
                        !expectedParameter.IsRegexSegmentName && x.Name == expectedParameter.segmentName
                    );
                if (segment == null)
                {
                    builder.AppendFormat("Segment {0} was not found on the transaction.", expectedParameter.segmentName);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }

                if (!segment.Parameters.ContainsKey(expectedParameter.parameterName))
                {
                    builder.AppendFormat("Segment parameter {0} was not found in segment named {1}.", expectedParameter.parameterName, expectedParameter.segmentName);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }

                var actualValue = segment.Parameters[expectedParameter.parameterName] as String;
                if (expectedParameter.parameterValue != null && actualValue != expectedParameter.parameterValue)
                {
                    builder.AppendFormat("Segment parameter {0} had the value {1} instead of {2} in segment named {3}.", expectedParameter.parameterName, actualValue, expectedParameter.parameterValue, expectedParameter.segmentName);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        #endregion Transaction Traces

        #region Error Traces

        public static void ErrorTraceHasAttributes([NotNull] IEnumerable<KeyValuePair<String, String>> expectedAttributes, ErrorTraceAttributeType attributeType, [NotNull] ErrorTrace errorTrace)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!errorTrace.Attributes.GetByType(attributeType).ContainsKey(expectedAttribute.Key))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the error trace.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }

                var actualValue = errorTrace.Attributes.GetByType(attributeType)[expectedAttribute.Key] as String;
                if (actualValue != expectedAttribute.Value)
                {
                    builder.AppendFormat("Attribute named {0} in the error trace had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void ErrorTraceDoesNotHaveAttributes([NotNull] IEnumerable<String> unexpectedAttributes, ErrorTraceAttributeType attributeType, [NotNull] ErrorTrace errorTrace)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = errorTrace.Attributes.GetByType(attributeType);
            foreach (var unexpectedAttribute in unexpectedAttributes)
            {
                if (actualAttributes.ContainsKey(unexpectedAttribute))
                {
                    builder.AppendFormat("Attribute named {0} was found in the error trace but it should not have been.", unexpectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        #endregion Error Traces

        #region Error Events

        public static void ErrorEventHasAttributes([NotNull] IEnumerable<KeyValuePair<String, String>> expectedAttributes, EventAttributeType attributeType, [NotNull] ErrorEventEvents errorEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            IDictionary<String, Object> actualAttributes = null;

            switch (attributeType)
            {
                case EventAttributeType.Agent:
                    actualAttributes = errorEvent.AgentAttributes;
                    break;
                case EventAttributeType.Intrinsic:
                    actualAttributes = errorEvent.IntrinsicAttributes;
                    break;
                case EventAttributeType.User:
                    actualAttributes = errorEvent.UserAttributes;
                    break;
            }

            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!actualAttributes.ContainsKey(expectedAttribute.Key))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the error event.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }

                var actualValue = actualAttributes[expectedAttribute.Key] as String;
                if (actualValue != expectedAttribute.Value)
                {
                    builder.AppendFormat("Attribute named {0} in the error event had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void ErrorEventDoesNotHaveAttributes([NotNull] IEnumerable<String> unexpectedAttributes, EventAttributeType attributeType, [NotNull] ErrorEventEvents errorEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            IDictionary<String, Object> actualAttributes = null;

            switch (attributeType)
            {
                case EventAttributeType.Agent:
                    actualAttributes = errorEvent.AgentAttributes;
                    break;
                case EventAttributeType.Intrinsic:
                    actualAttributes = errorEvent.IntrinsicAttributes;
                    break;
                case EventAttributeType.User:
                    actualAttributes = errorEvent.UserAttributes;
                    break;
            }

            foreach (var unexpectedAttribute in unexpectedAttributes)
            {
                if (actualAttributes.ContainsKey(unexpectedAttribute))
                {
                    builder.AppendFormat("Attribute named {0} was found in the error event but it should not have been.", unexpectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        #endregion Error Events

        #region Metrics

        public static void MetricsExist(IEnumerable<ExpectedMetric> expectedMetrics, IEnumerable<Metric> actualMetrics)
        {
            actualMetrics = actualMetrics.ToList();

            var succeeded = true;
            var builder = new StringBuilder();
            foreach (var expectedMetric in expectedMetrics)
            {
                var matchedMetric = TryFindMetric(expectedMetric, actualMetrics);
                if (matchedMetric == null)
                {
                    builder.AppendFormat("Metric named {0} scoped to {1} was not found in the metric payload.", expectedMetric.metricName, expectedMetric.metricScope ?? "nothing");
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }

                if (expectedMetric.callCount != null && matchedMetric.Values.CallCount != expectedMetric.callCount)
                {
                    builder.AppendFormat("Metric named {0} scoped to {1} had an unexpected count of {2}", matchedMetric.MetricSpec.Name, matchedMetric.MetricSpec.Scope ?? "nothing", matchedMetric.Values.CallCount);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void MetricsDoNotExist(IEnumerable<ExpectedMetric> unexpectedMetrics, IEnumerable<Metric> actualMetrics)
        {
            actualMetrics = actualMetrics.ToList();

            var succeeded = true;
            var builder = new StringBuilder();
            foreach (var unexpectedMetric in unexpectedMetrics)
            {
                var matchedMetric = TryFindMetric(unexpectedMetric, actualMetrics);
                if (matchedMetric != null)
                {
                    builder.AppendFormat("Metric named {0} scoped to {1} WAS found but SHOULD NOT exist in the metric payload.", matchedMetric.MetricSpec.Name, matchedMetric.MetricSpec.Scope ?? "nothing");
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        private static Metric TryFindMetric(ExpectedMetric expectedMetric, IEnumerable<Metric> actualMetrics)
        {
            foreach (var actualMetric in actualMetrics)
            {
                if (expectedMetric.IsRegexName && !Regex.IsMatch(actualMetric.MetricSpec.Name, expectedMetric.metricName))
                    continue;
                if (!expectedMetric.IsRegexName && expectedMetric.metricName != actualMetric.MetricSpec.Name)
                    continue;
                if (expectedMetric.metricScope != actualMetric.MetricSpec.Scope)
                    continue;

                return actualMetric;
            }

            return null;
        }

        #endregion Metrics

        #region Transaction Events

        public static void TransactionEventHasAttributes([NotNull] IEnumerable<KeyValuePair<String, String>> expectedAttributes, TransactionEventAttributeType attributeType, [NotNull] TransactionEvent transactionEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = transactionEvent.GetByType(attributeType);
            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!actualAttributes.ContainsKey(expectedAttribute.Key))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the transaction event.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }

                var actualValue = actualAttributes[expectedAttribute.Key] as String;
                if (actualValue != expectedAttribute.Value)
                {
                    builder.AppendFormat("Attribute named {0} in the transaction event had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionEventHasAttributes([NotNull] IEnumerable<String> expectedAttributes, TransactionEventAttributeType attributeType, [NotNull] TransactionEvent transactionEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = transactionEvent.GetByType(attributeType);
            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!actualAttributes.ContainsKey(expectedAttribute))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the transaction event.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionEventDoesNotHaveAttributes([NotNull] IEnumerable<String> unexpectedAttributes, TransactionEventAttributeType attributeType, [NotNull] TransactionEvent transactionEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = transactionEvent.GetByType(attributeType);
            foreach (var unexpectedAttribute in unexpectedAttributes)
            {
                if (actualAttributes.ContainsKey(unexpectedAttribute))
                {
                    builder.AppendFormat("Attribute named {0} was found in the transaction event but it should not have been.", unexpectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        #endregion

        #region Sql Traces

        public static void SqlTraceExists([NotNull] IEnumerable<ExpectedSqlTrace> expectedSqlTraces, [NotNull] IEnumerable<SqlTrace> actualSqlTraces)
        {
            actualSqlTraces = actualSqlTraces.ToList();

            foreach (var expectedSqlTrace in expectedSqlTraces)
            {
                var possibleMatches = actualSqlTraces
                    .Where(sqlTrace => sqlTrace != null)
                    .Where(sqlTrace =>
                        IsNullOrEqual(expectedSqlTrace.TransactionName, sqlTrace.TransactionName) ||
                        IsNullOrEqual(expectedSqlTrace.DatastoreMetricName, sqlTrace.DatastoreMetricName) ||
                        IsNullOrEqual(expectedSqlTrace.Sql, sqlTrace.Sql))
                    .ToList();

                if (!possibleMatches.Any())
                    throw new Exception($"SQL trace does not exist (but should). TransactionName: {expectedSqlTrace.TransactionName}, DatastoreMetricName: {expectedSqlTrace.DatastoreMetricName}, Sql: {expectedSqlTrace.Sql}");

                var closestMatch = possibleMatches
                    .OrderByDescending(sqlTrace => IsNullOrEqual(expectedSqlTrace.TransactionName, sqlTrace.TransactionName))
                    .ThenByDescending(sqlTrace => IsNullOrEqual(expectedSqlTrace.DatastoreMetricName, sqlTrace.DatastoreMetricName))
                    .ThenByDescending(sqlTrace => IsNullOrEqual(expectedSqlTrace.Sql, sqlTrace.Sql))
                    .FirstOrDefault();

                var isExactMatch = closestMatch.TransactionName == expectedSqlTrace.TransactionName &&
                                   closestMatch.DatastoreMetricName == expectedSqlTrace.DatastoreMetricName &&
                                   closestMatch.Sql == expectedSqlTrace.Sql;

                if (!isExactMatch)
                {
                    throw new Exception($"SQL trace does not exist (but should), but a close match was found. TransactionName: {closestMatch.TransactionName}, Sql: {closestMatch.Sql}, DatastoreMetricName: {closestMatch.DatastoreMetricName}");
                }

                if (!expectedSqlTrace.HasExplainPlan.HasValue)
                    return;

                var explainPlanText = closestMatch.ParameterData.GetValueOrDefault("explain_plan");
                if (expectedSqlTrace.HasExplainPlan.Value && explainPlanText == null)
                    throw new Exception($"SQL trace should have an explain plan but doesn't. TransactionName: {expectedSqlTrace.TransactionName}, Sql: {expectedSqlTrace.Sql}, DatastoreMetricName: {expectedSqlTrace.DatastoreMetricName}");

                if (!expectedSqlTrace.HasExplainPlan.Value && explainPlanText != null)
                    throw new Exception($"SQL trace shouldn't have an explain plan but does. TransactionName: {expectedSqlTrace.TransactionName}, Sql: {expectedSqlTrace.Sql}, DatastoreMetricName: {expectedSqlTrace.DatastoreMetricName}");
            }
        }

        #endregion Sql Traces

        #region Log lines

        public static void LogLinesExist([NotNull] IEnumerable<String> expectedLogLineRegexes, [NotNull] IEnumerable<String> actualLogLines)
        {
            actualLogLines = actualLogLines.ToList();

            var builder = new StringBuilder();
            foreach (var regexString in expectedLogLineRegexes)
            {
                var regex = new Regex(regexString);
                if (!actualLogLines.Any(logLine => regex.IsMatch(logLine)))
                    builder.Append("No log line was found matching: ").AppendLine(regexString);
            }

            var errorMessages = builder.ToString();
            Assert.True(errorMessages == String.Empty, errorMessages);
        }

        public static void LogLinesNotExist([NotNull] IEnumerable<String> unexpectedLogLineRegexes, [NotNull] IEnumerable<String> actualLogLines)
        {
            actualLogLines = actualLogLines.ToList();

            var builder = new StringBuilder();
            foreach (var regexString in unexpectedLogLineRegexes)
            {
                var regex = new Regex(regexString);
                if (actualLogLines.Any(logLine => regex.IsMatch(logLine)))
                    builder.Append("Log line was found matching: ").AppendLine(regexString);
            }

            var errorMessages = builder.ToString();
            Assert.True(errorMessages == String.Empty, errorMessages);
        }

        #endregion Log lines

        private static Boolean IsNullOrEqual(String expectedValue, String actualValue)
        {
            if (expectedValue == null)
                return true;

            return expectedValue == actualValue;
        }

        public class ExpectedMetric
        {
            public String metricName = null;
            public String metricScope = null;
            public Decimal? callCount = null;
            public Boolean IsRegexName = false;
        }

        public class ExpectedSegmentParameter
        {
            public String segmentName = null;
            public String parameterName = null;
            public String parameterValue = null;
            public Boolean IsRegexSegmentName = false;
        }

        public class ExpectedSqlTrace
        {
            public String TransactionName = null;
            public String Sql = null;
            public String DatastoreMetricName = null;
            public Boolean? HasExplainPlan = null;
        }
    }
}
