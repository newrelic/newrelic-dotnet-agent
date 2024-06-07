// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.TestUtilities;
using NewRelic.SystemExtensions.Collections.Generic;
using NUnit.Framework;

namespace CompositeTests
{
    internal static class MetricAssertions
    {
        public static void MetricsExist(IEnumerable<ExpectedMetric> expectedMetrics, IEnumerable<MetricWireModel> actualMetrics)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            foreach (var expectedMetric in expectedMetrics)
            {
                var matchedMetric = TryFindMetric(expectedMetric, actualMetrics);
                if (matchedMetric == null)
                {
                    builder.AppendFormat("Metric named {0} scoped to {1} was not found in the metric payload.", expectedMetric.Name, expectedMetric.Scope ?? "nothing");
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }

                if ((expectedMetric.Value0 != null && matchedMetric.DataModel.Value0 != expectedMetric.Value0) ||
                    (expectedMetric.Value1 != null && matchedMetric.DataModel.Value1 != expectedMetric.Value1) ||
                    (expectedMetric.Value2 != null && matchedMetric.DataModel.Value2 != expectedMetric.Value2) ||
                    (expectedMetric.Value3 != null && matchedMetric.DataModel.Value3 != expectedMetric.Value3) ||
                    (expectedMetric.Value4 != null && matchedMetric.DataModel.Value4 != expectedMetric.Value4) ||
                    (expectedMetric.Value5 != null && matchedMetric.DataModel.Value5 != expectedMetric.Value5))
                {
                    builder.AppendFormat("Metric named {0} scoped to {1} was found in the metric payload, but had unexpected stats.", matchedMetric.MetricNameModel.Name, matchedMetric.MetricNameModel.Scope ?? "nothing");
                    builder.AppendLine();
                    builder.AppendFormat("Expected: {0}, {1}, {2}, {3}, {4}, {5}", expectedMetric.Value0, expectedMetric.Value1, expectedMetric.Value2, expectedMetric.Value3, expectedMetric.Value4, expectedMetric.Value5);
                    builder.AppendLine();
                    builder.AppendFormat("Actual: {0}, {1}, {2}, {3}, {4}, {5}", matchedMetric.DataModel.Value0, matchedMetric.DataModel.Value1, matchedMetric.DataModel.Value2, matchedMetric.DataModel.Value3, matchedMetric.DataModel.Value4, matchedMetric.DataModel.Value5);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.That(succeeded, Is.True, builder.ToString());
        }

        public static void MetricsDoNotExist(IEnumerable<ExpectedMetric> unexpectedMetrics, IEnumerable<MetricWireModel> actualMetrics)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            foreach (var unexpectedMetric in unexpectedMetrics)
            {
                var matchedMetric = TryFindMetric(unexpectedMetric, actualMetrics);

                if (matchedMetric != null)
                {
                    builder.AppendFormat("Metric named {0} scoped to {1} was found in the metric payload.", matchedMetric.MetricNameModel.Name, matchedMetric.MetricNameModel.Scope ?? "nothing");
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.That(succeeded, Is.True, builder.ToString());
        }

        private static MetricWireModel TryFindMetric(ExpectedMetric expectedMetric, IEnumerable<MetricWireModel> actualMetrics)
        {
            foreach (var actualMetric in actualMetrics)
            {
                if (expectedMetric.IsRegexName && !Regex.IsMatch(actualMetric.MetricNameModel.Name, expectedMetric.Name))
                    continue;
                if (!expectedMetric.IsRegexName && expectedMetric.Name != actualMetric.MetricNameModel.Name)
                    continue;
                if (expectedMetric.Scope != actualMetric.MetricNameModel.Scope)
                    continue;

                return actualMetric;
            }

            return null;
        }
    }

    internal static class TransactionEventAssertions
    {
        public static void HasAttributes(IEnumerable<ExpectedAttribute> expectedAttributes, AttributeClassification attributeClassification, TransactionEventWireModel transactionEvent)
        {
            var errorMessageBuilder = new StringBuilder();
            var actualAttributes = transactionEvent.GetAttributes(attributeClassification);
            var allAttributesMatch = ExpectedAttribute.CheckIfAllAttributesMatch(actualAttributes, expectedAttributes, errorMessageBuilder);

            Assert.That(allAttributesMatch, Is.True, errorMessageBuilder.ToString());
        }

        public static void DoesNotHaveAttributes(IEnumerable<string> unexpectedAttributes, AttributeClassification attributeClassification, TransactionEventWireModel transactionEvent)
        {
            var errorMessageBuilder = new StringBuilder();
            var actualAttributes = transactionEvent.GetAttributes(attributeClassification);
            var allAttributesNotFound = ExpectedAttribute.CheckIfAllAttributesNotFound(actualAttributes, unexpectedAttributes, errorMessageBuilder);

            Assert.That(allAttributesNotFound, Is.True, errorMessageBuilder.ToString());
        }
    }

    internal static class CustomEventAssertions
    {
        public static void HasAttributes(IEnumerable<ExpectedAttribute> expectedAttributes, AttributeClassification attributeClassification, CustomEventWireModel customEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = customEvent.GetAttributes(attributeClassification);
            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!actualAttributes.ContainsKey(expectedAttribute.Key))
                {
                    builder.AppendFormat("Attribute named {0} was not found in the transaction event.", expectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }

                var expectedValue = expectedAttribute.Value;
                var actualValue = actualAttributes[expectedAttribute.Key] as string;
                if (expectedValue != null && actualValue != expectedAttribute.Value as string)
                {
                    builder.AppendFormat("Attribute named {0} in the transaction event had an unexpected value.  Expected: {1}, Actual: {2}", expectedAttribute.Key, expectedAttribute.Value, actualValue);
                    builder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            Assert.That(succeeded, Is.True, builder.ToString());
        }

        public static void DoesNotHaveAttributes(IEnumerable<string> unexpectedAttributes, AttributeClassification attributeClassification, CustomEventWireModel customEvent)
        {
            var succeeded = true;
            var builder = new StringBuilder();
            var actualAttributes = customEvent.GetAttributes(attributeClassification);
            foreach (var unexpectedAttribute in unexpectedAttributes)
            {
                if (actualAttributes.ContainsKey(unexpectedAttribute))
                {
                    builder.AppendFormat("Attribute named {0} was found in the transaction event but it should not have been.", unexpectedAttribute);
                    builder.AppendLine();
                    succeeded = false;
                }
            }

            Assert.That(succeeded, Is.True, builder.ToString());
        }
    }

    internal static class SpanAssertions
    {
        public static void HasAttributes(IEnumerable<ExpectedAttribute> expectedAttributes, AttributeClassification attributeClassification, ISpanEventWireModel span)
        {

            var errorMessageBuilder = new StringBuilder();
            var actualAttributes = span.GetAttributeValues(attributeClassification);
            var allAttributesMatch = ExpectedAttribute.CheckIfAllAttributesMatch(actualAttributes, expectedAttributes, errorMessageBuilder);

            Assert.That(allAttributesMatch, Is.True, errorMessageBuilder.ToString());
        }

        public static void DoesNotHaveAttributes(IEnumerable<string> unexpectedAttributes, AttributeClassification attributeClassification, ISpanEventWireModel span)
        {
            var errorMessageBuilder = new StringBuilder();
            var actualAttributes = span.GetAttributeValues(attributeClassification);
            var allAttributesNotFound = ExpectedAttribute.CheckIfAllAttributesNotFound(actualAttributes, unexpectedAttributes, errorMessageBuilder);

            Assert.That(allAttributesNotFound, Is.True, errorMessageBuilder.ToString());
        }

    }

    internal static class TransactionTraceAssertions
    {
        public static void HasAttributes(IEnumerable<ExpectedAttribute> expectedAttributes, AttributeClassification attributeClassification, TransactionTraceWireModel trace)
        {
            var errorMessageBuilder = new StringBuilder();
            var actualAttributes = trace.GetAttributes(attributeClassification);
            var allAttributesMatch = ExpectedAttribute.CheckIfAllAttributesMatch(actualAttributes, expectedAttributes, errorMessageBuilder);

            Assert.That(allAttributesMatch, Is.True, errorMessageBuilder.ToString());
        }

        public static void DoesNotHaveAttributes(IEnumerable<string> unexpectedAttributes, AttributeClassification attributeClassification, TransactionTraceWireModel trace)
        {
            var errorMessageBuilder = new StringBuilder();
            var actualAttributes = trace.GetAttributes(attributeClassification);
            var allAttributesNotFound = ExpectedAttribute.CheckIfAllAttributesNotFound(actualAttributes, unexpectedAttributes, errorMessageBuilder);

            Assert.That(allAttributesNotFound, Is.True, errorMessageBuilder.ToString());
        }

        public static void SegmentsExist(IEnumerable<string> expectedTraceSegmentNames, TransactionTraceWireModel trace, bool areRegexNames = false)
        {
            var allSegments = trace.TransactionTraceData.RootSegment.Flatten(node => node.Children);

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

            Assert.That(succeeded, Is.True, builder.ToString());
        }

        public static void SegmentsDoNotExist(IEnumerable<string> unexpectedTraceSegmentNames, TransactionTraceWireModel trace, bool areRegexNames = false)
        {
            var allSegments = trace.TransactionTraceData.RootSegment.Flatten(node => node.Children);

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

            Assert.That(succeeded, Is.True, builder.ToString());
        }
    }

    internal static class ErrorTraceAssertions
    {
        public static void ErrorTraceHasAttributes(IEnumerable<ExpectedAttribute> expectedAttributes, AttributeClassification attributeClassification, ErrorTraceWireModel errorTrace)
        {
            var errorMessageBuilder = new StringBuilder();
            var actualAttributes = errorTrace.GetAttributes(attributeClassification);
            var allAttributesMatch = ExpectedAttribute.CheckIfAllAttributesMatch(actualAttributes, expectedAttributes, errorMessageBuilder);

            Assert.That(allAttributesMatch, Is.True, errorMessageBuilder.ToString());
        }

        public static void ErrorTraceDoesNotHaveAttributes(IEnumerable<string> unexpectedAttributes, AttributeClassification attributeClassification, ErrorTraceWireModel errorTrace)
        {
            var errorMessageBuilder = new StringBuilder();
            var actualAttributes = errorTrace.GetAttributes(attributeClassification);
            var allAttributesNotFound = ExpectedAttribute.CheckIfAllAttributesNotFound(actualAttributes, unexpectedAttributes, errorMessageBuilder);

            Assert.That(allAttributesNotFound, Is.True, errorMessageBuilder.ToString());
        }
    }

    internal class ExpectedMetric
    {
        public string Name;
        public string Scope;
        public bool IsRegexName = false;
        public int? Value0 = null;
        public float? Value1 = null;
        public float? Value2 = null;
        public float? Value3 = null;
        public float? Value4 = null;
        public float? Value5 = null;
    }

    internal class ExpectedApdexMetric : ExpectedMetric
    {
        public int? SatisfyingCount { get { return Value0; } set { Value0 = value; } }
        public float? ToleratingCount { get { return Value1; } set { Value1 = value; } }
        public float? FrustratingCount { get { return Value2; } set { Value2 = value; } }
        public float? Min { get { return Value3; } set { Value3 = value; } }
        public float? Max { get { return Value4; } set { Value4 = value; } }
        public float? Unused { get { return Value5; } set { Value5 = value; } }
    }

    internal class ExpectedCountMetric : ExpectedMetric
    {
        public int? CallCount { get { return Value0; } set { Value0 = value; } }
        public float? Total { get { return Value1; } set { Value1 = value; } }
        public float? TotalExclusive { get { return Value2; } set { Value2 = value; } }
        public float? Min { get { return Value3; } set { Value3 = value; } }
        public float? Max { get { return Value4; } set { Value4 = value; } }
        public float? SumOfSquares { get { return Value5; } set { Value5 = value; } }
    }

    internal class ExpectedTimeMetric : ExpectedMetric
    {
        public int? CallCount { get { return Value0; } set { Value0 = value; } }
        public float? Total { get { return Value1; } set { Value1 = value; } }
        public float? TotalExclusive { get { return Value2; } set { Value2 = value; } }
        public float? Min { get { return Value3; } set { Value3 = value; } }
        public float? Max { get { return Value4; } set { Value4 = value; } }
        public float? SumOfSquaresInSeconds { get { return Value5; } set { Value5 = value; } }
    }

    internal class ExpectedAttribute
    {
        public string Key;
        public object Value;

        public static bool CheckIfAllAttributesMatch(IEnumerable<IAttributeValue> actualAttributes, IEnumerable<ExpectedAttribute> expectedAttributes, StringBuilder errorMessageBuilder)
        {
            return CheckIfAllAttributesMatch(actualAttributes.ToDictionary(x => x.AttributeDefinition.Name, x => x.Value), expectedAttributes, errorMessageBuilder);
        }

        public static bool CheckIfAllAttributesMatch(IDictionary<string, object> actualAttributes, IEnumerable<ExpectedAttribute> expectedAttributes, StringBuilder errorMessageBuilder)
        {
            var succeeded = true;

            foreach (var expectedAttribute in expectedAttributes)
            {
                if (!actualAttributes.ContainsKey(expectedAttribute.Key))
                {
                    errorMessageBuilder.Append($"Attribute named {expectedAttribute.Key} was not found.");
                    errorMessageBuilder.AppendLine();
                    succeeded = false;
                    continue;
                }

                var expectedValue = expectedAttribute.Value;
                var actualValue = actualAttributes[expectedAttribute.Key];
                if (expectedValue != null && !HaveSameValue(actualValue, expectedValue))
                {
                    errorMessageBuilder.Append($"Attribute named {expectedAttribute.Key} had an unexpected value.  Expected: {expectedValue} ({expectedValue.GetType().FullName}), Actual: {actualValue} ({actualValue?.GetType().FullName ?? "null"})");
                    errorMessageBuilder.AppendLine();
                    succeeded = false;
                    continue;
                }
            }

            return succeeded;
        }

        private static bool HaveSameValue(object actualValue, object expectedValue)
        {
            if (actualValue == null)
                return expectedValue == null;

            if (actualValue is string && expectedValue is string)
                return string.Equals((string)actualValue, (string)expectedValue);

            return actualValue.Equals(expectedValue);
        }

        public static bool CheckIfAllAttributesNotFound(IEnumerable<IAttributeValue> actualAttributes, IEnumerable<string> unexpectedAttributes, StringBuilder errorMessageBuilder)
        {
            return CheckIfAllAttributesNotFound(actualAttributes.ToDictionary(x => x.AttributeDefinition.Name, x => x.Value), unexpectedAttributes, errorMessageBuilder);
        }

        public static bool CheckIfAllAttributesNotFound(IDictionary<string, object> actualAttributes, IEnumerable<string> unexpectedAttributes, StringBuilder errorMessageBuilder)
        {
            var succeeded = true;

            foreach (var unexpectedAttribute in unexpectedAttributes)
            {
                if (actualAttributes.ContainsKey(unexpectedAttribute))
                {
                    errorMessageBuilder.Append($"Attribute named {unexpectedAttribute} was found in the transaction event but it should not have been.");
                    errorMessageBuilder.AppendLine();
                    succeeded = false;
                }
            }

            return succeeded;
        }
    }

    internal static class WireModelExtensions
    {
        public static IDictionary<string, object> GetAttributes(this TransactionEventWireModel transactionEvent, AttributeClassification attributeClassification)
        {
            switch (attributeClassification)
            {
                case AttributeClassification.Intrinsics:
                    return transactionEvent.IntrinsicAttributes();
                case AttributeClassification.AgentAttributes:
                    return transactionEvent.AgentAttributes();
                case AttributeClassification.UserAttributes:
                    return transactionEvent.UserAttributes();
                default:
                    throw new NotImplementedException();
            }
        }

        public static IDictionary<string, object> GetAttributes(this CustomEventWireModel customEvent, AttributeClassification attributeClassification)
        {
            return customEvent.AttributeValues.ToDictionary(attributeClassification);
        }
    
        public static IDictionary<string, object> GetAttributes(this TransactionTraceWireModel transactionTrace, AttributeClassification attributeClassification)
        {
            switch (attributeClassification)
            {
                case AttributeClassification.Intrinsics:
                    return transactionTrace.TransactionTraceData.Attributes.Intrinsics;
                case AttributeClassification.AgentAttributes:
                    return transactionTrace.TransactionTraceData.Attributes.AgentAttributes;
                case AttributeClassification.UserAttributes:
                    return transactionTrace.TransactionTraceData.Attributes.UserAttributes;
                default:
                    throw new NotImplementedException();
            }
        }

        public static IDictionary<string, object> GetAttributes(this ErrorTraceWireModel errorTrace, AttributeClassification attributeClassification)
        {
            switch (attributeClassification)
            {
                case AttributeClassification.Intrinsics:
                    return errorTrace.Attributes.Intrinsics.ToDictionary();
                case AttributeClassification.AgentAttributes:
                    return errorTrace.Attributes.AgentAttributes.ToDictionary();
                case AttributeClassification.UserAttributes:
                    return errorTrace.Attributes.UserAttributes.ToDictionary();
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
