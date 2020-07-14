using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
	[JsonConverter(typeof(JsonArrayConverter))]
	public class CustomEventWireModel
	{
		[JsonArrayIndex(Index = 0)]
		[NotNull]
		public readonly IEnumerable<KeyValuePair<String, Object>> IntrinsicAttributes;

		[JsonArrayIndex(Index = 1)]
		[NotNull]
		public readonly IEnumerable<KeyValuePair<String, Object>> UserAttributes;

		private const String EventTypeKey = "type";
		private const String TimeStampKey = "timestamp";

		/// <param name="eventType">The event type.</param>
		/// <param name="eventTimeStamp">The start time of the event.</param>
		/// <param name="userAttributes">Additional attributes supplied by the user.</param>
		public CustomEventWireModel([NotNull] String eventType, DateTime eventTimeStamp, [NotNull] IEnumerable<KeyValuePair<String, Object>> userAttributes)
		{
			IntrinsicAttributes = new Dictionary<String, Object>
			{
				{EventTypeKey, eventType},
				{TimeStampKey, eventTimeStamp.ToUnixTimeSeconds()},
			};

			UserAttributes = userAttributes.ToDictionary();
		}
	}
}