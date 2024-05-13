// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NewRelic.Agent.IntegrationTestHelpers.Collections.Generic;
using Newtonsoft.Json;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class Assertions
    {
        #region Transaction Traces

        public static void TransactionTraceExists(AgentLogFile agentLogFile, string transactionName)
        {
            var trace = agentLogFile.GetTransactionSamples()
                .Where(sample => sample != null)
                .Where(sample => sample.Path == transactionName)
                .FirstOrDefault();

            var failureMessage = string.Format("Transaction trace does not exist (but should).  Transaction Name: {0}", transactionName);
            Assert.True(trace != null, failureMessage);
        }

        public static void TransactionTraceHasAttributes(IEnumerable<KeyValuePair<string, string>> expectedAttributes, TransactionTraceAttributeType attributeType, TransactionSample sample)
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

                var actualValue = actualAttributes[expectedAttribute.Key] as string;
                if (!actualValue.Equals(expectedAttribute.Value, StringComparison.OrdinalIgnoreCase))
                {
                    builder.AppendFormat("Attribute named {0} in the transaction sample had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionTraceHasAttributes(IEnumerable<KeyValuePair<string, object>> expectedAttributes, TransactionTraceAttributeType attributeType, TransactionSample sample)
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

                if (!ValidateAttributeValues(expectedAttribute, actualAttributes[expectedAttribute.Key], builder, "transaction sample"))
                {
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionTraceHasAttributes(IEnumerable<string> expectedAttributes, TransactionTraceAttributeType attributeType, TransactionSample sample)
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

        public static void TransactionTraceDoesNotHaveAttributes(IEnumerable<string> unexpectedAttributes, TransactionTraceAttributeType attributeType, TransactionSample sample)
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

        public static void TransactionTraceSegmentsExist(IEnumerable<string> expectedTraceSegmentNames, TransactionSample sample, bool areRegexNames = false)
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

        public static void TransactionTraceSegmentExists(string expectedClass, string expectedMethod, TransactionSample sample)
        {
            var allSegments = sample.TraceData.RootSegment.Flatten(node => node.ChildSegments);

            var succeeded = true;
            var builder = new StringBuilder();
            if (!allSegments.Any(
                segment => (segment.ClassName == expectedClass) &&
                    (segment.MethodName == expectedMethod)
                ))
            {
                builder.AppendFormat("Segment from class {0} method {1} was not found in the transaction sample.", expectedClass, expectedMethod);
                builder.AppendLine();
                succeeded = false;
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionTraceSegmentDoesNotExist(string unexpectedClass, string unexpectedMethod, TransactionSample sample)
        {
            var allSegments = sample.TraceData.RootSegment.Flatten(node => node.ChildSegments);

            var succeeded = true;
            var builder = new StringBuilder();
            if (allSegments.Any(
                segment => (segment.ClassName == unexpectedClass) &&
                    (segment.MethodName == unexpectedMethod)
                ))
            {
                builder.AppendFormat("Segment from class {0} method {1} was found in the transaction sample but should not be there.", unexpectedClass, unexpectedMethod);
                builder.AppendLine();
                succeeded = false;
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionTraceSegmentsNotExist(IEnumerable<string> unexpectedTraceSegmentNames, TransactionSample sample, bool areRegexNames = false)
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

                var actualValue = segment.Parameters[expectedParameter.parameterName] as string;
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
        public static void TransactionTraceSegmentQueryParametersExist(ExpectedSegmentQueryParameters expectedQueryParameters, TransactionSample sample)
        {
            var segments = sample.TraceData.RootSegment.Flatten(node => node.ChildSegments);

            var segment = segments.First(x => x.Name == expectedQueryParameters.segmentName);
            Assert.Contains(expectedQueryParameters.parameterName, segment.Parameters);

            var actualQueryParameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(segment.Parameters[expectedQueryParameters.parameterName].ToString());
            SqlTraceQueryParametersAreEquivalent(expectedQueryParameters.QueryParameters, actualQueryParameters);
        }

        public static void TransactionTraceSegmentTreeEquals(ExpectedTransactionTraceSegment expectedRootTransactionSegment, TransactionTraceSegment actualRootTransactionSegment)
        {
            var result = expectedRootTransactionSegment.CompareToActualTransactionTrace(actualRootTransactionSegment);
            Assert.True(result.IsEquivalent, result.Diff);
        }

        #endregion Transaction Traces

        #region Error Traces

        public static void ErrorTraceHasAttributes(IEnumerable<KeyValuePair<string, string>> expectedAttributes, ErrorTraceAttributeType attributeType, ErrorTrace errorTrace)
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
                var attribute = errorTrace.Attributes.GetByType(attributeType)[expectedAttribute.Key];
                var actualValue = attribute as string;

                if (actualValue == null && attribute.GetType() == typeof(bool))
                {
                    actualValue = attribute.ToString().ToLowerInvariant();
                }

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

        public static void ErrorTraceHasAttributes(IEnumerable<string> expectedAttributes, ErrorTraceAttributeType attributeType, ErrorTrace errorTrace)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!errorTrace.Attributes.GetByType(attributeType).ContainsKey(expectedAttribute))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the error trace.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void ErrorTraceDoesNotHaveAttributes(IEnumerable<string> unexpectedAttributes, ErrorTraceAttributeType attributeType, ErrorTrace errorTrace)
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

        public static void ErrorEventHasAttributes(IEnumerable<KeyValuePair<string, string>> expectedAttributes, EventAttributeType attributeType, ErrorEventEvents errorEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            IDictionary<string, object> actualAttributes = null;

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

                var actualValue = actualAttributes[expectedAttribute.Key] as string;

                if (actualValue == null && actualAttributes[expectedAttribute.Key].GetType() == typeof(bool))
                {
                    actualValue = actualAttributes[expectedAttribute.Key].ToString().ToLowerInvariant();
                }

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

        public static void ErrorEventHasAttributes(IEnumerable<string> expectedAttributes, EventAttributeType attributeType, ErrorEventEvents errorEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = new List<string>();

            switch (attributeType)
            {
                case EventAttributeType.Agent:
                    actualAttributes = errorEvent.AgentAttributes.Keys.ToList();
                    break;
                case EventAttributeType.Intrinsic:
                    actualAttributes = errorEvent.IntrinsicAttributes.Keys.ToList();
                    break;
                case EventAttributeType.User:
                    actualAttributes = errorEvent.UserAttributes.Keys.ToList();
                    break;
            }

            foreach (var expectedAttribute in expectedAttributes)
            {

                if (!actualAttributes.Contains(expectedAttribute))
                {
                    builder.AppendFormat("Attribute name {0} was not found in error event", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void ErrorEventDoesNotHaveAttributes(IEnumerable<string> unexpectedAttributes, EventAttributeType attributeType, ErrorEventEvents errorEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            IDictionary<string, object> actualAttributes = null;

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

        public static void MetricExists(ExpectedMetric expectedMetric, IEnumerable<Metric> actualMetrics) => MetricsExist(new[] { expectedMetric }, actualMetrics);

        public static void MetricsExist(IEnumerable<ExpectedMetric> expectedMetrics, IEnumerable<Metric> actualMetrics)
        {
            actualMetrics = actualMetrics.ToList();

            var succeeded = true;
            var builder = new StringBuilder();

            if (!actualMetrics.Any() && expectedMetrics.Any())
            {
                builder.AppendLine("Unable to validate expected metrics because actualMetrics has no items.");
                succeeded = false;
            }
            else
            {
                foreach (var expectedMetric in expectedMetrics)
                {
                    if (expectedMetric.CallCountAllHarvests.HasValue)
                    {
                        if (expectedMetric.callCount.HasValue)
                        {
                            throw new Exception($"Cannot validate both callCount (single harvest) and CallCountAllHarvests for a single metric. Please choose one. ExpectedMetric: {expectedMetric}");
                        }

                        var matchedMetrics = TryFindMetrics(expectedMetric, actualMetrics);
                        if (matchedMetrics.Count == 0)
                        {
                            builder.AppendFormat("Metric named {0} scoped to {1} was not found in the metric payload.", expectedMetric.metricName, expectedMetric.metricScope ?? "nothing");
                            builder.AppendLine();
                            builder.AppendLine();
                            succeeded = false;
                            continue;
                        }

                        decimal totalCallCount = 0;
                        foreach (var matchedMetric in matchedMetrics)
                        {
                            totalCallCount += matchedMetric.Values.CallCount;
                        }

                        if (expectedMetric.CallCountAllHarvests != totalCallCount)
                        {
                            var firstMetric = matchedMetrics[0];
                            builder.AppendFormat("Metric named {0} scoped to {1} had an unexpected count of {2} when aggregated across all harvests", firstMetric.MetricSpec.Name, firstMetric.MetricSpec.Scope ?? "nothing", totalCallCount);
                            builder.AppendLine();

                            foreach (var matchedMetric in matchedMetrics)
                            {
                                builder.AppendLine($"- {matchedMetric}");
                            }

                            builder.AppendLine();
                            builder.AppendLine();

                            succeeded = false;
                        }
                    }
                    else
                    {
                        var matchedMetric = TryFindMetric(expectedMetric, actualMetrics);
                        if (matchedMetric == null)
                        {
                            builder.AppendFormat("Metric named {0} scoped to {1} was not found in the metric payload.", expectedMetric.metricName, expectedMetric.metricScope ?? "nothing");
                            builder.AppendLine();
                            builder.AppendLine();

                            succeeded = false;
                            continue;
                        }

                        if (expectedMetric.callCount.HasValue && matchedMetric.Values.CallCount != expectedMetric.callCount)
                        {
                            builder.AppendFormat($"Metric named {matchedMetric.MetricSpec.Name} scoped to {matchedMetric.MetricSpec.Scope ?? "nothing"} had an unexpected count of {matchedMetric.Values.CallCount} (Expected {expectedMetric.callCount})");
                            builder.AppendLine();
                            builder.AppendLine();

                            succeeded = false;
                        }
                    }
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void MetricsDoNotExist(IEnumerable<ExpectedMetric> unexpectedMetrics, IEnumerable<Metric> actualMetrics)
        {
            actualMetrics = actualMetrics.ToList();

            var succeeded = true;
            var builder = new StringBuilder();

            if (!actualMetrics.Any() && unexpectedMetrics.Any())
            {
                builder.AppendLine("Unable to validate unexpected metrics because actualMetrics has no items.");
                succeeded = false;
            }
            else
            {
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
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static Metric TryFindMetric(ExpectedMetric expectedMetric, IEnumerable<Metric> actualMetrics)
        {
            foreach (var actualMetric in actualMetrics)
            {
                if (expectedMetric.IsRegexName && !Regex.IsMatch(actualMetric.MetricSpec.Name, expectedMetric.metricName))
                    continue;
                if (!expectedMetric.IsRegexName && expectedMetric.metricName != actualMetric.MetricSpec.Name)
                    continue;
                if (expectedMetric.IsRegexScope && !Regex.IsMatch(actualMetric.MetricSpec.Scope ?? string.Empty, expectedMetric.metricScope))
                    continue;
                if (!expectedMetric.IsRegexScope && expectedMetric.metricScope != actualMetric.MetricSpec.Scope)
                    continue;

                return actualMetric;
            }

            return null;
        }

        public static List<Metric> TryFindMetrics(ExpectedMetric expectedMetric, IEnumerable<Metric> actualMetrics)
        {
            var foundMetrics = actualMetrics
                .Where(actualMetric => (expectedMetric.IsRegexName && Regex.IsMatch(actualMetric.MetricSpec.Name, expectedMetric.metricName)) ||
                                       (!expectedMetric.IsRegexName && expectedMetric.metricName == actualMetric.MetricSpec.Name))
                .Where(actualMetric => (expectedMetric.IsRegexScope && Regex.IsMatch(actualMetric.MetricSpec.Scope, expectedMetric.metricScope)) ||
                                       (!expectedMetric.IsRegexScope && expectedMetric.metricScope == actualMetric.MetricSpec.Scope))
                .ToList();

            return foundMetrics;
        }

        #endregion Metrics

        #region In Agent Log Forwarding Log Lines Assertions

        public static void LogLineExists(ExpectedLogLine expectedLogLine, IEnumerable<LogLine> actualLogLines) => LogLinesExist(new[] { expectedLogLine }, actualLogLines);

        public static void LogLinesExist(IEnumerable<ExpectedLogLine> expectedLogLines, IEnumerable<LogLine> actualLogLines) => LogLinesExist(expectedLogLines, actualLogLines, false);

        public static void LogLinesExist(IEnumerable<ExpectedLogLine> expectedLogLines, IEnumerable<LogLine> actualLogLines, bool ignoreAttributeCount)
        {
            actualLogLines = actualLogLines.ToList();

            var succeeded = true;
            var builder = new StringBuilder();

            if (!actualLogLines.Any() && expectedLogLines.Any())
            {
                builder.AppendLine("Unable to validate expected Log Lines because actualLogLines has no items.");
                succeeded = false;
            }
            else
            {
                foreach (var expectedLogLine in expectedLogLines)
                {
                    var matchedLogLine = TryFindLogLine(expectedLogLine, actualLogLines, ignoreAttributeCount);
                    if (matchedLogLine == null)
                    {
                        builder.Append($"LogLine `{expectedLogLine}` was not found in the Log payload.");
                        builder.AppendLine();
                        builder.AppendLine();

                        succeeded = false;
                        continue;
                    }
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void LogLineDoesntExist(ExpectedLogLine unexpectedLogLine, IEnumerable<LogLine> actualLogLines) => LogLinesDontExist(new[] { unexpectedLogLine }, actualLogLines);

        public static void LogLinesDontExist(IEnumerable<ExpectedLogLine> unexpectedLogLines, IEnumerable<LogLine> actualLogLines)
        {
            actualLogLines = actualLogLines.ToList();

            var succeeded = true;
            var builder = new StringBuilder();

            foreach (var unexpectedLogLine in unexpectedLogLines)
            {
                var matchedLogLine = TryFindLogLine(unexpectedLogLine, actualLogLines);
                if (matchedLogLine != null)
                {
                    builder.Append($"Unexpected LogLine `{unexpectedLogLine}` was found in the Log payload.");
                    builder.AppendLine();
                    builder.AppendLine();

                    succeeded = false;
                    continue;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        private static LogLine TryFindLogLine(ExpectedLogLine expectedLogLine, IEnumerable<LogLine> actualLogLines) => TryFindLogLine(expectedLogLine, actualLogLines, false);

        private static LogLine TryFindLogLine(ExpectedLogLine expectedLogLine, IEnumerable<LogLine> actualLogLines, bool ignoreAttributeCount)
        {
            foreach (var actualLogLine in actualLogLines)
            {
                if (expectedLogLine.Level != actualLogLine.Level)
                    continue;
                if (expectedLogLine.LogMessage != actualLogLine.Message)
                    continue;
                if (expectedLogLine.HasSpanId.HasValue && expectedLogLine.HasSpanId.Value && string.IsNullOrWhiteSpace(actualLogLine.Spanid))
                    continue;
                if (expectedLogLine.HasSpanId.HasValue && !expectedLogLine.HasSpanId.Value && !string.IsNullOrWhiteSpace(actualLogLine.Spanid))
                    continue;
                if (expectedLogLine.HasTraceId.HasValue && expectedLogLine.HasTraceId.Value && string.IsNullOrWhiteSpace(actualLogLine.Traceid))
                    continue;
                if (expectedLogLine.HasTraceId.HasValue && !expectedLogLine.HasTraceId.Value && !string.IsNullOrWhiteSpace(actualLogLine.Traceid))
                    continue;

                if (expectedLogLine.HasException.HasValue && expectedLogLine.HasException.Value)
                {
                    if (string.IsNullOrWhiteSpace(actualLogLine.ErrorStack)
                    || string.IsNullOrWhiteSpace(actualLogLine.ErrorMessage)
                    || string.IsNullOrWhiteSpace(actualLogLine.ErrorClass)
                    )
                        continue;

                    if (!actualLogLine.ErrorStack.Contains(expectedLogLine.ErrorStack))
                        continue;
                    if (expectedLogLine.ErrorMessage != actualLogLine.ErrorMessage)
                        continue;
                    if (expectedLogLine.ErrorClass != actualLogLine.ErrorClass)
                        continue;
                }

                if (expectedLogLine.HasException.HasValue && !expectedLogLine.HasException.Value)
                {
                    if (!string.IsNullOrWhiteSpace(actualLogLine.ErrorStack)
                    || !string.IsNullOrWhiteSpace(actualLogLine.ErrorMessage)
                    || !string.IsNullOrWhiteSpace(actualLogLine.ErrorClass))
                        continue;
                }

                if (expectedLogLine.Attributes != null)
                {
                    if (actualLogLine.Attributes == null && expectedLogLine.Attributes.Count != 0)
                    {
                        continue;
                    }

                    if (!ignoreAttributeCount && expectedLogLine.Attributes.Count != actualLogLine.Attributes.Count)
                    {
                        continue;
                    }

                    foreach(var expectedAttribute in expectedLogLine.Attributes)
                    {
                        var key = "context." + expectedAttribute.Key;
                        if (!actualLogLine.Attributes.ContainsKey(key))
                        {
                            continue;
                        }

                        if(expectedAttribute.Value.Equals(actualLogLine.Attributes[key]))
                        {
                            continue;
                        }
                    }
                }

                return actualLogLine;
            }

            return null;
        }

        #endregion

        #region Transaction Events

        public static void TransactionEventHasAttributes(IEnumerable<KeyValuePair<string, string>> expectedAttributes, TransactionEventAttributeType attributeType, TransactionEvent transactionEvent)
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

                var actualValue = actualAttributes[expectedAttribute.Key] as string;
                if (actualValue == null && actualAttributes[expectedAttribute.Key].GetType() ==  typeof(bool))
                {
                    actualValue = actualAttributes[expectedAttribute.Key].ToString().ToLowerInvariant();
                }

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

        public static void TransactionEventHasAttributes(IEnumerable<KeyValuePair<string, object>> expectedAttributes, TransactionEventAttributeType attributeType, TransactionEvent transactionEvent)
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

                if (!ValidateAttributeValues(expectedAttribute, actualAttributes[expectedAttribute.Key], builder, "transaction event"))
                {
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void TransactionEventHasAttributes(IEnumerable<string> expectedAttributes, TransactionEventAttributeType attributeType, TransactionEvent transactionEvent)
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

        public static void TransactionEventDoesNotHaveAttributes(IEnumerable<string> unexpectedAttributes, TransactionEventAttributeType attributeType, TransactionEvent transactionEvent)
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

        #region Span Events

        public static void SpanEventHasAttributes(IEnumerable<string> expectedAttributes, SpanEventAttributeType attributeType, SpanEvent spanEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = spanEvent.GetByType(attributeType);
            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!actualAttributes.ContainsKey(expectedAttribute))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the span event.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void SpanEventHasAttributes(IEnumerable<KeyValuePair<string, string>> expectedAttributes, SpanEventAttributeType attributeType, SpanEvent spanEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = spanEvent.GetByType(attributeType);
            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!actualAttributes.ContainsKey(expectedAttribute.Key))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the span event.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }

                var actualValue = actualAttributes[expectedAttribute.Key] as string;
                if (actualValue != expectedAttribute.Value)
                {
                    builder.AppendFormat("Attribute named {0} in the span event had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void SpanEventHasAttributes(IEnumerable<KeyValuePair<string, object>> expectedAttributes, SpanEventAttributeType attributeType, SpanEvent spanEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = spanEvent.GetByType(attributeType);
            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!actualAttributes.ContainsKey(expectedAttribute.Key))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the span event.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }

                if (succeeded && !ValidateAttributeValues(expectedAttribute, actualAttributes[expectedAttribute.Key], builder, "span event"))
                {
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        public static void SpanEventDoesNotHaveAttributes(IEnumerable<string> unexpectedAttributes, SpanEventAttributeType attributeType, SpanEvent spanEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = spanEvent.GetByType(attributeType);
            foreach (var unexpectedAttribute in unexpectedAttributes)
            {
                if (actualAttributes.ContainsKey(unexpectedAttribute))
                {
                    builder.AppendFormat("Attribute named {0} was found in the span event but it should not have been.", unexpectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.True(succeeded, builder.ToString());
        }

        #endregion

        #region Sql Traces

        public static void SqlTraceExists(IEnumerable<ExpectedSqlTrace> expectedSqlTraces, IEnumerable<SqlTrace> actualSqlTraces)
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

                var queryParameters = closestMatch.ParameterData.ContainsKey("query_parameters")
                    ? closestMatch.ParameterData["query_parameters"] as Dictionary<string, object>
                    : null;

                SqlTraceQueryParametersAreEquivalent(expectedSqlTrace.QueryParameters, queryParameters);

                if (!expectedSqlTrace.HasExplainPlan.HasValue)
                {
                    return;
                }

                var explainPlanText = closestMatch.ParameterData.GetValueOrDefault("explain_plan");
                if (expectedSqlTrace.HasExplainPlan.Value && explainPlanText == null)
                {
                    throw new Exception($"SQL trace should have an explain plan but doesn't. TransactionName: {expectedSqlTrace.TransactionName}, Sql: {expectedSqlTrace.Sql}, DatastoreMetricName: {expectedSqlTrace.DatastoreMetricName}");
                }

                if (!expectedSqlTrace.HasExplainPlan.Value && explainPlanText != null)
                {
                    throw new Exception($"SQL trace shouldn't have an explain plan but does. TransactionName: {expectedSqlTrace.TransactionName}, Sql: {expectedSqlTrace.Sql}, DatastoreMetricName: {expectedSqlTrace.DatastoreMetricName}");
                }
            }
        }

        #endregion Sql Traces

        #region Generic Agent Log Lines Assertions

        public static void LogLinesExist(IEnumerable<string> expectedLogLineRegexes, IEnumerable<string> actualLogLines)
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
            Assert.True(errorMessages == string.Empty, errorMessages);
        }

        public static void LogLinesNotExist(IEnumerable<string> unexpectedLogLineRegexes, IEnumerable<string> actualLogLines)
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
            Assert.True(errorMessages == string.Empty, errorMessages);
        }

        #endregion

        private static bool ValidateAttributeValues(KeyValuePair<string, object> expectedAttribute, object rawActualValue, StringBuilder builder, string wireModelTypeName)
        {
            var succeeded = true;

            switch (expectedAttribute.Value)
            {
                case string expectedString:
                    {
                        var actualValue = rawActualValue as string;
                        if (actualValue != expectedString)
                        {
                            builder.AppendFormat("Attribute named {0} in the {3} had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue, wireModelTypeName);
                            builder.AppendLine();
                            succeeded = false;
                        }

                        break;
                    }
                case bool expectedBool:
                    {
                        var actualValue = rawActualValue as bool?;
                        if (!actualValue.HasValue || actualValue.Value != expectedBool)
                        {
                            builder.AppendFormat("Attribute named {0} in the {3} had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue, wireModelTypeName);
                            builder.AppendLine();
                            succeeded = false;
                        }

                        break;
                    }
                case int expectedInt:
                    {
                        //Try getting both integer types before we do the comparison, because sometimes the expected value is an int and the actual is a long but we really just care that the values are the same.
                        var actualValue = rawActualValue as int? ?? rawActualValue as long?;
                        if (!actualValue.HasValue || actualValue.Value != expectedInt)
                        {
                            builder.AppendFormat("Attribute named {0} in the {3} had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue, wireModelTypeName);
                            builder.AppendLine();
                            succeeded = false;
                        }

                        break;
                    }
                case double expectedDouble:
                    {
                        //This comparison basically ensures exact matching. This is appropriate for things like attributes
                        //but not for situations that may be subject to precision issues.
                        var actualValue = rawActualValue as double?;
                        if (!actualValue.HasValue || Math.Abs(actualValue.Value - expectedDouble) > 0)
                        {
                            builder.AppendFormat("Attribute named {0} in the {3} had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue, wireModelTypeName);
                            builder.AppendLine();
                            succeeded = false;
                        }

                        break;
                    }
                default:
                    throw new NotImplementedException("Attribute handling for your type has not yet been implemented. The method only supports strings and bools. Update to add your type!");
            }

            return succeeded;
        }

        private static bool IsNullOrEqual(string expectedValue, string actualValue)
        {
            if (expectedValue == null)
                return true;

            return expectedValue == actualValue;
        }

        private static void SqlTraceQueryParametersAreEquivalent(Dictionary<string, object> expected, Dictionary<string, object> actual)
        {
            if (expected == null) return;

            Assert.NotNull(actual);

            var expectedKeys = expected.Keys;
            var actualKeys = actual.Keys;
            Assert.Empty(expectedKeys.Concat(actualKeys).Except(expectedKeys.Intersect(actualKeys)));

            foreach (var keyValuePair in expected)
            {
                Assert.Contains(keyValuePair.Key, actual.Keys);
                var expectedValueAsString = keyValuePair.Value.ToString();
                var actualValueAsString = actual[keyValuePair.Key].ToString();
                Assert.Equal(expectedValueAsString, actualValueAsString);
            }
        }

        public class ExpectedMetric
        {
            public string metricName = null;
            public string metricScope = null;
            public decimal? callCount = null;
            public decimal? CallCountAllHarvests = null;
            public bool IsRegexName = false;
            public bool IsRegexScope = false;

            public override string ToString()
            {
                return $"{{ metricName: {metricName} metricScope: {metricScope}, IsRegexName: {IsRegexName}, IsRegexScope: {IsRegexScope}, callCount: {callCount}, CallCountAllHarvests: {CallCountAllHarvests} }}";
            }
        }

        public class ExpectedLogLine
        {
            public string LogMessage = null;
            public string Level = null;
            public bool? HasSpanId = null;
            public bool? HasTraceId = null;

            public bool? HasException = null;
            public string ErrorStack = null;
            public string ErrorMessage = null;
            public string ErrorClass = null;
            public Dictionary<string,string> Attributes = null;

            public override string ToString()
            {
                if (Attributes != null && Attributes.Count > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var attribute in Attributes)
                    {
                        sb.Append($"'{attribute.Key}:{attribute.Value ?? ""}',");
                    }

                    return $"{{ Level: {Level}, LogMessage: {LogMessage}, HasSpanId: {HasSpanId}, HasTraceId: {HasTraceId} }}, HasException: {HasException}, ErrorStack: {ErrorStack}, ErrorMessage: {ErrorMessage}, ErrorClass: {ErrorClass}, {{ AttributeCount: {Attributes.Count}, Attributes: {sb.ToString()} }}";
                }

                return $"{{ Level: {Level}, LogMessage: {LogMessage}, HasSpanId: {HasSpanId}, HasTraceId: {HasTraceId} }}, HasException: {HasException}, ErrorStack: {ErrorStack}, ErrorMessage: {ErrorMessage}, ErrorClass: {ErrorClass}";
            }
        }

        public class ExpectedSegmentParameter
        {
            public string segmentName = null;
            public string parameterName = null;
            public string parameterValue = null;
            public bool IsRegexSegmentName = false;
        }

        public class ExpectedSegmentQueryParameters
        {
            public string segmentName = null;
            public readonly string parameterName = "query_parameters";
            public Dictionary<string, object> QueryParameters;
        }

        public class ExpectedSqlTrace
        {
            public string TransactionName = null;
            public string Sql = null;
            public string DatastoreMetricName = null;
            public bool? HasExplainPlan = null;
            public Dictionary<string, object> QueryParameters;

        }
    }
}
