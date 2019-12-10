using NewRelic.OpenTracing.AmazonLambda.Events;
using System;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda
{
	internal class ErrorEventBuilder
	{
		private string _errorClass;
		private string _errorMessage;
		private string _transactionName = "Unknown";
		private string _transactionGuid;
		private DateTimeOffset _timestamp;
		private TimeSpan _transactionDuration;
		private IDictionary<string, object> _distributedTraceIntrinsics;
		private IDictionary<string, object> _userAttributes;

		public ErrorEventBuilder SetTransactionName(string transactionName)
		{
			_transactionName = transactionName;
			return this;
		}

		public ErrorEventBuilder SetTransactionGuid(string transactionGuid)
		{
			_transactionGuid = transactionGuid;
			return this;
		}

		public ErrorEventBuilder SetDistributedTraceIntrinsics(IDictionary<string, object> distributedTraceIntrinsics)
		{
			_distributedTraceIntrinsics = distributedTraceIntrinsics;
			return this;
		}

		public ErrorEventBuilder SetErrorClass(string errorClass)
		{
			_errorClass = errorClass;
			return this;
		}

		public ErrorEventBuilder SetErrorMessage(string errorMessage)
		{
			_errorMessage = errorMessage;
			return this;
		}

		public ErrorEventBuilder SetTimestamp(DateTimeOffset timestamp)
		{
			_timestamp = timestamp;
			return this;
		}

		public ErrorEventBuilder SetTransactionDuration(TimeSpan transactionDuration)
		{
			_transactionDuration = transactionDuration;
			return this;
		}

		public ErrorEventBuilder SetUserAttributes(IDictionary<string, object> userAttributes)
		{
			_userAttributes = userAttributes;
			return this;
		}

		public ErrorEvent CreateError()
		{
			return new ErrorEvent(_timestamp, _transactionDuration, _errorClass, _errorMessage, _transactionName,
					_transactionGuid, _userAttributes, _distributedTraceIntrinsics);
		}
	}
}