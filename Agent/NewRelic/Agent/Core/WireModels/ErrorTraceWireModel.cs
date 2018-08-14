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
		/// ex. WebTransaction/ASP/post.aspx
		/// </summary>
		[JsonArrayIndex(Index = 1)]
		[NotNull] public virtual String Path { get; }

		/// <summary>
		/// The error message.
		/// </summary>
		[JsonArrayIndex(Index = 2)]
		[NotNull] public virtual String Message { get; }

		/// <summary>
		/// The class name of the exception thrown.
		/// </summary>
		[JsonArrayIndex(Index = 3)]
		[NotNull] public virtual String ExceptionClassName { get; }

		/// <summary>
		/// Parameters associated with this error.
		/// </summary>
		[JsonArrayIndex(Index = 4)]
		[NotNull] public virtual ErrorTraceAttributesWireModel Attributes { get; }

		/// <summary>
		/// Guid of this error.
		/// </summary>
		[JsonArrayIndex(Index = 5)]
		[CanBeNull] public virtual String Guid { get; }

		public ErrorTraceWireModel(DateTime timestamp, [NotNull] String path, [NotNull] String message, [NotNull] String exceptionClassName, [NotNull] ErrorTraceAttributesWireModel attributes, [CanBeNull] String guid)
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
			[CanBeNull] public virtual IEnumerable<String> StackTrace { get; }

			[JsonProperty("agentAttributes")]
			[NotNull] public virtual IEnumerable<KeyValuePair<String, Object>> AgentAttributes { get; }

			[JsonProperty("userAttributes")]
			[NotNull] public virtual IEnumerable<KeyValuePair<String, Object>> UserAttributes { get; }

			[JsonProperty("intrinsics")]
			[NotNull] public virtual IEnumerable<KeyValuePair<String, Object>> Intrinsics { get; }

			public ErrorTraceAttributesWireModel([NotNull] IEnumerable<KeyValuePair<String, Object>> agentAttributes, [NotNull] IEnumerable<KeyValuePair<String, Object>> intrinsicAttributes, [NotNull] IEnumerable<KeyValuePair<String, Object>> userAttributes, [CanBeNull] IEnumerable<String> stackTrace = null)
			{
				AgentAttributes = agentAttributes.ToReadOnlyDictionary();
				Intrinsics = intrinsicAttributes.ToReadOnlyDictionary();
				UserAttributes = userAttributes.ToReadOnlyDictionary();

				if (stackTrace != null)
					StackTrace = new ReadOnlyCollection<String>(new List<String>(stackTrace));
			}
		}
	}
}
