using System;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda.Traces
{
	internal class ErrorTrace
	{
		private DateTimeOffset _timestamp;
		private string _transactionName;
		private string _message;
		private string _errorType;
		private string _stackTrace;
		private IDictionary<string, object> _intrinsics;
		private IDictionary<string, object> _userAttributes;
		private string _transactionGuid;

		public ErrorTrace(DateTimeOffset timestamp, string transactionName, string message, string errorType, string stackTrace, IDictionary<string, object> intrinsics, IDictionary<string, object> userAttributes, string transactionGuid)
		{
			_timestamp = timestamp;
			_transactionName = transactionName;
			_message = message;
			_errorType = errorType;
			_stackTrace = stackTrace;
			_intrinsics = intrinsics;
			_userAttributes = userAttributes;
			_transactionGuid = transactionGuid;
		}

		private IDictionary<string, object> Attributes
		{
			get
			{
				var attributes = new Dictionary<string, object>();
				attributes.Add("stack_trace", new object[] { _stackTrace });
				attributes.Add("agentAttributes", new Dictionary<string, object>());
				attributes.Add("userAttributes", _userAttributes);
				attributes.Add("intrinsics", _intrinsics);
				return attributes;
			}
		}

		public object[] BuildErrorTraceObject()
		{
			return new object[] { _timestamp.ToUnixTimeMilliseconds(), _transactionName, _message, _errorType, Attributes, _transactionGuid };
		}
	}
}