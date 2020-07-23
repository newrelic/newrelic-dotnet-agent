﻿using System;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	[TestFixture]
	public class ExternalSegmentTests
	{
		private const String TransactionGuidSegmentParameterKey = "transaction_guid";

		[Test]
		public void Build_IncludesCatParameter_IfCatResponseDataIsSet()
		{
			var segment = new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), new ExternalSegmentData(new Uri("http://www.google.com"), "method",
				new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3, "guid", false)));
			segment.End();

			Assert.IsTrue(segment.Parameters.ToDictionary().ContainsKey(TransactionGuidSegmentParameterKey));
			Assert.AreEqual("guid", segment.Parameters.ToDictionary()[TransactionGuidSegmentParameterKey]);
		}

		[Test]
		public void Build_DoesNotIncludeCatParameter_IfCatResponseDataIsNotSet()
		{
			var segment = new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), new ExternalSegmentData(new Uri("http://www.google.com"), "method"));

			Assert.IsFalse(segment.Parameters.ToDictionary().ContainsKey(TransactionGuidSegmentParameterKey));
		}
	}
}
