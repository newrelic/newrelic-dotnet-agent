using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemExtensions.Collections.Generic;
using NUnit.Framework;

namespace CompositeTests
{
	internal static class MetricAssertions
	{
		public static void MetricsExist([NotNull] IEnumerable<ExpectedMetric> expectedMetrics, [NotNull] IEnumerable<MetricWireModel> actualMetrics)
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
				
				if ((expectedMetric.Value0 != null && matchedMetric.Data.Value0 != expectedMetric.Value0) ||
					(expectedMetric.Value1 != null && matchedMetric.Data.Value1 != expectedMetric.Value1) ||
					(expectedMetric.Value2 != null && matchedMetric.Data.Value2 != expectedMetric.Value2) ||
					(expectedMetric.Value3 != null && matchedMetric.Data.Value3 != expectedMetric.Value3) ||
					(expectedMetric.Value4 != null && matchedMetric.Data.Value4 != expectedMetric.Value4) ||
					(expectedMetric.Value5 != null && matchedMetric.Data.Value5 != expectedMetric.Value5))
				{
					builder.AppendFormat("Metric named {0} scoped to {1} was found in the metric payload, but had unexpected stats.", matchedMetric.MetricName.Name, matchedMetric.MetricName.Scope ?? "nothing");
					builder.AppendLine();
					builder.AppendFormat("Expected: {0}, {1}, {2}, {3}, {4}, {5}", expectedMetric.Value0, expectedMetric.Value1, expectedMetric.Value2, expectedMetric.Value3, expectedMetric.Value4, expectedMetric.Value5);
					builder.AppendLine();
					builder.AppendFormat("Actual: {0}, {1}, {2}, {3}, {4}, {5}", matchedMetric.Data.Value0, matchedMetric.Data.Value1, matchedMetric.Data.Value2, matchedMetric.Data.Value3, matchedMetric.Data.Value4, matchedMetric.Data.Value5);
					builder.AppendLine();
					succeeded = false;
				}
			}

