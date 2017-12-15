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

		[NotNull]
		IEnumerable<KeyValuePair<string, string>> RequestParameters { get; }

		[NotNull]
		IEnumerable<KeyValuePair<string, string>> ServiceParameters { get; }

		[NotNull]
		IEnumerable<KeyValuePair<string, Object>> UserAttributes { get; }

		[NotNull]
		IEnumerable<KeyValuePair<string, Object>> UserErrorAttributes { get; }

		string Uri { get; }
		[CanBeNull]
		string OriginalUri { get; }
		[CanBeNull]
		string ReferrerUri { get; }

		int? HttpResponseStatusCode { get; }
		TimeSpan? QueueTime { get; }
	}
}
