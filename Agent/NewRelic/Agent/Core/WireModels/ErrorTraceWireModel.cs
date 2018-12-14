using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
	[JsonConverter(typeof(JsonArrayConverter))]
	public class ErrorTraceWireModel
	{
		/// <summary>
		/// The UTC timestamp indicating when the error occurred. 
		/// </summary>
		[JsonArrayIndex(Index = 0)]
		[DateTimeSerializesAsUnixTimeMilliseconds]
		public virtual DateTime TimeStamp { get; }

		/// <summary>
		/// ex. WebTransaction/ASP/post.aspx or NoticeError API Call if outside a transaction
		/// UI calls this field 'URL' in Errors page; 'Transaction Name' in Error Analytics page
		/// </summary>
		[JsonArrayIndex(Index = 1)]
		public virtual string Path { get; }

		/// <summary>
		/// The error message.
		/// </summary>
		[JsonArrayIndex(Index = 2)]
		public virtual string Message { get; }

		/// <summary>
		/// The class name of the exception thrown.
		/// </summary>
		[JsonArrayIndex(Index = 3)]
		public virtual string ExceptionClassName { get; }

		/// <summary>
		/// Parameters associated with this error.
		/// </summary>
		[JsonArrayIndex(Index = 4)]
		public virtual ErrorTraceAttributesWireModel Attributes { get; }

		/// <summary>
		/// Guid of this error.
		/// </summary>
		[JsonArrayIndex(Index = 5)]
		public virtual string Guid { get; }

		public ErrorTraceWireModel(DateTime timestamp, string path, string message, string exceptionClassName, ErrorTraceAttributesWireModel attributes, string guid)
		{
			TimeStamp = timestamp;
			Path = path;
			Message = message;
			ExceptionClassName = exceptionClassName;
			Attributes = attributes;
			Guid = guid;
		}

		[JsonObject(MemberSerialization.OptIn)]
		public class ErrorTraceAttributesWireModel
		{
			[JsonProperty("stack_trace")]
			public virtual IEnumerable<string> StackTrace { get; }

			[JsonProperty("agentAttributes")]
			public virtual IEnumerable<KeyValuePair<string, Object>> AgentAttributes { get; }

			[JsonProperty("userAttributes")]
			public virtual IEnumerable<KeyValuePair<string, Object>> UserAttributes { get; }

			[JsonProperty("intrinsics")]
			public virtual IEnumerable<KeyValuePair<string, Object>> Intrinsics { get; }

			public ErrorTraceAttributesWireModel(IEnumerable<KeyValuePair<string, Object>> agentAttributes, IEnumerable<KeyValuePair<string, Object>> intrinsicAttributes, IEnumerable<KeyValuePair<string, Object>> userAttributes, [CanBeNull] IEnumerable<string> stackTrace = null)
			{
				AgentAttributes = agentAttributes.ToReadOnlyDictionary();
				Intrinsics = intrinsicAttributes.ToReadOnlyDictionary();
				UserAttributes = userAttributes.ToReadOnlyDictionary();

				if (stackTrace != null)
					StackTrace = new ReadOnlyCollection<string>(new List<string>(stackTrace));
			}
		}
	}
}
