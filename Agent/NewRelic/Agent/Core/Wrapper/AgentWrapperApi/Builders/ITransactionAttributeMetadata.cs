using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	/// <summary>
	/// These fields are all accessed prior to the end of the transaction (for RUM)
	/// and after the transaction ends. Some of the fields are also accessed for CAT too.
	/// This means all the underlying fields need to be accessable on multiple threads.
	/// </summary>
	public interface ITransactionAttributeMetadata
	{
		KeyValuePair<string, string>[] RequestParameters { get; }
		KeyValuePair<string, object>[] UserAttributes { get; }
		KeyValuePair<string, object>[] UserErrorAttributes { get; }

		string Uri { get; }
		[CanBeNull]
		string OriginalUri { get; }
		[CanBeNull]
		string ReferrerUri { get; }

		int? HttpResponseStatusCode { get; }
		TimeSpan? QueueTime { get; }
	}
}
