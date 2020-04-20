using System;

namespace NewRelic.Agent.Core.Errors
{
	public class ErrorData
	{
		public string ErrorMessage { get; }
		public string ErrorTypeName { get; }
		public string StackTrace { get; }
		public DateTime NoticedAt { get; }
		public string Path { get; set; }
		public ReadOnlyDictionary<string, object> CustomAttributes { get; }

		public const string StripExceptionMessagesMessage = "Message removed by New Relic based on your currently enabled security settings.";

		public ErrorData(string errorMessage, string errorTypeName, string stackTrace, DateTime noticedAt, ReadOnlyDictionary<string, object> customAttributes)
		{
			NoticedAt = noticedAt;
			StackTrace = stackTrace;
			ErrorMessage = errorMessage;
			ErrorTypeName = errorTypeName;
			Path = null;
			CustomAttributes = customAttributes;
		}
	}
}