			Assert.True(succeeded, builder.ToString());
		}

		public static void MetricsDoNotExist([NotNull] IEnumerable<ExpectedMetric> unexpectedMetrics, [NotNull] IEnumerable<MetricWireModel> actualMetrics)
		{
			var succeeded = true;
			var builder = new StringBuilder();
			foreach (var unexpectedMetric in unexpectedMetrics)
			{
				var matchedMetric = TryFindMetric(unexpectedMetric, actualMetrics);

				if (matchedMetric != null)
				{
					builder.AppendFormat("Metric named {0} scoped to {1} was found in the metric payload.", matchedMetric.MetricName.Name, matchedMetric.MetricName.Scope ?? "nothing");
					builder.AppendLine();
					succeeded = false;
				}
			}

			Assert.True(succeeded, builder.ToString());
		}

		private static MetricWireModel TryFindMetric([NotNull] ExpectedMetric expectedMetric, [NotNull] IEnumerable<MetricWireModel> actualMetrics)
		{
			foreach (var actualMetric in actualMetrics)
			{
				if (expectedMetric.IsRegexName && !Regex.IsMatch(actualMetric.MetricName.Name, expectedMetric.Name))
					continue;
				if (!expectedMetric.IsRegexName && expectedMetric.Name != actualMetric.MetricName.Name)
					continue;
				if (expectedMetric.Scope != actualMetric.MetricName.Scope)
					continue;

				return actualMetric;
			}

			return null;
		}
	}

	internal static class TransactionEventAssertions
	{
		public static void HasAttributes([NotNull] IEnumerable<ExpectedAttribute> expectedAttributes, AttributeClassification attributeClassification, [NotNull] TransactionEventWireModel transactionEvent)
		{
			var errorMessageBuilder = new StringBuilder();
			var actualAttributes = transactionEvent.GetAttributes(attributeClassification);
			var allAttributesMatch = ExpectedAttribute.CheckIfAllAttributesMatch(actualAttributes, expectedAttributes, errorMessageBuilder);

			Assert.True(allAttributesMatch, errorMessageBuilder.ToString());
		}

		public static void DoesNotHaveAttributes([NotNull] IEnumerable<String> unexpectedAttributes, AttributeClassification attributeClassification, [NotNull] TransactionEventWireModel transactionEvent)
		{
			var errorMessageBuilder = new StringBuilder();
			var actualAttributes = transactionEvent.GetAttributes(attributeClassification);
			var allAttributesNotFound = ExpectedAttribute.CheckIfAllAttributesNotFound(actualAttributes, unexpectedAttributes, errorMessageBuilder);

			Assert.True(allAttributesNotFound, errorMessageBuilder.ToString());
		}
	}

	internal static class CustomEventAssertions
	{
		public static void HasAttributes([NotNull] IEnumerable<ExpectedAttribute> expectedAttributes, AttributeClassification attributeClassification, [NotNull] CustomEventWireModel customEvent)
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

			Assert.True(succeeded, builder.ToString());
		}

		public static void DoesNotHaveAttributes([NotNull] IEnumerable<String> unexpectedAttributes, AttributeClassification attributeClassification, [NotNull] CustomEventWireModel customEvent)
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

			Assert.True(succeeded, builder.ToString());
		}
	}

	internal static class TransactionTraceAssertions
	{
		public static void HasAttributes([NotNull] IEnumerable<ExpectedAttribute> expectedAttributes, AttributeClassification attributeClassification, [NotNull] TransactionTraceWireModel trace)
		{
			var errorMessageBuilder = new StringBuilder();
			var actualAttributes = trace.GetAttributes(attributeClassification);
			var allAttributesMatch = ExpectedAttribute.CheckIfAllAttributesMatch(actualAttributes, expectedAttributes, errorMessageBuilder);

			Assert.True(allAttributesMatch, errorMessageBuilder.ToString());
		}

		public static void DoesNotHaveAttributes([NotNull] IEnumerable<String> unexpectedAttributes, AttributeClassification attributeClassification, [NotNull] TransactionTraceWireModel trace)
		{
			var errorMessageBuilder = new StringBuilder();
			var actualAttributes = trace.GetAttributes(attributeClassification);
			var allAttributesNotFound = ExpectedAttribute.CheckIfAllAttributesNotFound(actualAttributes, unexpectedAttributes, errorMessageBuilder);

			Assert.True(allAttributesNotFound, errorMessageBuilder.ToString());
		}

		public static void SegmentsExist([NotNull] IEnumerable<String> expectedTraceSegmentNames, [NotNull] TransactionTraceWireModel trace, Boolean areRegexNames = false)
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

			Assert.True(succeeded, builder.ToString());
		}

		public static void SegmentsDoNotExist([NotNull] IEnumerable<String> unexpectedTraceSegmentNames, [NotNull] TransactionTraceWireModel trace, Boolean areRegexNames = false)
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

			Assert.True(succeeded, builder.ToString());
		}
	}

	internal static class ErrorTraceAssertions
	{
		public static void ErrorTraceHasAttributes([NotNull] IEnumerable<ExpectedAttribute> expectedAttributes, AttributeClassification attributeClassification, [NotNull] ErrorTraceWireModel errorTrace)
		{
			var errorMessageBuilder = new StringBuilder();
			var actualAttributes = errorTrace.GetAttributes(attributeClassification);
			var allAttributesMatch = ExpectedAttribute.CheckIfAllAttributesMatch(actualAttributes, expectedAttributes, errorMessageBuilder);

			Assert.True(allAttributesMatch, errorMessageBuilder.ToString());
		}

		public static void ErrorTraceDoesNotHaveAttributes([NotNull] IEnumerable<String> unexpectedAttributes, AttributeClassification attributeClassification, [NotNull] ErrorTraceWireModel errorTrace)
		{
			var errorMessageBuilder = new StringBuilder();
			var actualAttributes = errorTrace.GetAttributes(attributeClassification);
			var allAttributesNotFound = ExpectedAttribute.CheckIfAllAttributesNotFound(actualAttributes, unexpectedAttributes, errorMessageBuilder);

			Assert.True(allAttributesNotFound, errorMessageBuilder.ToString());
		}
	}

	internal class ExpectedMetric
	{
		public String Name;
		public String Scope;
		public Boolean IsRegexName = false;
		public Int32? Value0 = null;
		public Single? Value1 = null;
		public Single? Value2 = null;
		public Single? Value3 = null;
		public Single? Value4 = null;
		public Single? Value5 = null;
	}

	internal class ExpectedApdexMetric : ExpectedMetric
	{
		public Int32? SatisfyingCount { get { return Value0; } set { Value0 = value; } }
		public Single? ToleratingCount { get { return Value1; } set { Value1 = value; } }
		public Single? FrustratingCount { get { return Value2; } set { Value2 = value; } }
		public Single? Min { get { return Value3; } set { Value3 = value; } }
		public Single? Max { get { return Value4; } set { Value4 = value; } }
		public Single? Unused { get { return Value5; } set { Value5 = value; } }
	}

	internal class ExpectedCountMetric : ExpectedMetric
	{
		public Int32? CallCount { get { return Value0; } set { Value0 = value; } }
		public Single? Total { get { return Value1; } set { Value1 = value; } }
		public Single? TotalExclusive { get { return Value2; } set { Value2 = value; } }
		public Single? Min { get { return Value3; } set { Value3 = value; } }
		public Single? Max { get { return Value4; } set { Value4 = value; } }
		public Single? SumOfSquares { get { return Value5; } set { Value5 = value; } }
	}

	internal class ExpectedTimeMetric : ExpectedMetric
	{
		public Int32? CallCount { get { return Value0; } set { Value0 = value; } }
		public Single? Total { get { return Value1; } set { Value1 = value; } }
		public Single? TotalExclusive { get { return Value2; } set { Value2 = value; } }
		public Single? Min { get { return Value3; } set { Value3 = value; } }
		public Single? Max { get { return Value4; } set { Value4 = value; } }
		public Single? SumOfSquaresInSeconds { get { return Value5; } set { Value5 = value; } }
	}

	internal class ExpectedAttribute
	{
		public String Key;
		public Object Value;

		public static Boolean CheckIfAllAttributesMatch([NotNull] IDictionary<String, Object> actualAttributes, [NotNull] IEnumerable<ExpectedAttribute> expectedAttributes, [NotNull] StringBuilder errorMessageBuilder)
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

		private static Boolean HaveSameValue(Object actualValue, Object expectedValue)
		{
			if (actualValue == null)
				return expectedValue == null;

			if (actualValue is String && expectedValue is String)
				return String.Equals((String)actualValue, (String)expectedValue);

			return actualValue.Equals(expectedValue);
		}

		public static Boolean CheckIfAllAttributesNotFound([NotNull] IDictionary<String, Object> actualAttributes, [NotNull] IEnumerable<String> unexpectedAttributes, [NotNull] StringBuilder errorMessageBuilder)
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
		[NotNull]
		public static IDictionary<String, Object> GetAttributes([NotNull] this TransactionEventWireModel transactionEvent, AttributeClassification attributeClassification)
		{
			switch (attributeClassification)
			{
				case AttributeClassification.Intrinsics:
					return transactionEvent.IntrinsicAttributes;
				case AttributeClassification.AgentAttributes:
					return transactionEvent.AgentAttributes;
				case AttributeClassification.UserAttributes:
					return transactionEvent.UserAttributes;
				default:
					throw new NotImplementedException();
			}
		}

		[NotNull]
		public static IDictionary<String, Object> GetAttributes([NotNull] this CustomEventWireModel customEvent, AttributeClassification attributeClassification)
		{
			switch (attributeClassification)
			{
				case AttributeClassification.Intrinsics:
					return customEvent.IntrinsicAttributes.ToDictionary();
				case AttributeClassification.UserAttributes:
					return customEvent.UserAttributes.ToDictionary();
				default:
					throw new NotImplementedException();
			}
		}

		[NotNull]
		public static IDictionary<String, Object> GetAttributes([NotNull] this TransactionTraceWireModel transactionTrace, AttributeClassification attributeClassification)
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

		[NotNull]
		public static IDictionary<String, Object> GetAttributes([NotNull] this ErrorTraceWireModel errorTrace, AttributeClassification attributeClassification)
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
