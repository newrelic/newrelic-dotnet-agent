using NewRelic.OpenTracing.AmazonLambda.Events;
using NewRelic.OpenTracing.AmazonLambda.Traces;
using System;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda
{
	internal class Errors
	{
		private IList<ErrorEvent> _errorEvents = new List<ErrorEvent>();
		private IList<ErrorTrace> _errorTraces = new List<ErrorTrace>();

		internal void RecordErrors(LambdaSpan span)
		{
			var errorEventLogEntry = span.GetSpanLogEntry("event");
			if (errorEventLogEntry == null || !"error".Equals(errorEventLogEntry.Value))
			{
				return;
			}
			
			var errorObjectLogEntry = span.GetSpanLogEntry("error.object");
			if (errorObjectLogEntry == null)
			{
				return;
			}
			
			var errorMessageLogEntry = span.GetSpanLogEntry("message");
			var errorObjectIsThrowable = errorObjectLogEntry.Value is Exception;
			if (errorMessageLogEntry == null && !errorObjectIsThrowable)
			{
				return;
			}

			var errorClass = errorObjectLogEntry.Value.GetType().Name;
			var errorMessage = GetErrorMessage(errorMessageLogEntry, errorObjectLogEntry);
			var userAttributes = CreateUserAttributes(span);

			var txnState = span.RootSpan.TransactionState;

			var errorEvent = new ErrorEventBuilder()
				.SetErrorClass(errorClass)
				.SetErrorMessage(errorMessage)
				.SetTransactionDuration(txnState.Duration)
				.SetTimestamp(errorObjectLogEntry.Timestamp)
				.SetUserAttributes(userAttributes)
				.SetTransactionName(txnState.TransactionName)
				.SetTransactionGuid(txnState.TransactionId)
				.SetDistributedTraceIntrinsics(span.Intrinsics)
				.CreateError();
			_errorEvents.Add(errorEvent);

			LogEntry errorStack = span.GetSpanLogEntry("stack");
			if (errorStack == null && !errorObjectIsThrowable)
			{
				return;
			}

			string stackTrace = GetStackTrace(errorStack, errorObjectLogEntry);
			var errorTrace = new ErrorTraceBuilder()
				.SetErrorMessage(errorMessage)
				.SetErrorType(errorClass)
				.SetTransactionGuid(txnState.TransactionId)
				.SetTransactionName(txnState.TransactionName)
				.SetDistributedTraceIntrinsics(span.Intrinsics)
				.SetUserAttributes(userAttributes)
				.SetTimestamp(errorObjectLogEntry.Timestamp)
				.SetStackTrace(stackTrace)
				.CreateErrorTrace();
			_errorTraces.Add(errorTrace);
		}

		private IDictionary<string, object> CreateUserAttributes(LambdaSpan span)
		{
			var attributes = new Dictionary<string, object>();
			var errorKind = span.GetSpanLogEntry("error.kind");
			if (errorKind != null)
			{
				attributes.Add("error.kind", errorKind.Value);
			}
			return attributes;
		}

		private string GetErrorMessage(LogEntry errorMessage, LogEntry errorObject)
		{
			if (errorMessage != null)
			{
				return errorMessage.Value.ToString();
			}

			if (errorObject.Value is Exception)
			{
				return ((Exception)errorObject.Value).Message;
			}

			return null;
		}

		private string GetStackTrace(LogEntry errorStack, LogEntry errorObject)
		{
			if (errorStack != null && errorStack.Value is string)
			{
				return (string)errorStack.Value;
			}
			else if (errorObject != null && errorObject.Value is Exception)
			{
				return ((Exception)errorObject.Value).StackTrace;
			}
			return string.Empty;
		}

		public IList<ErrorEvent> GetAndClearEvents()
		{
			var ret = _errorEvents;
			_errorEvents = new List<ErrorEvent>();
			return ret;
		}

		public IList<ErrorTrace> GetAndClearTraces()
		{
			var ret = _errorTraces;
			_errorTraces = new List<ErrorTrace>();
			return ret;
		}
	}
}