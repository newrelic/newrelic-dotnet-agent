using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using Telerik.JustMock;
using NewRelic.Agent.Core.Transactions;

namespace NewRelic.Agent.Core.Segments.Tests
{
	[TestFixture]
	public class CustomSegmentDataTests
	{
		private ITransactionSegmentState _transactionSegmentState;

		[SetUp]
		public void SetUp()
		{
			_transactionSegmentState = Mock.Create<ITransactionSegmentState>();
		}

		#region IsCombinableWith


		private Segment CreateCustomSegmentBuilder(MethodCallData methodCallData, string name, bool combinable)
		{
			var customSegmentData = new CustomSegmentData(name);
			var segment = new Segment(_transactionSegmentState, methodCallData);
			segment.Combinable = combinable;
			segment.SetSegmentData(customSegmentData);

			return segment;
		}

		[Test]
		public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
		{
			var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
			var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);

			Assert.IsTrue(segment1.IsCombinableWith(segment2));
		}


		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
		{
			var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
			var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", false);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
		{
			var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", false);
			var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", false);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
		{
			var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
			var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 2), "name", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
		{
			var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
			var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type2", "method", 1), "name", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
		{
			var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
			var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method2", 1), "name", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentName()
		{
			var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
			var segment2 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name2", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentSegmentType()
		{
			var segment1 = CreateCustomSegmentBuilder(new MethodCallData("type", "method", 1), "name", true);
			var segment2 = MethodSegmentDataTests.createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "type", "method", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		#endregion IsCombinableWith
	}
}
